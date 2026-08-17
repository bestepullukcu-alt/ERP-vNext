using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

public sealed class BackgroundJobContractsTests
{
    [Fact]
    public void Descriptor_rejects_non_utc_timezone()
    {
        var descriptor = new BackgroundJobDescriptor(
            "Diten.Platform.Sample",
            "Diten.Platform",
            "SampleJob",
            "MOD-0026",
            "* * * * *",
            "Local");

        Assert.Throws<BackgroundJobValidationException>(() => descriptor.Validate());
    }

    [Fact]
    public void Platform_registrar_returns_standard_descriptors_disabled_by_default()
    {
        var registrar = new PlatformRecurringJobRegistrar(Options.Create(new BackgroundJobSchedulerOptions()));

        var jobs = registrar.GetRecurringJobs();

        Assert.Equal(8, jobs.Count);
        Assert.All(jobs, job =>
        {
            Assert.False(job.Descriptor.IsEnabled);
            Assert.Equal("Diten.Platform", job.Descriptor.ServiceName);
            Assert.Equal("UTC", job.Descriptor.TimeZoneId);
            Assert.IsAssignableFrom<IBackgroundJobHandler<DeferredPlatformJobArgs>>(
                Activator.CreateInstance(job.HandlerType)!);
        });
    }

    [Fact]
    public void Platform_registrar_enables_descriptor_from_configuration()
    {
        var options = new BackgroundJobSchedulerOptions();
        options.EnabledJobs["Diten.Platform.MOD-0027.EmailDispatchJob"] = true;
        var registrar = new PlatformRecurringJobRegistrar(Options.Create(options));

        var job = registrar.GetRecurringJobs()
            .Single(registration => registration.Descriptor.JobName == "EmailDispatchJob");

        Assert.True(job.Descriptor.IsEnabled);
        Assert.Equal("* * * * *", job.Descriptor.CronExpression);
    }
}
