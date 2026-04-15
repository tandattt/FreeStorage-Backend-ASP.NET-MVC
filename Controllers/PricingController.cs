using ImageUploadApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ImageUploadApp.Controllers;

public class PricingController : Controller
{
    public IActionResult Index()
    {
        this.SetSeo(
            "Bảng giá",
            "Gói lưu trữ Storage Free: 5 GB miễn phí và các gói nâng cấp sắp ra mắt.",
            null,
            "/pricing");
        return View();
    }
}
