namespace ImageUploadApp.Services;

public interface IImageEncodingService
{
    /// <summary>Decode common image formats and encode as JPEG.</summary>
    Task<byte[]> ToJpegAsync(Stream input, CancellationToken cancellationToken = default);
}
