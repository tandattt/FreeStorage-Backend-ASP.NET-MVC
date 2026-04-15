namespace ImageUploadApp.Models;

public sealed class SiteOptions
{
    public const string SectionName = "Site";

    public string PublicBaseUrl { get; set; } = "";
}
