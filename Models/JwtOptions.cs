namespace ImageUploadApp.Models;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = "";

    public string Issuer { get; set; } = "ImageUploadApp";

    public string Audience { get; set; } = "ImageUploadApp";

    public int AccessTokenMinutes { get; set; } = 60;

    public int RefreshTokenDays { get; set; } = 14;
}
