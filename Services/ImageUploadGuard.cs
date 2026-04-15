using SixLabors.ImageSharp;

namespace ImageUploadApp.Services;

/// <summary>
/// Lọc file trước khi decode: đuôi, magic bytes, kích thước pixel. File không hợp lệ bị loại, không chặn các file ảnh khác.
/// </summary>
public static class ImageUploadGuard
{
    /// <summary>Chiều dài tối đa mỗi cạnh (pixel). Ảnh panorama/texture quá lớn thường làm API từ xa trả 500 hoặc gây hết bộ nhớ khi encode.</summary>
    public const int MaxImageDimensionPixels = 8192;

    /// <summary>Tổng số pixel (rộng × cao) tối đa. Giới hạn bổ sung khi cả hai cạnh đều dưới ngưỡng nhưng diện tích vẫn quá lớn.</summary>
    public const long MaxImageTotalPixels = 50_000_000L;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff", ".heic", ".heif", ".jfif", ".avif",
    };

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".com", ".msi", ".scr", ".ps1", ".sh",
        ".zip", ".rar", ".7z", ".tar", ".gz",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".csv", ".html", ".htm", ".js", ".css", ".php", ".php3", ".php4", ".php5", ".php6", ".php7", ".php8", ".php9", ".php10",
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp", "image/tiff", "image/heic", "image/heif",
        "image/avif", "image/jxl", "image/x-png", "image/pjpeg",
    };

    /// <returns>null nếu qua vòng lọc sơ bộ; ngược lại là thông báo lỗi tiếng Việt.</returns>
    public static string? ValidateMetadata(IFormFile file)
    {
        var name = Path.GetFileName(file.FileName);
        var ext = Path.GetExtension(file.FileName);

        if (BlockedExtensions.Contains(ext))
            return $"{name}: loại file không được phép (không phải ảnh).";

        if (!string.IsNullOrEmpty(ext) && !AllowedExtensions.Contains(ext))
            return $"{name}: đuôi file không nằm trong danh sách ảnh được hỗ trợ.";

        var ct = file.ContentType;
        if (!string.IsNullOrEmpty(ct))
        {
            var okMime = ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                || ct.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
                || AllowedContentTypes.Contains(ct);
            if (!okMime)
                return $"{name}: không phải kiểu MIME ảnh (Content-Type).";
        }

        return null;
    }

    /// <summary>Kiểm tra vài byte đầu có giống file ảnh phổ biến không.</summary>
    public static bool LooksLikeImageHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < 3)
            return false;

        // JPEG
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return true;

        // PNG
        if (header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return true;

        // GIF
        if (header.Length >= 6 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46)
            return true;

        // BMP
        if (header.Length >= 2 && header[0] == 0x42 && header[1] == 0x4D)
            return true;

        // WEBP: RIFF....WEBP
        if (header.Length >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return true;

        // TIFF
        if (header.Length >= 4
            && ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00)
                || (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A)))
            return true;

        // HEIF/HEIC/AVIF: ISO Base Media — 'ftyp' ở offset 4
        if (header.Length >= 12
            && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
            return true;

        return false;
    }

    public static async Task<(bool Ok, string? Error)> ValidateHeaderAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var buffer = new byte[32];
        var read = await stream.ReadAsync(buffer.AsMemory(0, 32), cancellationToken);
        if (read < 3)
            return (false, $"{Path.GetFileName(file.FileName)}: file rỗng hoặc quá nhỏ.");

        if (!LooksLikeImageHeader(buffer.AsSpan(0, read)))
            return (false, $"{Path.GetFileName(file.FileName)}: nội dung không giống file ảnh (magic bytes).");

        return (true, null);
    }

    /// <summary>Đọc metadata (không decode toàn bộ ảnh) để chặn ảnh quá lớn trước khi <see cref="ImageEncodingService.ToJpegAsync"/>.</summary>
    public static async Task<(bool Ok, string? Error)> ValidateDimensionsAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var displayName = Path.GetFileName(file.FileName);
        if (string.IsNullOrEmpty(displayName))
            displayName = "(không tên)";

        await using var stream = file.OpenReadStream();
        ImageInfo? info;
        try
        {
            info = await Image.IdentifyAsync(stream, cancellationToken);
        }
        catch (UnknownImageFormatException)
        {
            return (false, $"{displayName}: không xác định được định dạng ảnh.");
        }
        catch (Exception ex) when (ex is InvalidImageContentException or ImageProcessingException)
        {
            return (false, $"{displayName}: file ảnh không hợp lệ hoặc bị hỏng ({ex.Message}).");
        }

        if (info is null)
            return (false, $"{displayName}: không đọc được kích thước ảnh.");

        var w = info.Width;
        var h = info.Height;
        if (w <= 0 || h <= 0)
            return (false, $"{displayName}: kích thước ảnh không hợp lệ.");

        if (w > MaxImageDimensionPixels || h > MaxImageDimensionPixels)
        {
            return (false,
                $"{displayName}: ảnh {w}×{h} pixel — mỗi cạnh tối đa {MaxImageDimensionPixels} pixel. Hãy thu nhỏ ảnh rồi tải lại.");
        }

        var pixels = (long)w * h;
        if (pixels > MaxImageTotalPixels)
        {
            var mp = pixels / 1_000_000.0;
            var maxMp = MaxImageTotalPixels / 1_000_000.0;
            return (false,
                $"{displayName}: diện tích khoảng {mp:0.#} megapixel, tối đa {maxMp:0.#} MP. Hãy giảm độ phân giải rồi thử lại.");
        }

        return (true, null);
    }
}
