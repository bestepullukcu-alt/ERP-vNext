using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application;
using Diten.Platform.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests;

/// <summary>
/// S10 live pass, 2026-09-13 — the meeting series sweep and the holiday auto-fetch job were both wired into
/// <c>PlatformRecurringJobRegistrar</c> and neither was registered in DI. Hangfire's executor resolves the handler
/// with <c>GetRequiredService</c>, so each run threw "No service for type …" the moment its flag was on — in every
/// environment. Every unit test constructed the handler by hand, which is exactly why none of them could see it.
/// This reads the real composition, so a handler added without its registration fails here instead of in Hangfire.
///
/// <para><b>Two places, one rule (2026-10-01).</b> A handler that takes <see cref="IBackgroundJobScheduler"/> in its
/// constructor cannot live in <c>AddApplication</c>: with the scheduler switched off nothing registers the
/// scheduler, and the validated container would not build (PlatformContainerValidationTests,
/// <c>Api_serving_container_builds_with_scheduler_and_dashboard_disabled</c>). Those handlers are registered by
/// <see cref="SchedulerBoundBackgroundJobHandlers"/>, the call <c>ConfigureHangfire</c> makes beside the scheduler
/// itself. Which handler belongs where is DERIVED from the constructor here, not listed — so the second place
/// cannot become a way around the first.</para>
/// </summary>
public sealed class BackgroundJobHandlerRegistrationTests
{
    [Fact]
    public void Every_background_job_handler_in_the_application_assembly_is_registered_by_AddApplication()
    {
        var application = new ServiceCollection();
        application.AddApplication();
        var schedulerBound = new ServiceCollection();
        schedulerBound.AddSchedulerBoundBackgroundJobHandlers();

        var handlerTypes = HandlerTypes();
        Assert.NotEmpty(handlerTypes);

        var missing = handlerTypes
            .Where(type => !IsRegistered(NeedsScheduler(type) ? schedulerBound : application, type))
            .Select(type => type.FullName + (NeedsScheduler(type)
                ? " (takes IBackgroundJobScheduler → SchedulerBoundBackgroundJobHandlers)"
                : " (→ AddApplication)"))
            .ToList();
        Assert.True(missing.Count == 0, "Background job handlers not registered where they belong: " + string.Join(", ", missing));
    }

    [Fact]
    public void A_handler_that_needs_the_scheduler_is_registered_only_beside_it_and_nothing_else_is()
    {
        var application = new ServiceCollection();
        application.AddApplication();
        var schedulerBound = new ServiceCollection();
        schedulerBound.AddSchedulerBoundBackgroundJobHandlers();

        // In AddApplication it would break the container of a host that runs with the scheduler off.
        var wronglyInApplication = HandlerTypes()
            .Where(type => NeedsScheduler(type) && IsRegistered(application, type))
            .Select(type => type.FullName)
            .ToList();
        Assert.True(wronglyInApplication.Count == 0,
            "These handlers take IBackgroundJobScheduler but are registered by AddApplication: "
            + string.Join(", ", wronglyInApplication));

        // The scheduler-bound list is not a second, unguarded AddApplication.
        var wronglySchedulerBound = schedulerBound
            .Select(descriptor => descriptor.ServiceType)
            .Where(type => !NeedsScheduler(type))
            .Select(type => type.FullName)
            .ToList();
        Assert.True(wronglySchedulerBound.Count == 0,
            "These are registered beside the scheduler but do not take IBackgroundJobScheduler — they belong in "
            + "AddApplication: " + string.Join(", ", wronglySchedulerBound));

        // Non-vacuity: the split exists because of at least one real handler.
        Assert.Contains(HandlerTypes(), NeedsScheduler);
        Assert.All(schedulerBound, descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
    }

    private static List<Type> HandlerTypes() =>
        typeof(Diten.Platform.Application.DependencyInjection).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.GetInterfaces().Any(contract =>
                    contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IBackgroundJobHandler<>)))
            .ToList();

    private static bool NeedsScheduler(Type type) =>
        type.GetConstructors().Any(constructor =>
            constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(IBackgroundJobScheduler)));

    private static bool IsRegistered(IServiceCollection services, Type type) =>
        services.Any(descriptor => descriptor.ServiceType == type);
}
