using Diten.BuildingBlocks.BackgroundJobs;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Diten.Platform.Infrastructure.BackgroundJobs;

public static class BackgroundJobApplicationBuilderExtensions
{
    public static IApplicationBuilder UsePlatformHangfireDashboard(this IApplicationBuilder app, IConfiguration configuration)
    {
        var options = configuration.GetSection(BackgroundJobSchedulerOptions.SectionName)
            .Get<BackgroundJobSchedulerOptions>() ?? new BackgroundJobSchedulerOptions();

        if (!options.DashboardEnabled)
        {
            return app;
        }

        app.UseHangfireDashboard(options.DashboardPath, CreateDashboardOptions());

        return app;
    }

    /// <summary>
    /// The dashboard's options as production uses them. BL-521 FIX2 item 4 — the door is the ASYNC record-reading filter
    /// alone, and both lists are set explicitly: Hangfire admits a request only when every filter in BOTH lists admits it
    /// (and admits every request when both are empty), so a token-only filter added back to the synchronous list, or an
    /// emptied asynchronous list, would reopen the dashboard. The guard test pins this exact pair.
    /// </summary>
    public static DashboardOptions CreateDashboardOptions() => new()
    {
        Authorization = Array.Empty<IDashboardAuthorizationFilter>(),
        AsyncAuthorization = [new PlatformActorHangfireAuthorizationFilter()]
    };
}
