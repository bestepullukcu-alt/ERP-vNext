using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Diten.Platform.Application.Features.WorkingCalendarImport;
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
        // Default WorkingCalendarImportOptions on purpose: Schedule.Enabled defaults to false, so the
        // registrar skips the holiday auto-fetch registration and returns exactly the standard list this
        // test was written to measure. Enabling it here would add a job and change the assertion's subject.
        var registrar = new PlatformRecurringJobRegistrar(
            Options.Create(new BackgroundJobSchedulerOptions()),
            Options.Create(new WorkingCalendarImportOptions()));

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
        // Same reasoning as above: the working-calendar schedule stays off so the only descriptor this test
        // turns on is the one it configures by key.
        var registrar = new PlatformRecurringJobRegistrar(
            Options.Create(options),
            Options.Create(new WorkingCalendarImportOptions()));

        var job = registrar.GetRecurringJobs()
            .Single(registration => registration.Descriptor.JobName == "EmailDispatchJob");

        Assert.True(job.Descriptor.IsEnabled);
        Assert.Equal("* * * * *", job.Descriptor.CronExpression);
    }
}
