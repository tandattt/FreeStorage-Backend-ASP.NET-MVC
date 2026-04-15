using Microsoft.AspNetCore.Mvc;

namespace ImageUploadApp.Controllers;

public class PricingController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
