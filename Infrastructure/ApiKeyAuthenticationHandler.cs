using System.Security.Claims;
using System.Text.Encodings.Web;
using ImageUploadApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ImageUploadApp.Infrastructure;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiKeyService _keys;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService keys)
        : base(options, logger, encoder)
    {
        _keys = keys;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!ApiKeyAuth.TryGetHeader(Request, out var raw) || string.IsNullOrWhiteSpace(raw))
            return AuthenticateResult.NoResult();

        var userId = await _keys.ValidateAndGetUserIdAsync(raw, Context.RequestAborted);
        if (userId is null)
            return AuthenticateResult.Fail("Invalid API key");

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
