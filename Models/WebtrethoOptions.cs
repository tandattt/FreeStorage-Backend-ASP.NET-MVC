namespace ImageUploadApp.Models;

public class WebtrethoOptions
{
    public const string SectionName = "Webtretho";

    public string ApiUrl { get; set; } = "https://www.webtretho.vn/api";

    /// <summary>GUID truyền vào mutation uploadFile createdBy.</summary>
    public string CreatedBy { get; set; } = "77b08868-20cf-4cb2-bc8e-fc9672533342";
}
