using System.Security.Claims;
using System.Text;
using System.Threading;
using Hangfire;
using ImageUploadApp.Configurations;
using ImageUploadApp.Data;
using ImageUploadApp.Infrastructure;
using ImageUploadApp.Models;
using ImageUploadApp.Services;
using ImageUploadApp.Services.Jobs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

static bool IsMobileUserAgent(string? userAgent)
{
    if (string.IsNullOrEmpty(userAgent))
        return false;
    return userAgent.Contains("Mobi", StringComparison.OrdinalIgnoreCase)
        || userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)
        || userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
        || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)
        || userAgent.Contains("iPod", StringComparison.OrdinalIgnoreCase);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 524_288_000);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 524_288_000);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtKey = jwtSection["SecretKey"] ?? "";
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:SecretKey must be configured with at least 32 characters.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
    {
        mySqlOptions.CommandTimeout(30);
    });
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.Configure<JwtOptions>(jwtSection);
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IPhotoFolderService, PhotoFolderService>();
builder.Services.AddScoped<IPhotoBatchUploadService, PhotoBatchUploadService>();

builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager();

var jwtIssuer = jwtSection["Issuer"] ?? "ImageUploadApp";
var jwtAudience = jwtSection["Audience"] ?? "ImageUploadApp";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = MultiAuthAuthenticationDefaults.Scheme;
        options.DefaultAuthenticateScheme = MultiAuthAuthenticationDefaults.Scheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name,
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers.Authorization.ToString();
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = authHeader["Bearer ".Length..].Trim();
                    return Task.CompletedTask;
                }
                if (!string.IsNullOrEmpty(context.Request.Cookies[AuthCookies.AccessToken]))
                    context.Token = context.Request.Cookies[AuthCookies.AccessToken];
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                    return Task.CompletedTask;
                context.HandleResponse();
                var returnPath = (context.Request.Path + context.Request.QueryString).ToString();
                var returnUrl = Uri.EscapeDataString(string.IsNullOrEmpty(returnPath) ? "/" : returnPath);
                context.Response.Redirect("/Account/Login?returnUrl=" + returnUrl);
                return Task.CompletedTask;
            },
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationDefaults.AuthenticationScheme,
        _ => { })
    .AddPolicyScheme(MultiAuthAuthenticationDefaults.Scheme, MultiAuthAuthenticationDefaults.Scheme, policy =>
    {
        policy.ForwardDefaultSelector = context =>
        {
            if (ApiKeyAuth.ShouldUseApiKeyPath(context.Request.Path)
                && ApiKeyAuth.TryGetHeader(context.Request, out var k)
                && !string.IsNullOrWhiteSpace(k))
                return ApiKeyAuthenticationDefaults.AuthenticationScheme;
            return JwtBearerDefaults.AuthenticationScheme;
        };
    });

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.AddHttpClient<ITelegramNotifyService, TelegramNotifyService>();

builder.Services.Configure<WebtrethoOptions>(builder.Configuration.GetSection(WebtrethoOptions.SectionName));
builder.Services.AddHttpClient<IWebtrethoUploadService, WebtrethoUploadService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ImageUploadApp/1.0");
    client.Timeout = TimeSpan.FromMinutes(3);
});
builder.Services.AddHttpClient("media-proxy", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ImageUploadApp/1.0");
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddSingleton<IImageEncodingService, ImageEncodingService>();
builder.Services.AddScoped<ImagePipelineJob>();
builder.Services.AddScoped<PendingFolderReconcileJob>();

var hangfireConnection = builder.Configuration.GetConnectionString("HangfireConnection") ?? connectionString;
builder.Services.AddHangfireWithMySql(hangfireConnection);

builder.Services.AddControllersWithViews();
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection(SiteOptions.SectionName));
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

GlobalJobFilters.Filters.Add(new ImagePipelineJobFailedFilter(
    app.Services.GetRequiredService<IServiceScopeFactory>()));

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (string.Equals(Environment.GetEnvironmentVariable("APPLY_MIGRATIONS"), "true", StringComparison.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

await using (var scope = app.Services.CreateAsyncScope())
{
    var rm = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await rm.RoleExistsAsync("User"))
        await rm.CreateAsync(new IdentityRole("User"));
}

if (!string.Equals(Environment.GetEnvironmentVariable("DISABLE_HTTPS_REDIRECT"), "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}
app.UseResponseCompression();
app.UseRouting();

app.UseMiddleware<JwtRenewalMiddleware>();
app.UseAuthentication();
app.UseSiteVisitTelegramNotify();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (IsMobileUserAgent(context.Request.Headers.UserAgent.ToString()))
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/Account/Manage", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/Photos");
            return;
        }
    }

    await next();
});

app.MapStaticAssets();

app.MapControllers();

app.MapControllerRoute(
        name: "pricing",
        pattern: "pricing",
        defaults: new { controller = "Pricing", action = "Index" })
    .WithStaticAssets();

app.MapControllerRoute(
        name: "privacy",
        pattern: "privacy",
        defaults: new { controller = "Home", action = "Privacy" })
    .WithStaticAssets();

app.MapControllerRoute(
        name: "developerAlbumApi",
        pattern: "developer/album-api",
        defaults: new { controller = "Developer", action = "AlbumApiDocs" })
    .WithStaticAssets();

app.MapControllerRoute(
        name: "developerApiKeys",
        pattern: "developer/api-keys",
        defaults: new { controller = "Developer", action = "ApiKeys" })
    .WithStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var recurring = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurring.AddOrUpdate<PendingFolderReconcileJob>(
        "pending-folder-reconcile",
        job => job.RunAsync(CancellationToken.None),
        Cron.MinuteInterval(4),
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
}

app.Run();
