using Diten.Platform.API.Middlewares;

namespace Diten.Platform.API.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantResolutionMiddleware>();
        return app;
    }
}
