using System.IdentityModel.Tokens.Jwt;
using ImageUploadApp.Models;
using ImageUploadApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;

namespace ImageUploadApp.Infrastructure;

public sealed class JwtRenewalMiddleware
{
    private readonly RequestDelegate _next;

    public JwtRenewalMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        JwtTokenService jwt,
        UserManager<IdentityUser> users,
        IWebHostEnvironment env,
        IOptions<JwtOptions> jwtOptions)
    {
        var opt = jwtOptions.Value;
        var access = context.Request.Cookies[AuthCookies.AccessToken];
        var accessValid = false;
        if (!string.IsNullOrEmpty(access))
        {
            try
            {
                var t = new JwtSecurityTokenHandler().ReadJwtToken(access);
                accessValid = t.ValidTo > DateTime.UtcNow.AddSeconds(45);
            }
            catch
            {
                accessValid = false;
            }
        }

        if (!accessValid
            && context.Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var rawRefresh)
            && !string.IsNullOrEmpty(rawRefresh))
        {
            var rotated = await jwt.RotateRefreshAsync(rawRefresh, users, context.RequestAborted);
            if (rotated.HasValue)
            {
                var v = rotated.Value;
                AuthCookies.AppendPair(context.Response.Cookies, env, opt, v.AccessToken, v.RawRefresh);
                context.Request.Headers.Append("Authorization", $"Bearer {v.AccessToken}");
            }
        }

        await _next(context);
    }
}
