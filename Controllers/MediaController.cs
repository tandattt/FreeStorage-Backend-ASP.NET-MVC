using ImageUploadApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImageUploadApp.Controllers;

[Authorize]
[Route("media")]
public class MediaController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<MediaController> _log;

    public MediaController(
        ApplicationDbContext db,
        UserManager<IdentityUser> users,
        IHttpClientFactory httpFactory,
        ILogger<MediaController> log)
    {
        _db = db;
        _users = users;
        _httpFactory = httpFactory;
        _log = log;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] bool download = false, CancellationToken cancellationToken = default)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var row = await _db.Images.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (row is null)
            return NotFound();

        if (string.IsNullOrEmpty(row.SourceUrl))
            return NotFound();

        var client = _httpFactory.CreateClient("media-proxy");
        using var upstream = await client.GetAsync(row.SourceUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!upstream.IsSuccessStatusCode)
        {
            _log.LogWarning("Upstream {Status} for image {Id}", upstream.StatusCode, id);
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        var contentType = upstream.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        var bytes = await upstream.Content.ReadAsByteArrayAsync(cancellationToken);

        if (download)
        {
            var name = string.IsNullOrWhiteSpace(row.OriginalFileName)
                ? $"{id}.jpg"
                : Path.ChangeExtension(Path.GetFileName(row.OriginalFileName), ".jpg");
            return File(bytes, contentType, name);
        }

        return File(bytes, contentType);
    }
}
