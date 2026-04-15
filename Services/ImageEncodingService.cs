using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ImageUploadApp.Services;

public class ImageEncodingService : IImageEncodingService
{
    public async Task<byte[]> ToJpegAsync(Stream input, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync(input, cancellationToken);
        image.Mutate(x => x.AutoOrient());

        await using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 88 }, cancellationToken);
        return ms.ToArray();
    }
}
