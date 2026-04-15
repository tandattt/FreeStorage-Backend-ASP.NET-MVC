using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ImageUploadApp.Data;
using ImageUploadApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ImageUploadApp.Services;

public sealed class JwtTokenService
{
    private readonly ApplicationDbContext _db;
    private readonly JwtOptions _opt;
    private readonly JwtSecurityTokenHandler _handler = new();

    public JwtTokenService(ApplicationDbContext db, IOptions<JwtOptions> options)
    {
        _db = db;
        _opt = options.Value;
    }

    public string CreateAccessToken(IdentityUser user, IList<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Email ?? user.UserName ?? user.Id),
        };
        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        foreach (var r in roles)
            claims.Add(new Claim(ClaimTypes.Role, r));

        var token = new JwtSecurityToken(
            _opt.Issuer,
            _opt.Audience,
            claims,
            now,
            now.AddMinutes(_opt.AccessTokenMinutes),
            creds);
        return _handler.WriteToken(token);
    }

    public async Task<(string RawRefresh, DateTimeOffset ExpiresAt)> CreateRefreshTokenAsync(string userId, CancellationToken cancellationToken = default)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var hash = Hash(raw);
        var exp = DateTimeOffset.UtcNow.AddDays(_opt.RefreshTokenDays);
        _db.RefreshTokens.Add(new RefreshTokenRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = exp,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
        return (raw, exp);
    }

    public async Task<(IdentityUser User, string AccessToken, string RawRefresh)?> RotateRefreshAsync(
        string rawRefresh,
        UserManager<IdentityUser> users,
        CancellationToken cancellationToken = default)
    {
        var hash = Hash(rawRefresh);
        var row = await _db.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAt == null, cancellationToken);
        if (row is null || row.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;

        var user = await users.FindByIdAsync(row.UserId);
        if (user is null)
            return null;

        row.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var roles = await users.GetRolesAsync(user);
        var access = CreateAccessToken(user, roles);
        var (newRaw, _) = await CreateRefreshTokenAsync(user.Id, cancellationToken);
        return (user, access, newRaw);
    }

    public async Task RevokeRefreshAsync(string rawRefresh, CancellationToken cancellationToken = default)
    {
        var hash = Hash(rawRefresh);
        var row = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (row is null)
            return;
        row.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
