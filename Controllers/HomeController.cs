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
            "Cloud lưu trữ ảnh miễn phí, upload ảnh online free",
            "Storage Free là nền tảng cloud lưu trữ free giúp bạn lưu ảnh miễn phí, quản lý album online và tải ảnh mọi lúc trên mọi thiết bị.",
            null,
            "/");

        var origin = this.GetPublicBaseUrl().TrimEnd('/');
        ViewData["JsonLd"] = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["@type"] = "WebSite",
                    ["name"] = "Storage Free",
                    ["url"] = $"{origin}/",
                    ["description"] = "Nền tảng cloud lưu trữ ảnh miễn phí, upload ảnh online free và quản lý album cá nhân."
                },
                new Dictionary<string, object?>
                {
                    ["@type"] = "Service",
                    ["name"] = "Dịch vụ lưu ảnh miễn phí",
                    ["serviceType"] = "Cloud image storage",
                    ["provider"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "Organization",
                        ["name"] = "Storage Free"
                    },
                    ["areaServed"] = "VN",
                    ["url"] = $"{origin}/"
                }
            }
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
