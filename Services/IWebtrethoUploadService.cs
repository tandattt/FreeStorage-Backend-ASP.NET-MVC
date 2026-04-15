namespace ImageUploadApp.Services;

public interface IWebtrethoUploadService
{
    Task<WebtrethoUploadResult?> UploadJpegAsync(byte[] jpegBytes, string fileName, CancellationToken cancellationToken = default);
}

public sealed record WebtrethoUploadResult(string CdnUrl, string? ExternalFileId);
