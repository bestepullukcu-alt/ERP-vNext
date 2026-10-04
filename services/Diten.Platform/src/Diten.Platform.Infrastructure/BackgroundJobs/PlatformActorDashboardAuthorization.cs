using System.Net;
using Diten.BuildingBlocks.BackgroundJobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Diten.Platform.Infrastructure.BackgroundJobs;

public static class PlatformActorDashboardAuthorization
{
    /// <summary>
    /// BL-521 — the job dashboard shows and runs every tenant's jobs: platform administrators only, never a partner.
    ///
    /// <para>FIX2 item 4 — decided by the administrator RECORD, the same decision the <c>PlatformAdminOnly</c> policy makes
    /// (<c>DependencyInjection.IsConfirmedPlatformAdministratorAsync</c>): a <c>platform_admin</c>
    /// token whose own record is live and is not a partner record. Before, the token alone decided, so an administrator
    /// demoted to partner, suspended or deleted kept the dashboard for as long as its token lived.</para>
    /// </summary>
    public static async Task<bool> IsAuthorizedAsync(HttpContext httpContext)
    {
        var user = httpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var actorType = user.Claims
            .FirstOrDefault(claim => string.Equals(claim.Type, "actor_type", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        if (!string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return await DependencyInjection.IsConfirmedPlatformAdministratorAsync(httpContext);
    }

    public static bool IsDevelopmentAnonymousBypassAllowed(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            return false;
        }

        var environment = httpContext.RequestServices.GetService<IHostEnvironment>();
        if (environment?.IsDevelopment() != true)
        {
            return false;
        }

        var configuration = httpContext.RequestServices.GetService<IConfiguration>();
        if (configuration?.GetValue<bool>($"{BackgroundJobSchedulerOptions.SectionName}:DashboardAllowAnonymousInDevelopment") != true)
        {
            return false;
        }

        var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
        return remoteIpAddress is not null && IPAddress.IsLoopback(remoteIpAddress);
    }
}
