using Hangfire;
using ImageUploadApp.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace ImageUploadApp.Services.Jobs;

/// <summary>
/// Định kỳ quét <c>App_Data/pending</c>: với mỗi file JPEG hợp lệ còn bản ghi ảnh chưa có SourceUrl,
/// enqueue <see cref="ImagePipelineJob.ProcessAsync"/> để upload CDN, cập nhật DB và xóa file tạm (cùng luồng xử lý như lúc upload).
/// </summary>
public class PendingFolderReconcileJob
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<PendingFolderReconcileJob> _log;

    public PendingFolderReconcileJob(
        ApplicationDbContext db,
        IWebHostEnvironment env,
        IBackgroundJobClient backgroundJobs,
        ILogger<PendingFolderReconcileJob> log)
    {
        _db = db;
        _env = env;
        _backgroundJobs = backgroundJobs;
        _log = log;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var pendingDir = Path.Combine(_env.ContentRootPath, "App_Data", "pending");
        if (!Directory.Exists(pendingDir))
            return;

        var enqueued = 0;
        var seen = new HashSet<Guid>();

        foreach (var fullPath in Directory.EnumerateFiles(pendingDir, "*.jpg", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = Path.GetFileName(fullPath);
            if (!TryParsePendingFileName(name, out var imageId))
                continue;

            if (!seen.Add(imageId))
                continue;

            var image = await _db.Images.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == imageId, cancellationToken);

            if (image is null)
            {
                _log.LogDebug("Reconcile: file {File} không khớp bản ghi Images — bỏ qua", name);
                continue;
            }

            if (!string.IsNullOrEmpty(image.SourceUrl))
                continue;

            if (image.PipelineFailed)
                continue;

            if (string.IsNullOrEmpty(image.PendingFilePath))
                continue;

            var expected = Path.GetFileName(image.PendingFilePath);
            if (!string.Equals(name, expected, StringComparison.OrdinalIgnoreCase))
                continue;

            _backgroundJobs.Enqueue<ImagePipelineJob>(j => j.ProcessAsync(imageId));
            enqueued++;
        }

        if (enqueued > 0)
            _log.LogInformation("Reconcile pending: đã enqueue {Count} job xử lý upload", enqueued);
    }

    /// <summary>Khớp quy ước lưu file: <c>{guid:N}.jpg</c> (36 ký tự).</summary>
    private static bool TryParsePendingFileName(string fileName, out Guid imageId)
    {
        imageId = default;
        if (string.IsNullOrEmpty(fileName) || fileName.Length != 36)
            return false;
        if (!fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            return false;
        return Guid.TryParseExact(fileName.AsSpan(0, 32), "N", out imageId);
    }
}
