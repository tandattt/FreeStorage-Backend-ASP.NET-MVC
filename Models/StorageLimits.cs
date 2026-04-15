namespace ImageUploadApp.Models;

public static class StorageLimits
{
    /// <summary>Hạn mức tổng dung lượng đã upload mỗi tài khoản (5 GB).</summary>
    public const long PerUserQuotaBytes = 5L * 1024 * 1024 * 1024;
}
