using System.Security.Cryptography;
using System.Text;
using ImageUploadApp.Data;
using ImageUploadApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ImageUploadApp.Services;

public sealed class ApiKeyService : IApiKeyService
{
    private readonly ApplicationDbContext _db;
    private readonly string _pepper;

    public ApiKeyService(ApplicationDbContext db, IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _pepper = jwtOptions.Value.SecretKey ?? "";
        if (_pepper.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be configured with at least 32 characters.");
    }

    public async Task<IReadOnlyList<UserApiKeyListItem>> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.UserApiKeys.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new UserApiKeyListItem(
                x.Id,
                x.Name,
                x.ClientId,
                x.RevokedAtUtc == null,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return rows;
    }

    public async Task<(bool ok, string? error, string? plaintextKey, UserApiKeyListItem? row)> CreateAsync(
        string userId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length is < 1 or > 120)
            return (false, "Tên từ 1 đến 120 ký tự.", null, null);

        var clientId = await GenerateUniqueClientIdAsync(cancellationToken);
        var secret = GenerateSecret();
        var hash = HashSecret(clientId, secret);

        var entity = new UserApiKeyRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = trimmed,
            ClientId = clientId,
            SecretHash = hash,
            CreatedAtUtc = DateTime.UtcNow,
            RevokedAtUtc = null,
        };

        _db.UserApiKeys.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var plaintext = $"{clientId}.{secret}";
        var row = new UserApiKeyListItem(entity.Id, entity.Name, entity.ClientId, true, entity.CreatedAtUtc);
        return (true, null, plaintext, row);
    }

    public async Task<(bool ok, string? error)> RevokeAsync(string userId, Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _db.UserApiKeys.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (row is null)
            return (false, "Không tìm thấy khóa.");
        if (row.RevokedAtUtc != null)
            return (false, "Khóa đã bị thu hồi.");

        row.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<string?> ValidateAndGetUserIdAsync(string fullKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullKey))
            return null;

        var trimmed = fullKey.Trim();
        var dot = trimmed.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0 || dot >= trimmed.Length - 1)
            return null;

        var clientId = trimmed[..dot];
        var secret = trimmed[(dot + 1)..];
        if (clientId.Length > 64 || secret.Length > 256)
            return null;

        var row = await _db.UserApiKeys.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ClientId == clientId, cancellationToken);
        if (row is null || row.RevokedAtUtc != null)
            return null;

        var expected = row.SecretHash;
        var actual = HashSecret(clientId, secret);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(actual)))
            return null;

        return row.UserId;
    }

    private async Task<string> GenerateUniqueClientIdAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < 32; i++)
        {
            var id = GenerateClientId();
            var exists = await _db.UserApiKeys.AsNoTracking().AnyAsync(x => x.ClientId == id, cancellationToken);
            if (!exists)
                return id;
        }

        throw new InvalidOperationException("Could not allocate unique ClientId.");
    }

    private static string GenerateClientId()
    {
        Span<byte> b = stackalloc byte[10];
        RandomNumberGenerator.Fill(b);
        return Convert.ToHexString(b).ToLowerInvariant();
    }

    private static string GenerateSecret()
    {
        Span<byte> b = stackalloc byte[32];
        RandomNumberGenerator.Fill(b);
        return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private string HashSecret(string clientId, string secret)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(_pepper));
        var bytes = h.ComputeHash(Encoding.UTF8.GetBytes(clientId + ":" + secret));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
