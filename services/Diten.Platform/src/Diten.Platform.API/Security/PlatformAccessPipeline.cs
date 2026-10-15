using Diten.Platform.Common.Tenancy;

namespace Diten.Platform.API.Security;

/// <summary>
/// BL-512 FIX1 — who the caller is, which tenant they act in, what they may do, and how often they may search: the
/// four steps in the ONE order they must run, kept in one place that Program.cs and the HTTP test hosts both call.
///
/// <para>Why one place: the rate limiter partitions people-search by the signed-in user, so it must run AFTER
/// authentication — moved above it, every caller is "anonymous" and the whole platform shares one bucket of 30 a
/// minute. A test host that copied these four lines by hand proved nothing about Program.cs; now the test hosts run
/// this method, and a guard test checks Program.cs calls it exactly once and none of the four on its own.</para>
/// </summary>
public static class PlatformAccessPipeline
{
    public static IApplicationBuilder UsePlatformAccessPipeline(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseTenantResolution();
        app.UseAuthorization();
        app.UseRateLimiter(); // after authentication: the people-search policy partitions by the signed-in user
        return app;
    }
}
