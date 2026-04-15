using Hangfire.Dashboard;

namespace ImageUploadApp.Infrastructure;

public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http?.User?.Identity?.IsAuthenticated == true;
    }
}
