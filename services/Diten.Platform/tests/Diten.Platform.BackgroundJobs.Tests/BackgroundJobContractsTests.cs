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

    /// <summary>
    /// CT 2026-09-13 — this used to assert exactly 8 jobs, every one a DeferredPlatformJobHandler built with
    /// Activator. That was the registrar of the placeholder era: real sweeps (email dispatch, workflow escalation,
    /// task recurrence and due-soon, MOD-0357 meeting series) replaced placeholders one by one and the test stayed red
    /// through all of them. It now names the list: a job added or removed without updating this set fails HERE, and every
    /// handler must actually implement IBackgroundJobHandler of its own args type (resolution from DI is pinned by
    /// BackgroundJobHandlerRegistrationTests in the Application test project).
    /// </summary>
    [Fact]
    public void Platform_registrar_returns_standard_descriptors_disabled_by_default()
    {
        // Default WorkingCalendarImportOptions on purpose: Schedule.Enabled defaults to false, so the holiday
        // auto-fetch registration is skipped and the standard list below is the whole answer.
        var registrar = new PlatformRecurringJobRegistrar(
            Options.Create(new BackgroundJobSchedulerOptions()),
            Options.Create(new WorkingCalendarImportOptions()));

        var jobs = registrar.GetRecurringJobs();

        Assert.Equal(
            new[]
            {
                "Diten.Platform.MOD-0009.ProvisioningRetryJob",
                "Diten.Platform.MOD-0018.EntitlementCacheRefreshJob",
                "Diten.Platform.MOD-0021.AuditLogArchiveJob",
                "Diten.Platform.MOD-0023.WorkflowEscalationSweepJob",
                "Diten.Platform.MOD-0024.TaskDueSoonSweepJob",
                "Diten.Platform.MOD-0024.TaskRecurrenceSweepJob",
                "Diten.Platform.MOD-0027.EmailDispatchJob",
                "Diten.Platform.MOD-0033.QuotaResetJob",
                "Diten.Platform.MOD-0034.WebhookRetryJob",
                "Diten.Platform.MOD-0297.SubscriptionRenewalJob",
                "Diten.Platform.MOD-0297.TrialExpiryScanJob",
                "Diten.Platform.MOD-0357.MeetingSeriesSweepJob",
            },
            jobs.Select(job => job.Descriptor.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray());

        Assert.All(jobs, job =>
        {
            Assert.False(job.Descriptor.IsEnabled);
            Assert.Equal("Diten.Platform", job.Descriptor.ServiceName);
            Assert.Equal("UTC", job.Descriptor.TimeZoneId);
            Assert.True(
                typeof(IBackgroundJobHandler<>).MakeGenericType(job.ArgsType).IsAssignableFrom(job.HandlerType),
                $"{job.HandlerType.Name} does not handle {job.ArgsType.Name}");
        });
    }

    [Fact]
    public void Platform_registrar_enables_descriptor_from_configuration()
    {
        var options = new BackgroundJobSchedulerOptions { RegisterStandardJobs = true };
        options.EnabledJobs["Diten.Platform.MOD-0027.EmailDispatchJob"] = true;
        // Same reasoning as above: the working-calendar schedule stays off so the only descriptor this test
        // turns on is the one it configures by key. Found by ID, not JobName: the email job's handler became
        // EmailDispatchSweepJob while its configuration key stayed the same, which is what an operator types.
        var registrar = new PlatformRecurringJobRegistrar(
            Options.Create(options),
            Options.Create(new WorkingCalendarImportOptions()));

        var jobs = registrar.GetRecurringJobs();
        var job = jobs.Single(registration => registration.Descriptor.Id == "Diten.Platform.MOD-0027.EmailDispatchJob");

        Assert.True(job.Descriptor.IsEnabled);
        Assert.Equal("* * * * *", job.Descriptor.CronExpression);
        Assert.All(jobs.Where(other => other != job), other => Assert.False(other.Descriptor.IsEnabled));
    }
}
