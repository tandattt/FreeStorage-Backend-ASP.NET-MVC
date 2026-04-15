using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Primitives;

namespace ImageUploadApp.Infrastructure;

public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
}

public static class MultiAuthAuthenticationDefaults
{
    public const string Scheme = "MultiAuth";
}

public static class ApiKeyAuth
{
    public static bool TryGetHeader(HttpRequest request, [NotNullWhen(true)] out string? key)
    {
        key = null;
        if (request.Headers.TryGetValue("X-Api-Key", out StringValues v1) && !StringValues.IsNullOrEmpty(v1))
        {
            key = v1.ToString();
            return true;
        }

        if (request.Headers.TryGetValue("Api-Key", out StringValues v2) && !StringValues.IsNullOrEmpty(v2))
        {
            key = v2.ToString();
            return true;
        }

        if (request.Headers.TryGetValue("apikey", out StringValues v3) && !StringValues.IsNullOrEmpty(v3))
        {
            key = v3.ToString();
            return true;
        }

        return false;
    }

    public static bool ShouldUseApiKeyPath(PathString path)
    {
        if (path.StartsWithSegments("/api/v1/album"))
            return true;
        if (path.StartsWithSegments("/media"))
            return true;
        return false;
    }
}
