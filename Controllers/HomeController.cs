using System.Diagnostics;
using System.Text.Json;
using ImageUploadApp.Infrastructure;
using ImageUploadApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace ImageUploadApp.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        this.SetSeo(
            "Trang chủ",
            "Lưu trữ ảnh cá nhân riêng tư: album chỉ bạn xem, tối ưu màn hình, tải về bất cứ lúc nào.",
            null,
            "/");

        var origin = this.GetPublicBaseUrl().TrimEnd('/');
        ViewData["JsonLd"] = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebSite",
            ["name"] = "Storage Free",
            ["url"] = $"{origin}/",
            ["description"] = "Dịch vụ album ảnh riêng tư — đăng ký để lưu và quản lý ảnh của bạn an toàn."
        });

        return View();
    }

    public IActionResult Privacy()
    {
        this.SetSeo(
            "Chính sách quyền riêng tư",
            "Chính sách quyền riêng tư của Storage Free: cách thu thập và sử dụng dữ liệu khi bạn dùng dịch vụ lưu ảnh.",
            null,
            "/privacy");
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        this.SetSeo(
            "Có lỗi xảy ra",
            "Trang thông báo lỗi Storage Free.",
            "noindex, nofollow",
            null);
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
