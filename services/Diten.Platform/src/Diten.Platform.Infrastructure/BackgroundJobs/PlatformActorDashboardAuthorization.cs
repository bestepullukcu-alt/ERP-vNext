using System.Security.Claims;
using System.Net;
using Diten.BuildingBlocks.BackgroundJobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Diten.Platform.Infrastructure.BackgroundJobs;

public static class PlatformActorDashboardAuthorization
{
    public static bool IsAuthorized(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var actorType = user.Claims
            .FirstOrDefault(claim => string.Equals(claim.Type, "actor_type", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
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
