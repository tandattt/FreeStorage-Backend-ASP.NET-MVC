using Microsoft.AspNetCore.Mvc;

namespace ImageUploadApp.Controllers;

public sealed class VerifyGoogleController : Controller
{
    [HttpGet("/google297b3fb6c5124652.html")]
    [Produces("text/html")]
    public PhysicalFileResult GoogleSiteVerification()
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Views", "Verify_Google", "google297b3fb6c5124652.html");
        return PhysicalFile(filePath, "text/html; charset=utf-8");
    }
}
