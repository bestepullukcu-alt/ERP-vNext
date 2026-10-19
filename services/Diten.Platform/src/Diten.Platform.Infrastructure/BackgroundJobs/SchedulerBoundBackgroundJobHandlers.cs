using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.Platform.Infrastructure.BackgroundJobs;

/// <summary>
/// The background-job handlers that can exist only where a scheduler exists.
///
/// <para>A handler whose constructor takes <see cref="IBackgroundJobScheduler"/> cannot be registered by
/// <c>AddApplication</c>: with both background-job flags off no scheduler is registered, and the container —
/// validated on build — would refuse to start over a handler nothing will ever run. So such a handler is
/// registered HERE, by the same call that registers the scheduler (<c>ConfigureHangfire</c>), and by nothing
/// else. Every other handler stays in <c>AddApplication</c>.</para>
///
/// <para>This is one named place instead of a line inside <c>ConfigureHangfire</c> so that
/// <c>BackgroundJobHandlerRegistrationTests</c> can measure the real registration without a MongoDB: a handler
/// registered in neither place fails there, and so does a handler listed here that does not need the scheduler.</para>
/// </summary>
public static class SchedulerBoundBackgroundJobHandlers
{
    public static IServiceCollection AddSchedulerBoundBackgroundJobHandlers(this IServiceCollection services)
    {
        services.AddScoped<EmailDispatchSweepJob>();
        return services;
    }
}
