using ImageUploadApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ImageUploadApp.Infrastructure;

public static class SeoViewDataExtensions
{
    public static string GetPublicBaseUrl(this Controller controller)
    {
        var site = controller.HttpContext.RequestServices.GetRequiredService<IOptions<SiteOptions>>().Value;
        var configured = (site.PublicBaseUrl ?? "").TrimEnd('/');
        return string.IsNullOrEmpty(configured)
            ? $"{controller.Request.Scheme}://{controller.Request.Host}{controller.Request.PathBase}"
            : configured;
    }

    public static void SetSeo(
        this Controller controller,
        string title,
        string description,
        string? robots = null,
        string? canonicalPathOrFullUrl = null)
    {
        controller.ViewData["Title"] = title;
        controller.ViewData["Description"] = description;
        if (!string.IsNullOrEmpty(robots))
            controller.ViewData["Robots"] = robots;
        else
            controller.ViewData.Remove("Robots");

        var site = controller.HttpContext.RequestServices.GetRequiredService<IOptions<SiteOptions>>().Value;
        var configuredBase = (site.PublicBaseUrl ?? "").TrimEnd('/');

        string canonical;
        if (!string.IsNullOrWhiteSpace(canonicalPathOrFullUrl))
        {
            var c = canonicalPathOrFullUrl.Trim();
            if (c.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || c.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                canonical = c;
            }
            else
            {
                var b = string.IsNullOrEmpty(configuredBase)
                    ? $"{controller.Request.Scheme}://{controller.Request.Host}{controller.Request.PathBase}"
                    : configuredBase;
                if (!c.StartsWith('/'))
                    c = "/" + c;
                canonical = b + c;
            }
        }
        else
        {
            var b = string.IsNullOrEmpty(configuredBase)
                ? $"{controller.Request.Scheme}://{controller.Request.Host}{controller.Request.PathBase}"
                : configuredBase;
            canonical = b + controller.Request.Path + controller.Request.QueryString;
        }

        controller.ViewData["Canonical"] = canonical;
    }
}
