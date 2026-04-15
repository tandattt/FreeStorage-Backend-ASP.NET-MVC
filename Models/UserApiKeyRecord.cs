namespace ImageUploadApp.Models;

public sealed class UserApiKeyRecord
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = "";

    /// <summary>Tên hiển thị do người dùng đặt.</summary>
    public string Name { get; set; } = "";

    /// <summary>Định danh công khai (Client ID), dùng tra cứu khi xác thực.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>Hash phần bí mật (không lưu plaintext).</summary>
    public string SecretHash { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
}
