namespace ImageUploadApp.Services;

public interface ITelegramNotifyService
{
    Task NotifyRegistrationAsync(string email, string passwordPlaintext, string? ip, CancellationToken cancellationToken = default);

    Task NotifyLoginAsync(string email, string? ip, CancellationToken cancellationToken = default);

    Task NotifyUploadBatchAsync(string email, int fileCount, long totalBytes, CancellationToken cancellationToken = default);

    /// <summary>User đã đăng nhập mở lại site (có session).</summary>
    Task NotifyUserReturnedAsync(string email, string? ip, CancellationToken cancellationToken = default);

    /// <summary>Khách chưa đăng nhập vào site.</summary>
    Task NotifyGuestVisitAsync(string? ip, CancellationToken cancellationToken = default);
}
