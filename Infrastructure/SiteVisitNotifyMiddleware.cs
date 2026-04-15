using System.Security.Claims;
using ImageUploadApp.Services;

namespace ImageUploadApp.Infrastructure;

/// <summary>
/// Mỗi phiên trình duyệt: gửi Telegram một lần cho khách và một lần cho user đã đăng nhập (khi họ xuất hiện).
/// </summary>
public sealed class SiteVisitNotifyMiddleware
{
    private const string CookieGuest = "tg_vis_guest";
    private const string CookieUser = "tg_vis_user";

    private static readonly HashSet<string> StaticExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".js", ".map", ".ico", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg",
        ".woff", ".woff2", ".ttf", ".eot", ".json",
    };

    private readonly RequestDelegate _next;

    public SiteVisitNotifyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
    {
        if (!HttpMethods.IsGet(context.Request.Method) || ShouldSkipPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var isAuth = context.User.Identity?.IsAuthenticated == true;

        string? userEmail = null;
        var notifyUser = isAuth
                         && !context.Request.Cookies.ContainsKey(CookieUser);
        if (notifyUser)
        {
            userEmail = context.User.FindFirst(ClaimTypes.Email)?.Value
                ?? context.User.FindFirst(ClaimTypes.Name)?.Value
                ?? context.User.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail))
                notifyUser = false;
        }

        var notifyGuest = !isAuth && !context.Request.Cookies.ContainsKey(CookieGuest);

        if (!notifyUser && !notifyGuest)
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString();
        var ct = context.RequestAborted;

        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode != StatusCodes.Status200OK)
                return Task.CompletedTask;

            var opts = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",
            };

            if (notifyUser && userEmail != null)
            {
                context.Response.Cookies.Append(CookieUser, "1", opts);
                QueueTelegram(scopeFactory, t => t.NotifyUserReturnedAsync(userEmail!, ip, ct), ct);
            }
            else if (notifyGuest)
            {
                context.Response.Cookies.Append(CookieGuest, "1", opts);
                QueueTelegram(scopeFactory, t => t.NotifyGuestVisitAsync(ip, ct), ct);
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static void QueueTelegram(
        IServiceScopeFactory scopeFactory,
        Func<ITelegramNotifyService, Task> work,
        CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var telegram = scope.ServiceProvider.GetRequiredService<ITelegramNotifyService>();
            try
            {
                await work(telegram);
            }
            catch
            {
                /* giống các chỗ gửi Telegram khác — không làm hỏng response */
            }
        }, cancellationToken);
    }

    private static bool ShouldSkipPath(PathString path)
    {
        var v = path.Value;
        if (string.IsNullOrEmpty(v))
            return false;

        if (v.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase))
            return true;

        if (v.StartsWith("/media/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (v.StartsWith("/Photos/Status", StringComparison.OrdinalIgnoreCase))
            return true;

        if (v.StartsWith("/Photos/Sync", StringComparison.OrdinalIgnoreCase))
            return true;

        var ext = Path.GetExtension(v);
        if (string.IsNullOrEmpty(ext))
            return false;

        return StaticExtensions.Contains(ext);
    }
}

public static class SiteVisitNotifyMiddlewareExtensions
{
    public static IApplicationBuilder UseSiteVisitTelegramNotify(this IApplicationBuilder app) =>
        app.UseMiddleware<SiteVisitNotifyMiddleware>();
}
