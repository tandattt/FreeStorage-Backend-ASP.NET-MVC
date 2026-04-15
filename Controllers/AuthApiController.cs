using ImageUploadApp.Infrastructure;
using ImageUploadApp.Models;
using ImageUploadApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ImageUploadApp.Controllers;

[ApiController]
[Route("api/auth")]
[IgnoreAntiforgeryToken]
public sealed class AuthApiController : ControllerBase
{
    private readonly UserManager<IdentityUser> _users;
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly JwtTokenService _jwt;
    private readonly IWebHostEnvironment _env;
    private readonly JwtOptions _jwtOpt;

    public AuthApiController(
        UserManager<IdentityUser> users,
        SignInManager<IdentityUser> signIn,
        JwtTokenService jwt,
        IWebHostEnvironment env,
        IOptions<JwtOptions> jwtOpt)
    {
        _users = users;
        _signIn = signIn;
        _jwt = jwt;
        _env = env;
        _jwtOpt = jwtOpt.Value;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AuthLoginApiRequest body, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var user = await _users.FindByEmailAsync(body.Email);
        if (user is null)
            return Unauthorized();

        var check = await _signIn.CheckPasswordSignInAsync(user, body.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
            return Unauthorized();

        var roles = await _users.GetRolesAsync(user);
        var access = _jwt.CreateAccessToken(user, roles);
        var (rawRefresh, _) = await _jwt.CreateRefreshTokenAsync(user.Id, cancellationToken);
        AuthCookies.AppendPair(Response.Cookies, _env, _jwtOpt, access, rawRefresh);
        return Ok(new { accessToken = access, expiresInMinutes = _jwtOpt.AccessTokenMinutes });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var raw) || string.IsNullOrEmpty(raw))
            return Unauthorized();

        var rotated = await _jwt.RotateRefreshAsync(raw, _users, cancellationToken);
        if (!rotated.HasValue)
            return Unauthorized();

        var v = rotated.Value;
        AuthCookies.AppendPair(Response.Cookies, _env, _jwtOpt, v.AccessToken, v.RawRefresh);
        return Ok(new { accessToken = v.AccessToken, expiresInMinutes = _jwtOpt.AccessTokenMinutes });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var raw) && !string.IsNullOrEmpty(raw))
            await _jwt.RevokeRefreshAsync(raw, cancellationToken);
        AuthCookies.ClearAuthCookies(Response.Cookies, _env);
        return Ok();
    }
}
