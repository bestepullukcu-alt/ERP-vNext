using Diten.BuildingBlocks.BackgroundJobs;
using Hangfire;
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

        app.UseHangfireDashboard(options.DashboardPath, new DashboardOptions
        {
            Authorization = [new PlatformActorHangfireAuthorizationFilter()]
        });

        return app;
    }
}
