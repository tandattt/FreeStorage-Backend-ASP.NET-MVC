using System.Security.Cryptography;
using System.Text;
using Hangfire;
using ImageUploadApp.Data;
using ImageUploadApp.Models;
using ImageUploadApp.Services;
using ImageUploadApp.Services.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;

namespace ImageUploadApp.Controllers;

[Authorize]
public class PhotosController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly IImageEncodingService _imageEncoding;
    private readonly IWebHostEnvironment _env;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<PhotosController> _log;
    private readonly ITelegramNotifyService _telegram;

    public PhotosController(
        ApplicationDbContext db,
        UserManager<IdentityUser> users,
        IImageEncodingService imageEncoding,
        IWebHostEnvironment env,
        IBackgroundJobClient backgroundJobs,
        ILogger<PhotosController> log,
        ITelegramNotifyService telegram)
    {
        _db = db;
        _users = users;
        _imageEncoding = imageEncoding;
        _env = env;
        _backgroundJobs = backgroundJobs;
        _log = log;
        _telegram = telegram;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 12, int cols = 6, CancellationToken cancellationToken = default)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var vm = await BuildPhotosIndexVmAsync(userId, page, pageSize, cols, cancellationToken);
        return View(vm);
    }

    /// <summary>Poll 5s: so sánh revision — không đổi thì không cần cập nhật DOM.</summary>
    [HttpGet]
    public async Task<IActionResult> Sync(int page = 1, int pageSize = 12, int cols = 6, string? revision = null, CancellationToken cancellationToken = default)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var vm = await BuildPhotosIndexVmAsync(userId, page, pageSize, cols, cancellationToken);

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
            createdAt = x.CreatedAt.ToString("O"),
            dateLabel = x.CreatedAt.LocalDateTime.ToString("g"),
            isPending = x.IsPending,
            isFailed = x.IsFailed,
            proxyUrl = x.ProxyUrl,
            downloadUrl = x.DownloadUrl,
        });

        return Json(new {
            changed = true,
            revision = vm.Revision,
            usedBytes = vm.UsedBytes,
            usedSizeLabel = vm.UsedSizeLabel,
            quotaPercent = vm.QuotaPercent,
            totalCount = vm.TotalCount,
            from,
            to,
            totalPages = vm.TotalPages,
            page = vm.Page,
            pageSize = vm.PageSize,
            cols = vm.Cols,
            compactGrid,
            items,
        });
    }

    private async Task<PhotosIndexVm> BuildPhotosIndexVmAsync(
        string userId, int page, int pageSize, int cols, CancellationToken cancellationToken)
    {
        int[] allowedPageSizes = [6, 12, 24, 48];
        if (!allowedPageSizes.Contains(pageSize))
            pageSize = 12;
        int[] allowedCols = [2, 3, 4, 6, 8, 10];
        if (!allowedCols.Contains(cols))
            cols = 6;
        if (page < 1)
            page = 1;

        var baseQuery = _db.Images.AsNoTracking().Where(x => x.UserId == userId);

        var usedBytes = await baseQuery.SumAsync(x => x.StoredSizeBytes ?? 0L, cancellationToken);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
        if (page > totalPages)
            page = totalPages;

        var rows = await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new { x.Id, x.CreatedAt, x.SourceUrl, x.PipelineFailed })
            .ToListAsync(cancellationToken);

        var list = rows.Select(x => new PhotoItemVm {
            Id = x.Id,
            CreatedAt = x.CreatedAt,
            IsPending = x.SourceUrl == null && !x.PipelineFailed,
            IsFailed = x.PipelineFailed && x.SourceUrl == null,
            ProxyUrl = x.SourceUrl == null ? null : Url.Action(nameof(MediaController.Get), "Media", new { id = x.Id }),
            DownloadUrl = x.SourceUrl == null ? null : Url.Action(nameof(MediaController.Get), "Media", new { id = x.Id, download = true }),
        }).ToList();

        var rev = ComputePhotosRevision(usedBytes, totalCount, list);

        return new PhotosIndexVm {
            Items = list,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Cols = cols,
            UsedBytes = usedBytes,
            Revision = rev,
        };
    }

    private static string ComputePhotosRevision(long usedBytes, int totalCount, IReadOnlyList<PhotoItemVm> items)
    {
        var sb = new StringBuilder(64 + items.Count * 48);
        sb.Append(usedBytes).Append('|').Append(totalCount).Append('|');
        foreach (var x in items)
        {
            var state = x.IsPending ? 'p' : x.IsFailed ? 'f' : 'r';
            sb.Append(x.Id.ToString("N")).Append(':').Append(state).Append(';');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash)[..16];
    }

    /// <summary>JSON cho poll: chỉ ảnh của user, lọc theo ids (cách nhau bởi dấu phẩy).</summary>
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
    public IActionResult Upload() => View();

    /// <summary>Tối đa số file mỗi request (client chia batch; giảm 413 / request quá lớn).</summary>
    public const int MaxFilesPerUploadRequest = 150;

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Upload(List<IFormFile>? files, CancellationToken cancellationToken)
    {
        var wantsJson = string.Equals(Request.Headers["X-Batch-Upload"], "1", StringComparison.Ordinal);

        var chosen = files?.Where(f => f is { Length: > 0 }).ToList() ?? [];
        if (chosen.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng chọn ít nhất một file ảnh.");
            if (wantsJson)
                return BadRequest(new { ok = false, errors = new[] { "Vui lòng chọn ít nhất một file ảnh." } });
            return View();
        }

        if (chosen.Count > MaxFilesPerUploadRequest)
        {
            var msg = $"Mỗi lần gửi tối đa {MaxFilesPerUploadRequest} ảnh.";
            ModelState.AddModelError(string.Empty, msg);
            if (wantsJson)
                return BadRequest(new { ok = false, errors = new[] { msg } });
            return View();
        }

        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var pendingDir = Path.Combine(_env.ContentRootPath, "App_Data", "pending");
        Directory.CreateDirectory(pendingDir);

        var errors = new List<string>();
        var enqueued = 0;
        long batchBytes = 0;

        var usedBytes = await _db.Images
            .Where(x => x.UserId == userId)
            .SumAsync(x => x.StoredSizeBytes ?? 0L, cancellationToken);

        const long maxImageBytes = 120 * 1024 * 1024; // khớp tầm form limit

        foreach (var file in chosen)
        {
            if (file.Length > maxImageBytes)
            {
                errors.Add($"{DisplayName(file)}: vượt quá {maxImageBytes / (1024 * 1024)} MB.");
                continue;
            }

            var metaErr = ImageUploadGuard.ValidateMetadata(file);
            if (metaErr is not null)
            {
                errors.Add(metaErr);
                continue;
            }

            var (headerOk, headerErr) = await ImageUploadGuard.ValidateHeaderAsync(file, cancellationToken);
            if (!headerOk)
            {
                if (headerErr is not null)
                    errors.Add(headerErr);
                continue;
            }

            var (dimOk, dimErr) = await ImageUploadGuard.ValidateDimensionsAsync(file, cancellationToken);
            if (!dimOk)
            {
                if (dimErr is not null)
                    errors.Add(dimErr);
                continue;
            }

            byte[] jpeg;
            try
            {
                await using var readStream = file.OpenReadStream();
                jpeg = await _imageEncoding.ToJpegAsync(readStream, cancellationToken);
            }
            catch (UnknownImageFormatException)
            {
                errors.Add($"{DisplayName(file)}: không đọc được như ảnh (decode thất bại).");
                continue;
            }

            var newSize = (long)jpeg.Length;
            if (usedBytes + batchBytes + newSize > StorageLimits.PerUserQuotaBytes)
            {
                errors.Add($"{DisplayName(file)}: vượt hạn mức 5 GB cho tài khoản (đã dùng ~{usedBytes / (1024.0 * 1024.0):0.#} MB).");
                continue;
            }

            var id = Guid.NewGuid();
            var fileName = $"{id:N}.jpg";
            var fullPath = Path.Combine(pendingDir, fileName);
            await System.IO.File.WriteAllBytesAsync(fullPath, jpeg, cancellationToken);

            _db.Images.Add(new ImageRecord
            {
                Id = id,
                UserId = userId,
                SourceUrl = null,
                PendingFilePath = fileName,
                OriginalFileName = file.FileName,
                CreatedAt = DateTimeOffset.UtcNow,
                StoredSizeBytes = newSize,
            });

            await _db.SaveChangesAsync(cancellationToken);

            _backgroundJobs.Enqueue<ImagePipelineJob>(j => j.ProcessAsync(id));
            batchBytes += newSize;
            enqueued++;
        }

        if (enqueued > 0)
        {
            try
            {
                var userEntity = await _users.GetUserAsync(User);
                var notifyEmail = userEntity?.Email ?? userId;
                await _telegram.NotifyUploadBatchAsync(notifyEmail, enqueued, batchBytes, cancellationToken);
            }
            catch (Exception ex)
            {
                // Ảnh đã lưu + job đã enqueue; không được làm hỏng JSON trả về cho client.
                _log.LogWarning(ex, "Notify sau khi upload: bỏ qua, phản hồi JSON vẫn trả về bình thường.");
            }
        }

        if (enqueued == 0)
        {
            foreach (var e in errors)
                ModelState.AddModelError(string.Empty, e);
            if (wantsJson)
                return BadRequest(new { ok = false, errors = errors.ToArray() });
            return View();
        }

        string flash;
        if (errors.Count > 0)
            flash = enqueued == 1
                ? $"Đã nhận 1 ảnh để xử lý. {errors.Count} file bị bỏ qua."
                : $"Đã nhận {enqueued} ảnh để xử lý. {errors.Count} file bị bỏ qua.";
        else
            flash = enqueued == 1
                ? "Đã thêm ảnh vào album (đang tải lên trong nền)."
                : $"Đã thêm {enqueued} ảnh vào album (đang tải lên trong nền).";

        if (wantsJson)
            return Json(new { ok = true, enqueued, skipped = errors.Count, message = flash });

        TempData["Message"] = flash;
        return RedirectToAction(nameof(Index));
    }

    private static string DisplayName(IFormFile file)
    {
        var n = Path.GetFileName(file.FileName);
        return string.IsNullOrEmpty(n) ? "(không tên)" : n;
    }
}
