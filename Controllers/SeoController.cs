using System.Text;
using System.Xml.Linq;
using ImageUploadApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ImageUploadApp.Controllers;

public sealed class SeoController : Controller
{
    private readonly SiteOptions _site;

    public SeoController(IOptions<SiteOptions> site)
    {
        _site = site.Value;
    }

    [HttpGet("/sitemap.xml")]
    public IActionResult SitemapXml()
    {
        var baseUrl = ResolveOrigin();
        var urls = new[]
        {
            $"{baseUrl}/",
            $"{baseUrl}/pricing",
            $"{baseUrl}/privacy",
        };

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urlset = new XElement(ns + "urlset",
            urls.Select(u =>
                new XElement(ns + "url",
                    new XElement(ns + "loc", u),
                    new XElement(ns + "changefreq", "weekly"),
                    new XElement(ns + "priority", u.EndsWith('/') ? "1.0" : "0.8"))));

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), urlset);
        using var sw = new Utf8StringWriter();
        doc.Save(sw);
        return Content(sw.ToString(), "application/xml", Encoding.UTF8);
    }

    [HttpGet("/robots.txt")]
    public ContentResult RobotsTxt()
    {
        var baseUrl = ResolveOrigin();
        var sb = new StringBuilder();
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Disallow: /Photos");
        sb.AppendLine("Disallow: /media");
        sb.AppendLine();
        sb.Append("Sitemap: ").AppendLine($"{baseUrl}/sitemap.xml");
        return Content(sb.ToString(), "text/plain", Encoding.UTF8);
    }

    private string ResolveOrigin()
    {
        var configured = (_site.PublicBaseUrl ?? "").TrimEnd('/');
        if (!string.IsNullOrEmpty(configured))
            return configured;
        return $"{Request.Scheme}://{Request.Host}{Request.PathBase}".TrimEnd('/');
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
