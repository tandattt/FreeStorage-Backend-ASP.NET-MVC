using ImageUploadApp.Data;
using ImageUploadApp.Infrastructure;
using ImageUploadApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ImageUploadApp.Services;

public interface IPhotoFolderService
{
    Task<IReadOnlyList<PhotoFolderListItem>> ListChildFoldersWithCountsAsync(string userId, Guid? parentFolderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PhotoFolderBreadcrumbItem>> GetBreadcrumbAsync(string userId, Guid? folderId, CancellationToken cancellationToken = default);

    Task<PhotoFolder?> GetOwnedFolderAsync(string userId, Guid folderId, CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error, Guid? Id)> CreateAsync(string userId, string name, Guid? parentFolderId, CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error, Guid? RedirectParentId)> DeleteFolderAsync(string userId, Guid folderId, CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error)> MoveImageAsync(string userId, Guid imageId, Guid? targetFolderId, CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error, int Moved)> MoveImagesBulkAsync(string userId, IReadOnlyList<Guid> imageIds, Guid? targetFolderId, CancellationToken cancellationToken = default);
}

public sealed class PhotoFolderService : IPhotoFolderService
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PhotoFolderService> _log;

    public PhotoFolderService(ApplicationDbContext db, IWebHostEnvironment env, ILogger<PhotoFolderService> log)
    {
        _db = db;
        _env = env;
        _log = log;
    }

    public async Task<IReadOnlyList<PhotoFolderListItem>> ListChildFoldersWithCountsAsync(string userId, Guid? parentFolderId, CancellationToken cancellationToken = default)
    {
        var q = _db.PhotoFolders.AsNoTracking().Where(f => f.UserId == userId);
        if (parentFolderId.HasValue)
            q = q.Where(f => f.ParentFolderId == parentFolderId.Value);
        else
            q = q.Where(f => f.ParentFolderId == null);

        var folders = await q.OrderBy(f => f.Name).Select(f => new { f.Id, f.Name }).ToListAsync(cancellationToken);
        if (folders.Count == 0)
            return [];

        var ids = folders.Select(f => f.Id).ToList();
        var counts = await _db.Images.AsNoTracking()
            .Where(i => i.UserId == userId && i.FolderId != null && ids.Contains(i.FolderId.Value))
            .GroupBy(i => i.FolderId!.Value)
            .Select(g => new { Id = g.Key, Cnt = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Cnt, cancellationToken);

        return folders.Select(f => new PhotoFolderListItem
        {
            Id = f.Id,
            Name = f.Name,
            PhotoCount = counts.TryGetValue(f.Id, out var c) ? c : 0,
        }).ToList();
    }

    public async Task<IReadOnlyList<PhotoFolderBreadcrumbItem>> GetBreadcrumbAsync(string userId, Guid? folderId, CancellationToken cancellationToken = default)
    {
        if (!folderId.HasValue)
            return [];

        var chain = new List<PhotoFolderBreadcrumbItem>();
        Guid? cur = folderId;
        var guard = 0;
        while (guard++ < 64 && cur.HasValue)
        {
            var row = await _db.PhotoFolders.AsNoTracking()
                .Where(f => f.Id == cur.Value && f.UserId == userId)
                .Select(f => new { f.Id, f.Name, f.ParentFolderId })
                .FirstOrDefaultAsync(cancellationToken);
            if (row is null)
                break;
            chain.Add(new PhotoFolderBreadcrumbItem { Id = row.Id, Name = row.Name });
            cur = row.ParentFolderId;
        }

        chain.Reverse();
        return chain;
    }

    public Task<PhotoFolder?> GetOwnedFolderAsync(string userId, Guid folderId, CancellationToken cancellationToken = default)
    {
        return _db.PhotoFolders.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == folderId && f.UserId == userId, cancellationToken);
    }

    public async Task<(bool Ok, string? Error, Guid? Id)> CreateAsync(string userId, string name, Guid? parentFolderId, CancellationToken cancellationToken = default)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
            return (false, "Nhập tên thư mục.", null);
        if (trimmed.Length > 200)
            return (false, "Tên thư mục tối đa 200 ký tự.", null);

        if (parentFolderId.HasValue)
        {
            var parentOk = await _db.PhotoFolders.AnyAsync(f => f.Id == parentFolderId.Value && f.UserId == userId, cancellationToken);
            if (!parentOk)
                return (false, "Thư mục cha không hợp lệ.", null);
        }

        var exists = await _db.PhotoFolders.AnyAsync(
            f => f.UserId == userId && f.Name == trimmed && f.ParentFolderId == parentFolderId,
            cancellationToken);
        if (exists)
            return (false, "Đã có thư mục trùng tên trong cùng cấp.", null);

        var row = new PhotoFolder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = trimmed,
            ParentFolderId = parentFolderId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.PhotoFolders.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null, row.Id);
    }

    public async Task<(bool Ok, string? Error, Guid? RedirectParentId)> DeleteFolderAsync(string userId, Guid folderId, CancellationToken cancellationToken = default)
    {
        var folder = await _db.PhotoFolders.FirstOrDefaultAsync(f => f.Id == folderId && f.UserId == userId, cancellationToken);
        if (folder is null)
            return (false, "Không tìm thấy thư mục.", null);

        var redirectParent = folder.ParentFolderId;

        var imagesInFolder = await _db.Images
            .Where(i => i.UserId == userId && i.FolderId == folderId)
            .ToListAsync(cancellationToken);
        foreach (var img in imagesInFolder)
        {
            PendingImageFileHelper.TryDeleteForImage(img.PendingFilePath, _env, _log);
            _db.Images.Remove(img);
        }

        var childFolders = await _db.PhotoFolders
            .Where(f => f.UserId == userId && f.ParentFolderId == folderId)
            .ToListAsync(cancellationToken);
        foreach (var ch in childFolders)
            ch.ParentFolderId = folder.ParentFolderId;

        _db.PhotoFolders.Remove(folder);
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null, redirectParent);
    }

    public async Task<(bool Ok, string? Error)> MoveImageAsync(string userId, Guid imageId, Guid? targetFolderId, CancellationToken cancellationToken = default)
    {
        var img = await _db.Images.FirstOrDefaultAsync(i => i.Id == imageId && i.UserId == userId, cancellationToken);
        if (img is null)
            return (false, "Không tìm thấy ảnh.");

        if (targetFolderId.HasValue)
        {
            var ok = await _db.PhotoFolders.AnyAsync(f => f.Id == targetFolderId.Value && f.UserId == userId, cancellationToken);
            if (!ok)
                return (false, "Thư mục không hợp lệ.");
        }

        img.FolderId = targetFolderId;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error, int Moved)> MoveImagesBulkAsync(string userId, IReadOnlyList<Guid> imageIds, Guid? targetFolderId, CancellationToken cancellationToken = default)
    {
        if (imageIds.Count == 0)
            return (false, "Chưa chọn ảnh.", 0);

        if (targetFolderId.HasValue)
        {
            var ok = await _db.PhotoFolders.AnyAsync(f => f.Id == targetFolderId.Value && f.UserId == userId, cancellationToken);
            if (!ok)
                return (false, "Thư mục đích không hợp lệ.", 0);
        }

        var n = await _db.Images
            .Where(i => imageIds.Contains(i.Id) && i.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.FolderId, _ => targetFolderId), cancellationToken);

        return (true, null, n);
    }
}
