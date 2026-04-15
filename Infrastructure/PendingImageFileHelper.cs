using Microsoft.AspNetCore.Hosting;

namespace ImageUploadApp.Infrastructure;

/// <summary>Xóa file JPEG tạm trong App_Data/pending khi xóa bản ghi ảnh.</summary>
public static class PendingImageFileHelper
{
    public static void TryDeleteForImage(string? pendingFilePathOrName, IWebHostEnvironment env, ILogger? log = null)
    {
        if (string.IsNullOrEmpty(pendingFilePathOrName))
            return;

        var name = Path.GetFileName(pendingFilePathOrName);
        if (name.Length != 36 || !name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            return;

        var full = Path.Combine(env.ContentRootPath, "App_Data", "pending", name);
        try
        {
            if (File.Exists(full))
                File.Delete(full);
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "Không xóa được file pending {Path}", full);
        }
    }
}
