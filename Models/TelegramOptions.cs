namespace ImageUploadApp.Models;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>Bot token từ BotFather. Nên đặt qua User Secrets / biến môi trường, không commit token thật.</summary>
    public string BotToken { get; set; } = "";

    /// <summary>Chat ID nhận tin (nhóm hoặc cá nhân).</summary>
    public string ChatId { get; set; } = "";
}
