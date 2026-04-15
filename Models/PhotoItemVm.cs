namespace ImageUploadApp.Models;

public class PhotoItemVm
{
    public Guid Id { get; set; }

    public Guid? FolderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>null khi chưa có ảnh (job đang chạy).</summary>
    public string? ProxyUrl { get; set; }
    public string? DownloadUrl { get; set; }
    /// <summary>Job còn chạy hoặc chờ retry (chưa ready, chưa fail hẳn).</summary>
    public bool IsPending { get; set; }
    /// <summary>Job Hangfire đã Failed sau khi hết retry.</summary>
    public bool IsFailed { get; set; }
}
