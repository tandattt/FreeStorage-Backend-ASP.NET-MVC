using System.Security.Claims;
using ImageUploadApp.Data;
using ImageUploadApp.Infrastructure;
using ImageUploadApp.Models;
using ImageUploadApp.Models.Api;
using ImageUploadApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImageUploadApp.Controllers.Api;

/// <summary>REST API album cho developer: JWT (web/cookie) hoặc <c>X-Api-Key</c> (khóa tạo tại /developer/api-keys).</summary>
[ApiController]
[Route("api/v1/album")]
[Authorize]
[IgnoreAntiforgeryToken]
public sealed class DeveloperAlbumApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly IPhotoFolderService _folders;
    private readonly IPhotoBatchUploadService _batchUpload;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DeveloperAlbumApiController> _log;

    public DeveloperAlbumApiController(
        ApplicationDbContext db,
        UserManager<IdentityUser> users,
        IPhotoFolderService folders,
        IPhotoBatchUploadService batchUpload,
        IWebHostEnvironment env,
        ILogger<DeveloperAlbumApiController> log)
    {
        _db = db;
        _users = users;
        _folders = folders;
        _batchUpload = batchUpload;
        _env = env;
        _log = log;
    }

    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Missing user id claim.");

    private string MediaAbsoluteUrl(Guid imageId)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        return $"{baseUrl.TrimEnd('/')}/media/{imageId:N}";
    }

    private static int ClampPage(int page) => page < 1 ? 1 : page;

    private static int ClampPageSize(int pageSize, int max = 100)
    {
        if (pageSize < 1) return 20;
        return pageSize > max ? max : pageSize;
    }

    /// <summary>Dung lượng đã dùng / hạn mức / tổng số ảnh.</summary>
    [HttpGet("storage")]
    public async Task<IActionResult> GetStorage(CancellationToken cancellationToken)
    {
        var uid = UserId;
        var usedBytes = await _db.Images.AsNoTracking()
            .Where(x => x.UserId == uid)
            .SumAsync(x => x.StoredSizeBytes ?? 0L, cancellationToken);
        var totalImages = await _db.Images.AsNoTracking()
            .CountAsync(x => x.UserId == uid, cancellationToken);
        var quota = StorageLimits.PerUserQuotaBytes;
        var pct = quota <= 0 ? 0 : Math.Min(100, Math.Floor(usedBytes * 100.0 / quota));

        return Ok(new
        {
            usedBytes,
            quotaBytes = quota,
            usedPercent = pct,
            totalImages,
        });
    }

    /// <summary>Danh sách thư mục con (phân trang). Bỏ <paramref name="parentFolderId"/> = cấp gốc.</summary>
    [HttpGet("folders")]
    public async Task<IActionResult> ListFolders(
        [FromQuery] Guid? parentFolderId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var uid = UserId;
        page = ClampPage(page);
        pageSize = ClampPageSize(pageSize, 100);

        var q = _db.PhotoFolders.AsNoTracking().Where(f => f.UserId == uid);
        if (parentFolderId.HasValue)
            q = q.Where(f => f.ParentFolderId == parentFolderId.Value);
        else
            q = q.Where(f => f.ParentFolderId == null);

        var totalCount = await q.CountAsync(cancellationToken);
        var rows = await q
            .OrderBy(f => f.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new { f.Id, f.Name, f.ParentFolderId, f.CreatedAt })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Ok(new { page, pageSize, totalCount, items = Array.Empty<object>() });
        }

        var ids = rows.Select(r => r.Id).ToList();
        var counts = await _db.Images.AsNoTracking()
            .Where(i => i.UserId == uid && i.FolderId != null && ids.Contains(i.FolderId.Value))
            .GroupBy(i => i.FolderId!.Value)
            .Select(g => new { Id = g.Key, Cnt = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Cnt, cancellationToken);

        var items = rows.Select(r => new
        {
            id = r.Id,
            name = r.Name,
            parentFolderId = r.ParentFolderId,
            photoCount = counts.TryGetValue(r.Id, out var c) ? c : 0,
            createdAt = r.CreatedAt,
        });

        return Ok(new { page, pageSize, totalCount, items });
    }

    /// <summary>Danh sách ảnh (phân trang). Không có <paramref name="folderId"/> = chỉ ảnh chưa gán thư mục (gốc album).</summary>
    [HttpGet("photos")]
    public async Task<IActionResult> ListPhotos(
        [FromQuery] Guid? folderId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        var uid = UserId;
        page = ClampPage(page);
        pageSize = ClampPageSize(pageSize, 100);

        Guid? effective = folderId;
        if (effective.HasValue)
        {
            var owned = await _folders.GetOwnedFolderAsync(uid, effective.Value, cancellationToken);
            if (owned is null)
                return BadRequest(new { error = "Thư mục không tồn tại hoặc không thuộc tài khoản." });
        }

        var baseQuery = _db.Images.AsNoTracking().Where(x => x.UserId == uid);
        if (effective.HasValue)
            baseQuery = baseQuery.Where(x => x.FolderId == effective.Value);
        else
            baseQuery = baseQuery.Where(x => x.FolderId == null);

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new { x.Id, x.FolderId, x.CreatedAt, x.SourceUrl, x.PipelineFailed })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new
        {
            id = x.Id,
            folderId = x.FolderId,
            createdAt = x.CreatedAt,
            isReady = x.SourceUrl != null,
            isFailed = x.PipelineFailed && x.SourceUrl == null,
            mediaUrl = x.SourceUrl == null ? null : MediaAbsoluteUrl(x.Id),
        });

        return Ok(new { page, pageSize, totalCount, folderId = effective, items });
    }

    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolder([FromBody] AlbumCreateFolderRequest body, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var (ok, err, id) = await _folders.CreateAsync(UserId, body.Name, body.ParentFolderId, cancellationToken);
        if (!ok)
            return BadRequest(new { error = err });

        var name = body.Name.Trim();
        return StatusCode(StatusCodes.Status201Created, new { id, name, parentFolderId = body.ParentFolderId });
    }

    [HttpDelete("folders/{id:guid}")]
    public async Task<IActionResult> DeleteFolder(Guid id, CancellationToken cancellationToken)
    {
        var (ok, err, _) = await _folders.DeleteFolderAsync(UserId, id, cancellationToken);
        if (!ok)
            return BadRequest(new { error = err });

        return NoContent();
    }

    [HttpPost("photos/upload")]
    [RequestSizeLimit(524_288_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
    public async Task<IActionResult> UploadPhotos([FromForm] Guid? folderId, List<IFormFile>? files, CancellationToken cancellationToken)
    {
        var chosen = files?.Where(f => f is { Length: > 0 }).ToList() ?? [];
        if (chosen.Count == 0)
            return BadRequest(new { ok = false, error = "Cần ít nhất một file (form field: files)." });

        if (chosen.Count > PhotoBatchUploadService.MaxFilesPerRequest)
            return BadRequest(new { ok = false, error = $"Tối đa {PhotoBatchUploadService.MaxFilesPerRequest} file mỗi lần." });

        var uid = UserId;
        var user = await _users.GetUserAsync(User);
        var notifyEmail = user?.Email ?? uid;

        var result = await _batchUpload.UploadAsync(uid, notifyEmail, chosen, folderId, cancellationToken);
        if (result.Enqueued == 0)
            return BadRequest(new { ok = false, errors = result.Errors, message = result.Message });

        return Ok(new
        {
            ok = true,
            enqueued = result.Enqueued,
            skipped = result.Skipped,
            folderId = result.EffectiveFolderId,
            message = result.Message,
        });
    }

    [HttpDelete("photos/{id:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid id, CancellationToken cancellationToken)
    {
        var uid = UserId;
        var img = await _db.Images.FirstOrDefaultAsync(i => i.Id == id && i.UserId == uid, cancellationToken);
        if (img is null)
            return NotFound();

        PendingImageFileHelper.TryDeleteForImage(img.PendingFilePath, _env, _log);
        _db.Images.Remove(img);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
