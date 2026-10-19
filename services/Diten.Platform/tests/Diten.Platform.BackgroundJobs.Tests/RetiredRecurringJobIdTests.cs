using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Diten.Platform.Application.Features.WorkingCalendarImport;
using Diten.Platform.Infrastructure.BackgroundJobs;
using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

/// <summary>
/// CRM-BP87-03 — the subscription jobs moved from the deprecated MOD-0297 alias to Blueprint 8.7 MOD-0361. Hangfire keeps
/// every recurring id it was ever given, so an installation that registered the old ids would run the job twice (old id
/// on its stored cron, new id beside it) unless the old ids are removed when the registrar runs.
/// </summary>
public sealed class RetiredRecurringJobIdTests
{
    private const string NewTrialExpiry = "Diten.Platform.MOD-0361.TrialExpiryScanJob";
    private const string NewSubscriptionRenewal = "Diten.Platform.MOD-0361.SubscriptionRenewalJob";
    private const string OldTrialExpiry = "Diten.Platform.MOD-0297.TrialExpiryScanJob";
    private const string OldSubscriptionRenewal = "Diten.Platform.MOD-0297.SubscriptionRenewalJob";

    private static IReadOnlyCollection<RecurringJobRegistration> Registrations(BackgroundJobSchedulerOptions options)
        => new PlatformRecurringJobRegistrar(Options.Create(options), Options.Create(new WorkingCalendarImportOptions()))
            .GetRecurringJobs();

    private static BackgroundJobSchedulerOptions EnabledFor(params string[] ids)
    {
        var options = new BackgroundJobSchedulerOptions { Enabled = true, RegisterStandardJobs = true };
        foreach (var id in ids)
        {
            options.EnabledJobs[id] = true;
        }

        return options;
    }

    [Fact]
    public void The_subscription_jobs_are_registered_under_MOD_0361()
    {
        var jobs = Registrations(new BackgroundJobSchedulerOptions());

        Assert.Equal("MOD-0361", jobs.Single(j => j.Descriptor.Id == NewTrialExpiry).Descriptor.Owner);
        Assert.Equal("MOD-0361/MOD-0364", jobs.Single(j => j.Descriptor.Id == NewSubscriptionRenewal).Descriptor.Owner);
    }

    [Fact]
    public void The_retired_MOD_0297_ids_stay_disabled_even_when_their_old_configuration_key_is_on()
    {
        var jobs = Registrations(EnabledFor(OldTrialExpiry, OldSubscriptionRenewal));

        var retired = jobs.Where(j => j.Descriptor.Id is OldTrialExpiry or OldSubscriptionRenewal).ToList();
        Assert.Equal(2, retired.Count);
        Assert.All(retired, j => Assert.False(j.Descriptor.IsEnabled));
    }

    [Fact]
    public async Task Registering_removes_the_old_ids_from_hangfire_and_schedules_only_the_new_ones()
    {
        var recurring = new Mock<IRecurringJobManager>();
        var scheduler = new HangfireBackgroundJobScheduler(
            new Mock<IBackgroundJobClient>().Object,
            recurring.Object,
            Options.Create(new BackgroundJobSchedulerOptions()));

        foreach (var registration in Registrations(EnabledFor(NewTrialExpiry, NewSubscriptionRenewal, OldTrialExpiry)))
        {
            await scheduler.RegisterRecurringAsync(registration);
        }

        recurring.Verify(x => x.RemoveIfExists(OldTrialExpiry), Times.Once);
        recurring.Verify(x => x.RemoveIfExists(OldSubscriptionRenewal), Times.Once);
        recurring.Verify(x => x.AddOrUpdate(NewTrialExpiry, It.IsAny<Job>(), "0 2 * * *", It.IsAny<RecurringJobOptions>()), Times.Once);
        recurring.Verify(x => x.AddOrUpdate(NewSubscriptionRenewal, It.IsAny<Job>(), "0 3 * * *", It.IsAny<RecurringJobOptions>()), Times.Once);
        recurring.Verify(x => x.AddOrUpdate(OldTrialExpiry, It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<RecurringJobOptions>()), Times.Never);
        recurring.Verify(x => x.AddOrUpdate(OldSubscriptionRenewal, It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<RecurringJobOptions>()), Times.Never);
    }
}
