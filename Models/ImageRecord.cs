using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImageUploadApp.Models;

public class ImageRecord
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    /// <summary>Upstream URL sau khi job xong; null khi đang xử lý.</summary>
    [MaxLength(2048)]
    public string? SourceUrl { get; set; }

    /// <summary>true khi job Hangfire đã vào trạng thái Failed (hết retry); FE không poll nữa.</summary>
    public bool PipelineFailed { get; set; }

    /// <summary>Đường dẫn file JPEG tạm trên server (chỉ dùng nội bộ, tới khi upload xong).</summary>
    [MaxLength(1024)]
    public string? PendingFilePath { get; set; }

    [MaxLength(128)]
    public string? ExternalFileId { get; set; }

    [MaxLength(260)]
    public string? OriginalFileName { get; set; }

    /// <summary>Kích thước file JPEG đã lưu (byte), dùng cho hạn mức tài khoản.</summary>
    public long? StoredSizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual Microsoft.AspNetCore.Identity.IdentityUser? User { get; set; }
}
