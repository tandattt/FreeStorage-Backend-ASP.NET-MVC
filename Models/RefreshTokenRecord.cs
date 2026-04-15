namespace ImageUploadApp.Models;

public sealed class RefreshTokenRecord
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = "";

    public string TokenHash { get; set; } = "";

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
