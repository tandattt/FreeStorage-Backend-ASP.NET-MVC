using Microsoft.AspNetCore.Mvc;

namespace ImageUploadApp.Controllers;

public sealed class VerifyGoogleController : Controller
{
    [HttpGet("/google297b3fb6c5124652.html")]
    [Produces("text/html")]
    public ContentResult GoogleSiteVerification()
    {
        return Content("google-site-verification: google297b3fb6c5124652.html", "text/html");
    }
}
