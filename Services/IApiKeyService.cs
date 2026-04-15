namespace ImageUploadApp.Services;

public interface IApiKeyService
{
    Task<IReadOnlyList<UserApiKeyListItem>> ListAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Tạo key mới. <paramref name="plaintextKey"/> chỉ trả một lần (định dạng clientId.secret).</summary>
    Task<(bool ok, string? error, string? plaintextKey, UserApiKeyListItem? row)> CreateAsync(
        string userId,
        string name,
        CancellationToken cancellationToken = default);

    Task<(bool ok, string? error)> RevokeAsync(string userId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Xác thực chuỗi đầy đủ (clientId.secret), trả về UserId nếu hợp lệ.</summary>
    Task<string?> ValidateAndGetUserIdAsync(string fullKey, CancellationToken cancellationToken = default);
}

public sealed record UserApiKeyListItem(Guid Id, string Name, string ClientId, bool IsActive, DateTime CreatedAtUtc);
