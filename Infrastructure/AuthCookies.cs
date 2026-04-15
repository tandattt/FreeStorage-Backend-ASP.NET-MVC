using ImageUploadApp.Models;
using Microsoft.AspNetCore.Hosting;

namespace ImageUploadApp.Infrastructure;

public static class AuthCookies
{
    public const string AccessToken = "access_token";
    public const string RefreshToken = "refresh_token";

    public static CookieOptions AccessOptions(TimeSpan lifetime, IWebHostEnvironment env) => new()
    {
        HttpOnly = true,
        Secure = !env.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        MaxAge = lifetime,
        Path = "/"
    };

    public static CookieOptions RefreshOptions(TimeSpan lifetime, IWebHostEnvironment env) => new()
    {
        HttpOnly = true,
        Secure = !env.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        MaxAge = lifetime,
        Path = "/"
    };

    public static void AppendPair(IResponseCookies cookies, IWebHostEnvironment env, JwtOptions opt, string access, string refreshRaw)
    {
        cookies.Append(AccessToken, access, AccessOptions(TimeSpan.FromMinutes(opt.AccessTokenMinutes), env));
        cookies.Append(RefreshToken, refreshRaw, RefreshOptions(TimeSpan.FromDays(opt.RefreshTokenDays), env));
    }

    public static void ClearAuthCookies(IResponseCookies cookies, IWebHostEnvironment env)
    {
        cookies.Delete(AccessToken, new CookieOptions { Path = "/", Secure = !env.IsDevelopment(), SameSite = SameSiteMode.Lax });
        cookies.Delete(RefreshToken, new CookieOptions { Path = "/", Secure = !env.IsDevelopment(), SameSite = SameSiteMode.Strict });
    }
}
