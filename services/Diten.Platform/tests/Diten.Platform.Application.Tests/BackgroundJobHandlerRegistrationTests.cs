using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests;

/// <summary>
/// S10 live pass, 2026-09-13 — the meeting series sweep and the holiday auto-fetch job were both wired into
/// <c>PlatformRecurringJobRegistrar</c> and neither was registered in DI. Hangfire's executor resolves the handler
/// with <c>GetRequiredService</c>, so each run threw "No service for type …" the moment its flag was on — in every
/// environment. Every unit test constructed the handler by hand, which is exactly why none of them could see it.
/// This reads the real composition, so a handler added without its registration fails here instead of in Hangfire.
/// </summary>
public sealed class BackgroundJobHandlerRegistrationTests
{
    [Fact]
    public void Every_background_job_handler_in_the_application_assembly_is_registered_by_AddApplication()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var handlerTypes = typeof(Diten.Platform.Application.DependencyInjection).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.GetInterfaces().Any(contract =>
                    contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IBackgroundJobHandler<>)))
            .ToList();

        Assert.NotEmpty(handlerTypes);
        var missing = handlerTypes
            .Where(type => !services.Any(descriptor => descriptor.ServiceType == type))
            .Select(type => type.FullName)
            .ToList();
        Assert.True(missing.Count == 0, "Background job handlers not registered in AddApplication: " + string.Join(", ", missing));
    }
}
