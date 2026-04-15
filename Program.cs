using System.Security.Claims;
using System.Threading;
using Hangfire;
using Hangfire.Dashboard;
using ImageUploadApp.Configurations;
using ImageUploadApp.Data;
using ImageUploadApp.Infrastructure;
using ImageUploadApp.Models;
using ImageUploadApp.Services;
using ImageUploadApp.Services.Jobs;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

// EF + MySQL (Pomelo): AutoDetect phiên bản server như PeShop
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

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.AddHttpClient<ITelegramNotifyService, TelegramNotifyService>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnSignedIn = async context =>
    {
        try
        {
            var telegram = context.HttpContext.RequestServices.GetRequiredService<ITelegramNotifyService>();
            var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value
                ?? context.Principal?.FindFirst(ClaimTypes.Name)?.Value
                ?? context.Principal?.Identity?.Name;
            if (string.IsNullOrEmpty(email))
                return;
            await telegram.NotifyLoginAsync(email, context.HttpContext.Connection.RemoteIpAddress?.ToString(), context.HttpContext.RequestAborted);
        }
        catch
        {
            /* không chặn đăng nhập */
        }
    };
});

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

// Hangfire: chuỗi riêng (HangfireConnection) hoặc dùng chung DefaultConnection
var hangfireConnection = builder.Configuration.GetConnectionString("HangfireConnection") ?? connectionString;
builder.Services.AddHangfireWithMySql(hangfireConnection);

builder.Services.AddControllersWithViews();

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

if (!string.Equals(Environment.GetEnvironmentVariable("DISABLE_HTTPS_REDIRECT"), "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseAuthentication();
app.UseSiteVisitTelegramNotify();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (IsMobileUserAgent(context.Request.Headers.UserAgent.ToString()))
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/Identity/Account/Manage", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/Photos");
            return;
        }
    }

    await next();
});

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthFilter()],
});

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

RecurringJob.AddOrUpdate<PendingFolderReconcileJob>(
    "pending-folder-reconcile",
    job => job.RunAsync(CancellationToken.None),
    Cron.MinuteInterval(4),
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

app.Run();
