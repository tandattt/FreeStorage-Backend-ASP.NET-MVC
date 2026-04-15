using Hangfire;
using ImageUploadApp.Data;
using ImageUploadApp.Models;
using ImageUploadApp.Services.Jobs;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;

namespace ImageUploadApp.Services;

public interface IPhotoBatchUploadService
{
    Task<PhotoBatchUploadResult> UploadAsync(
        string userId,
        string notifyEmail,
        IReadOnlyList<IFormFile> files,
        Guid? folderId,
        CancellationToken cancellationToken = default);
}

public sealed class PhotoBatchUploadResult
{
    public int Enqueued { get; init; }

    public int Skipped { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public long BatchBytes { get; init; }

    public Guid? EffectiveFolderId { get; init; }

    public string Message { get; init; } = "";
}

public sealed class PhotoBatchUploadService : IPhotoBatchUploadService
{
    public const int MaxFilesPerRequest = 150;

    private readonly ApplicationDbContext _db;
    private readonly IPhotoFolderService _folders;
    private readonly IImageEncodingService _imageEncoding;
    private readonly IWebHostEnvironment _env;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ITelegramNotifyService _telegram;
    private readonly ILogger<PhotoBatchUploadService> _log;

    public PhotoBatchUploadService(
        ApplicationDbContext db,
        IPhotoFolderService folders,
        IImageEncodingService imageEncoding,
        IWebHostEnvironment env,
        IBackgroundJobClient backgroundJobs,
        ITelegramNotifyService telegram,
        ILogger<PhotoBatchUploadService> log)
    {
        _db = db;
        _folders = folders;
        _imageEncoding = imageEncoding;
        _env = env;
        _backgroundJobs = backgroundJobs;
        _telegram = telegram;
        _log = log;
    }

    public async Task<PhotoBatchUploadResult> UploadAsync(
        string userId,
        string notifyEmail,
        IReadOnlyList<IFormFile> files,
        Guid? folderId,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var enqueued = 0;
        long batchBytes = 0;

        Guid? targetFolderId = folderId;
        if (targetFolderId.HasValue)
        {
            var owned = await _folders.GetOwnedFolderAsync(userId, targetFolderId.Value, cancellationToken);
            if (owned is null)
                targetFolderId = null;
        }

        var pendingDir = Path.Combine(_env.ContentRootPath, "App_Data", "pending");
        Directory.CreateDirectory(pendingDir);

        var usedBytes = await _db.Images
            .Where(x => x.UserId == userId)
            .SumAsync(x => x.StoredSizeBytes ?? 0L, cancellationToken);

        const long maxImageBytes = 120 * 1024 * 1024;

        foreach (var file in files)
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
            await File.WriteAllBytesAsync(fullPath, jpeg, cancellationToken);

            _db.Images.Add(new ImageRecord
            {
                Id = id,
                UserId = userId,
                FolderId = targetFolderId,
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
                await _telegram.NotifyUploadBatchAsync(notifyEmail, enqueued, batchBytes, cancellationToken);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Notify sau khi upload API/batch: bỏ qua.");
            }
        }

        string message;
        if (enqueued == 0)
            message = errors.Count > 0 ? string.Join(" ", errors) : "Không có file hợp lệ.";
        else if (errors.Count > 0)
            message = enqueued == 1
                ? $"Đã nhận 1 ảnh để xử lý. {errors.Count} file bị bỏ qua."
                : $"Đã nhận {enqueued} ảnh để xử lý. {errors.Count} file bị bỏ qua.";
        else
            message = enqueued == 1
                ? "Đã thêm ảnh vào album (đang xử lý nền)."
                : $"Đã thêm {enqueued} ảnh vào album (đang xử lý nền).";

        return new PhotoBatchUploadResult
        {
            Enqueued = enqueued,
            Skipped = errors.Count,
            Errors = errors,
            BatchBytes = batchBytes,
            EffectiveFolderId = targetFolderId,
            Message = message,
        };
    }

    private static string DisplayName(IFormFile file)
    {
        var n = Path.GetFileName(file.FileName);
        return string.IsNullOrEmpty(n) ? "(không tên)" : n;
    }
}
