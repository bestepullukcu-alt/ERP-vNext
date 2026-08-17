using Microsoft.AspNetCore.Builder;

namespace Diten.Platform.Common.Tenancy;

public static class TenantResolutionExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantResolutionMiddleware>();
    }
}
