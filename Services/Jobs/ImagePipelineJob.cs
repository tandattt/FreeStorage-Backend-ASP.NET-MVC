using Hangfire;
using ImageUploadApp.Data;
using ImageUploadApp.Models;
using ImageUploadApp.Services;
using Microsoft.EntityFrameworkCore;

namespace ImageUploadApp.Services.Jobs;

public class ImagePipelineJob
{
    private readonly ApplicationDbContext _db;
    private readonly IWebtrethoUploadService _webtretho;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ImagePipelineJob> _log;

    public ImagePipelineJob(
        ApplicationDbContext db,
        IWebtrethoUploadService webtretho,
        IWebHostEnvironment env,
        ILogger<ImagePipelineJob> log)
    {
        _db = db;
        _webtretho = webtretho;
        _env = env;
        _log = log;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 60, 60 })]
    public async Task ProcessAsync(Guid imageId)
    {
        var image = await _db.Images.FirstOrDefaultAsync(x => x.Id == imageId);
        if (image is null)
        {
            _log.LogWarning("ImagePipelineJob: record {Id} not found — skip", imageId);
            return;
        }

        if (!string.IsNullOrEmpty(image.SourceUrl))
            return;

        var path = ResolvePendingPath(image);
        if (string.IsNullOrEmpty(path))
        {
            _log.LogError(
                "ImagePipelineJob: không resolve được đường dẫn pending (coi là lỗi, job sẽ retry). Id={Id}, PendingFilePath={Pending}",
                imageId, image.PendingFilePath ?? "(null)");
            throw new InvalidOperationException(
                "Không resolve được đường dẫn file pending — cần kiểm tra PendingFilePath / App_Data/pending.");
        }

        if (!System.IO.File.Exists(path))
        {
            _log.LogWarning(
                "ImagePipelineJob: chưa thấy file trên đĩa (fail + retry). Id={Id}, Path={Path}",
                imageId, path);
            throw new InvalidOperationException(
                "Chưa thấy file JPEG pending trên đĩa — có thể volume chưa sync; Hangfire sẽ chạy lại.");
        }

        // Đọc xong phải đóng stream trước khi Upload/Delete — nếu không Windows giữ lock, File.Delete thất bại.
        byte[] jpeg;
        await using (var fs = System.IO.File.OpenRead(path))
        {
            using var ms = new MemoryStream();
            await fs.CopyToAsync(ms);
            jpeg = ms.ToArray();
        }

        var safeName = Path.GetFileNameWithoutExtension(image.OriginalFileName ?? "upload");
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "upload";
        var uploadFileName = $"{safeName}_{Guid.NewGuid().ToString("N")[..8]}.jpg";

        var uploadResult = await _webtretho.UploadJpegAsync(jpeg, uploadFileName);
        if (uploadResult is null)
        {
            _log.LogWarning("ImagePipelineJob: Webtretho failed for {Id}", imageId);
            throw new InvalidOperationException("Remote upload failed; retry scheduled.");
        }

        image.SourceUrl = uploadResult.CdnUrl;
        image.ExternalFileId = uploadResult.ExternalFileId;
        image.PendingFilePath = null;
        await _db.SaveChangesAsync();

        TryDeletePendingFile(path, _log);
    }

    private static void TryDeletePendingFile(string path, ILogger log)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (!System.IO.File.Exists(path))
                    return;
                System.IO.File.Delete(path);
                return;
            }
            catch (IOException ex)
            {
                if (attempt >= 3)
                {
                    log.LogWarning(ex, "Could not delete temp file {Path}", path);
                    return;
                }

                log.LogDebug(ex, "Retry delete pending file attempt {Attempt} {Path}", attempt, path);
                Thread.Sleep(150 * attempt);
            }
            catch (UnauthorizedAccessException ex)
            {
                if (attempt >= 3)
                {
                    log.LogWarning(ex, "Could not delete temp file {Path}", path);
                    return;
                }

                log.LogDebug(ex, "Retry delete pending file attempt {Attempt} {Path}", attempt, path);
                Thread.Sleep(150 * attempt);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Could not delete temp file {Path}", path);
                return;
            }
        }
    }

    private string? ResolvePendingPath(ImageRecord image)
    {
        if (string.IsNullOrEmpty(image.PendingFilePath))
            return null;
        var name = Path.GetFileName(image.PendingFilePath);
        if (name.Length != 36 || !name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            return null;
        var dir = Path.Combine(_env.ContentRootPath, "App_Data", "pending");
        return Path.Combine(dir, name);
    }
}
