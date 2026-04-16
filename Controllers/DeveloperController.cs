using System.Security.Claims;
using ImageUploadApp.Infrastructure;
using ImageUploadApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImageUploadApp.Controllers;

[Authorize]
public class DeveloperController : Controller
{
    private readonly IApiKeyService _apiKeys;

    public DeveloperController(IApiKeyService apiKeys) => _apiKeys = apiKeys;

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AlbumApiDocs()
    {
        this.SetSeo(
            "API lưu ảnh miễn phí trên cloud cho developer",
            "Tài liệu REST API lưu ảnh miễn phí: quản lý thư mục, upload ảnh online, kiểm tra dung lượng cloud storage free.",
            "index, follow",
            "/developer/album-api");
        ViewData["PublicBaseUrl"] = this.GetPublicBaseUrl();
        ViewData["DeveloperNav"] = "docs";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ApiKeys(CancellationToken cancellationToken)
    {
        this.SetSeo(
            "Khóa API (developer)",
            "Tạo và quản lý API key để gọi REST API album.",
            "noindex, nofollow",
            "/developer/api-keys");
        ViewData["PublicBaseUrl"] = this.GetPublicBaseUrl();
        ViewData["DeveloperNav"] = "keys";

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(uid))
            return Challenge();

        var list = await _apiKeys.ListAsync(uid, cancellationToken);
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateApiKey([FromForm] string name, CancellationToken cancellationToken)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(uid))
            return Challenge();

        var (ok, error, plaintext, _) = await _apiKeys.CreateAsync(uid, name ?? "", cancellationToken);
        if (!ok)
        {
            TempData["ApiKeyError"] = error ?? "Không tạo được khóa.";
            return RedirectToAction(nameof(ApiKeys));
        }

        TempData["NewApiKey"] = plaintext;
        return RedirectToAction(nameof(ApiKeys));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeApiKey(Guid id, CancellationToken cancellationToken)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(uid))
            return Challenge();

        var (ok, error) = await _apiKeys.RevokeAsync(uid, id, cancellationToken);
        TempData[ok ? "ApiKeyInfo" : "ApiKeyError"] = ok ? "Đã thu hồi khóa." : (error ?? "Lỗi.");
        return RedirectToAction(nameof(ApiKeys));
    }
}
