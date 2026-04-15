using System.Security.Cryptography;
using System.Text;
using ImageUploadApp.Data;
using ImageUploadApp.Infrastructure;
using ImageUploadApp.Models;
using ImageUploadApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ImageUploadApp.Controllers;

[Authorize]
public class PhotosController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PhotosController> _log;
    private readonly IPhotoFolderService _folders;
    private readonly IPhotoBatchUploadService _batchUpload;

    public PhotosController(
        ApplicationDbContext db,
        UserManager<IdentityUser> users,
        IWebHostEnvironment env,
        ILogger<PhotosController> log,
        IPhotoFolderService folders,
        IPhotoBatchUploadService batchUpload)
    {
        _db = db;
        _users = users;
        _env = env;
        _log = log;
        _folders = folders;
        _batchUpload = batchUpload;
    }

    public async Task<IActionResult> Index(Guid? folderId, int page = 1, int pageSize = 12, int cols = 6, CancellationToken cancellationToken = default)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var vm = await BuildPhotosIndexVmAsync(userId, folderId, page, pageSize, cols, cancellationToken);
        this.SetSeo(
            "Ảnh đã tải lên",
            "Album riêng của bạn trên Storage Free — chỉ tài khoản đăng nhập mới xem được.",
            "noindex, nofollow",
            null);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Sync(Guid? folderId, int page = 1, int pageSize = 12, int cols = 6, string? revision = null, CancellationToken cancellationToken = default)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var vm = await BuildPhotosIndexVmAsync(userId, folderId, page, pageSize, cols, cancellationToken);

        if (!string.IsNullOrWhiteSpace(revision)
            && string.Equals(revision.Trim(), vm.Revision, StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { changed = false, revision = vm.Revision });
        }

        var from = vm.TotalCount == 0 ? 0 : (vm.Page - 1) * vm.PageSize + 1;
        var to = vm.TotalCount == 0 ? 0 : Math.Min(vm.Page * vm.PageSize, vm.TotalCount);
        var compactGrid = vm.Cols >= 8;

        var items = vm.Items.Select(x => new {
            id = x.Id,
            folderId = x.FolderId,
            createdAt = x.CreatedAt.ToString("O"),
            dateLabel = x.CreatedAt.LocalDateTime.ToString("g"),
            isPending = x.IsPending,
            isFailed = x.IsFailed,
            proxyUrl = x.ProxyUrl,
            downloadUrl = x.DownloadUrl,
        });

        var childFolders = vm.ChildFolders.Select(f => new { id = f.Id, name = f.Name, photoCount = f.PhotoCount }).ToList();
        var breadcrumb = vm.Breadcrumb.Select(b => new { id = b.Id, name = b.Name }).ToList();

        return Json(new {
            changed = true,
            revision = vm.Revision,
            usedBytes = vm.UsedBytes,
            usedSizeLabel = vm.UsedSizeLabel,
            quotaPercent = vm.QuotaPercent,
            totalCount = vm.TotalCount,
            totalLibraryCount = vm.TotalLibraryCount,
            from,
            to,
            totalPages = vm.TotalPages,
            page = vm.Page,
            pageSize = vm.PageSize,
            cols = vm.Cols,
            compactGrid,
            folderId = vm.FolderId,
            parentFolderId = vm.ParentFolderId,
            childFolders,
            breadcrumb,
            items,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFolder(string name, Guid? parentFolderId, CancellationToken cancellationToken = default)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var (ok, err, id) = await _folders.CreateAsync(userId, name, parentFolderId, cancellationToken);
        if (!ok)
        {
            TempData["Error"] = err;
            return RedirectToAction(nameof(Index), parentFolderId.HasValue ? new { folderId = parentFolderId } : null);
        }

        return RedirectToAction(nameof(Index), new { folderId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFolder(Guid folderId, CancellationToken cancellationToken = default)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var (ok, err, redirectParentId) = await _folders.DeleteFolderAsync(userId, folderId, cancellationToken);
        if (!ok)
            TempData["Error"] = err;
        return RedirectToAction(nameof(Index), redirectParentId.HasValue ? new { folderId = redirectParentId } : null);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MovePhotosBulk(string imageIds, Guid? targetFolderId, Guid? returnFolderId, CancellationToken cancellationToken = default)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var ids = new List<Guid>();
        foreach (var part in (imageIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(part, out var g))
                ids.Add(g);
        }

        var (ok, err, _) = await _folders.MoveImagesBulkAsync(userId, ids, targetFolderId, cancellationToken);
        if (!ok)
            TempData["Error"] = err;
        return RedirectToAction(nameof(Index), new { folderId = returnFolderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MovePhoto(Guid imageId, Guid? targetFolderId, Guid? returnFolderId, CancellationToken cancellationToken = default)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var (ok, err) = await _folders.MoveImageAsync(userId, imageId, targetFolderId, cancellationToken);
        if (!ok)
            TempData["Error"] = err;
        return RedirectToAction(nameof(Index), new { folderId = returnFolderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(Guid imageId, Guid? returnFolderId, CancellationToken cancellationToken = default)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var img = await _db.Images.FirstOrDefaultAsync(i => i.Id == imageId && i.UserId == userId, cancellationToken);
        if (img is null)
        {
            TempData["Error"] = "Không tìm thấy ảnh.";
            return RedirectToAction(nameof(Index), returnFolderId.HasValue ? new { folderId = returnFolderId } : null);
        }

        PendingImageFileHelper.TryDeleteForImage(img.PendingFilePath, _env, _log);
        _db.Images.Remove(img);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Message"] = "Đã xóa ảnh.";
        return RedirectToAction(nameof(Index), returnFolderId.HasValue ? new { folderId = returnFolderId } : null);
    }

    private async Task<PhotosIndexVm> BuildPhotosIndexVmAsync(
        string userId, Guid? folderId, int page, int pageSize, int cols, CancellationToken cancellationToken)
    {
        int[] allowedPageSizes = [6, 12, 24, 48];
        if (!allowedPageSizes.Contains(pageSize))
            pageSize = 12;
        int[] allowedCols = [2, 3, 4, 6, 8, 10];
        if (!allowedCols.Contains(cols))
            cols = 6;
        if (page < 1)
            page = 1;

        Guid? effectiveFolderId = folderId;
        PhotoFolder? ownedFolder = null;
        if (effectiveFolderId.HasValue)
        {
            ownedFolder = await _folders.GetOwnedFolderAsync(userId, effectiveFolderId.Value, cancellationToken);
            if (ownedFolder is null)
                effectiveFolderId = null;
        }

        var childFolders = await _folders.ListChildFoldersWithCountsAsync(userId, effectiveFolderId, cancellationToken);
        var breadcrumb = await _folders.GetBreadcrumbAsync(userId, effectiveFolderId, cancellationToken);
        string? folderName = ownedFolder?.Name;

        var baseQuery = _db.Images.AsNoTracking().Where(x => x.UserId == userId);
        if (effectiveFolderId.HasValue)
            baseQuery = baseQuery.Where(x => x.FolderId == effectiveFolderId.Value);
        else
            baseQuery = baseQuery.Where(x => x.FolderId == null);

        var usedBytes = await _db.Images.AsNoTracking()
            .Where(x => x.UserId == userId)
            .SumAsync(x => x.StoredSizeBytes ?? 0L, cancellationToken);

        var totalLibraryCount = await _db.Images.AsNoTracking()
            .CountAsync(x => x.UserId == userId, cancellationToken);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
        if (page > totalPages)
            page = totalPages;

        var rows = await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new { x.Id, x.FolderId, x.CreatedAt, x.SourceUrl, x.PipelineFailed })
            .ToListAsync(cancellationToken);

        var list = rows.Select(x => new PhotoItemVm {
            Id = x.Id,
            FolderId = x.FolderId,
            CreatedAt = x.CreatedAt,
            IsPending = x.SourceUrl == null && !x.PipelineFailed,
            IsFailed = x.PipelineFailed && x.SourceUrl == null,
            ProxyUrl = x.SourceUrl == null ? null : Url.Action(nameof(MediaController.Get), "Media", new { id = x.Id }),
            DownloadUrl = x.SourceUrl == null ? null : Url.Action(nameof(MediaController.Get), "Media", new { id = x.Id, download = true }),
        }).ToList();

        var rev = ComputePhotosRevision(effectiveFolderId, usedBytes, totalCount, childFolders, list);

        return new PhotosIndexVm {
            FolderId = effectiveFolderId,
            FolderName = folderName,
            ChildFolders = childFolders,
            Breadcrumb = breadcrumb,
            ParentFolderId = ownedFolder?.ParentFolderId,
            TotalLibraryCount = totalLibraryCount,
            Items = list,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Cols = cols,
            UsedBytes = usedBytes,
            Revision = rev,
        };
    }

    private static string ComputePhotosRevision(
        Guid? folderId,
        long usedBytes,
        int totalCount,
        IReadOnlyList<PhotoFolderListItem> childFolders,
        IReadOnlyList<PhotoItemVm> items)
    {
        var sb = new StringBuilder(80 + items.Count * 48 + childFolders.Count * 40);
        sb.Append(folderId?.ToString("N") ?? "all").Append('|').Append(usedBytes).Append('|').Append(totalCount).Append('|');
        foreach (var f in childFolders.OrderBy(x => x.Id))
            sb.Append('F').Append(f.Id.ToString("N")).Append(':').Append(f.PhotoCount).Append(';');
        sb.Append('|');
        foreach (var x in items)
        {
            var state = x.IsPending ? 'p' : x.IsFailed ? 'f' : 'r';
            sb.Append(x.Id.ToString("N")).Append(':').Append(state).Append(';');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash)[..16];
    }

    [HttpGet]
    public async Task<IActionResult> Status([FromQuery] string? ids, CancellationToken cancellationToken)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(ids))
            return Json(new { items = Array.Empty<object>() });

        var guidList = new List<Guid>();
        foreach (var part in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(part, out var g))
                guidList.Add(g);
        }

        if (guidList.Count == 0)
            return Json(new { items = Array.Empty<object>() });

        IQueryable<ImageRecord> q = _db.Images.AsNoTracking()
            .Where(x => x.UserId == userId && guidList.Contains(x.Id));

        var rows = await q
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.CreatedAt, x.SourceUrl, x.PipelineFailed })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new {
            id = x.Id,
            isReady = x.SourceUrl != null,
            isFailed = x.PipelineFailed && x.SourceUrl == null,
            proxyUrl = x.SourceUrl == null ? null : Url.Action(nameof(MediaController.Get), "Media", new { id = x.Id }),
            downloadUrl = x.SourceUrl == null ? null : Url.Action(nameof(MediaController.Get), "Media", new { id = x.Id, download = true }),
            createdAt = x.CreatedAt,
        });

        return Json(new { items });
    }

    [HttpGet]
    public async Task<IActionResult> Upload(Guid? folderId, CancellationToken cancellationToken = default)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        if (folderId.HasValue)
        {
            var f = await _folders.GetOwnedFolderAsync(userId, folderId.Value, cancellationToken);
            if (f is null)
                folderId = null;
            else
            {
                ViewData["UploadFolderId"] = folderId.Value;
                ViewData["UploadFolderName"] = f.Name;
            }
        }

        SetUploadPageSeo();
        return View();
    }

    private void SetUploadPageSeo()
    {
        this.SetSeo(
            "Tải ảnh",
            "Tải ảnh lên album riêng trên Storage Free — hỗ trợ nhiều file mỗi lần.",
            "noindex, nofollow",
            "/Photos/Upload");
    }

    private async Task ApplyUploadFolderViewDataAsync(string userId, Guid? folderId, CancellationToken cancellationToken)
    {
        if (!folderId.HasValue)
            return;
        var f = await _folders.GetOwnedFolderAsync(userId, folderId.Value, cancellationToken);
        if (f is null)
            return;
        ViewData["UploadFolderId"] = folderId.Value;
        ViewData["UploadFolderName"] = f.Name;
    }

    public const int MaxFilesPerUploadRequest = PhotoBatchUploadService.MaxFilesPerRequest;

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Upload(List<IFormFile>? files, Guid? folderId, CancellationToken cancellationToken)
    {
        var wantsJson = string.Equals(Request.Headers["X-Batch-Upload"], "1", StringComparison.Ordinal);

        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var chosen = files?.Where(f => f is { Length: > 0 }).ToList() ?? [];
        if (chosen.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng chọn ít nhất một file ảnh.");
            if (wantsJson)
                return BadRequest(new { ok = false, errors = new[] { "Vui lòng chọn ít nhất một file ảnh." } });
            await ApplyUploadFolderViewDataAsync(userId, folderId, cancellationToken);
            SetUploadPageSeo();
            return View();
        }

        if (chosen.Count > MaxFilesPerUploadRequest)
        {
            var msg = $"Mỗi lần gửi tối đa {MaxFilesPerUploadRequest} ảnh.";
            ModelState.AddModelError(string.Empty, msg);
            if (wantsJson)
                return BadRequest(new { ok = false, errors = new[] { msg } });
            await ApplyUploadFolderViewDataAsync(userId, folderId, cancellationToken);
            SetUploadPageSeo();
            return View();
        }

        var userEntity = await _users.GetUserAsync(User);
        var notifyEmail = userEntity?.Email ?? userId;
        var result = await _batchUpload.UploadAsync(userId, notifyEmail, chosen, folderId, cancellationToken);

        if (result.Enqueued == 0)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e);
            if (wantsJson)
                return BadRequest(new { ok = false, errors = result.Errors.ToArray() });
            await ApplyUploadFolderViewDataAsync(userId, result.EffectiveFolderId, cancellationToken);
            SetUploadPageSeo();
            return View();
        }

        if (wantsJson)
            return Json(new { ok = true, enqueued = result.Enqueued, skipped = result.Skipped, message = result.Message });

        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(Index), new { folderId = result.EffectiveFolderId });
    }
}
