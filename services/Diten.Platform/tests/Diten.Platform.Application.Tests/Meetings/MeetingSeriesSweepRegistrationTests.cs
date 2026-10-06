using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Diten.Platform.Application.Features.Meetings.BackgroundJobs;
using Diten.Platform.Application.Features.WorkingCalendarImport;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S11 (KS7) — how the meeting-series sweep is REGISTERED, pinned. Mirrors
/// TaskRecurrenceSweepRegistrationTests' own shape exactly for the sibling job this WP's reading list names as
/// the pattern to copy.
///
/// <para><b>Why the identifiers are asserted literally.</b> The job id is also the CONFIGURATION KEY:
/// enabling the job means writing this exact string into <c>BackgroundJobs:EnabledJobs</c>. A rename here would
/// silently disable a job every operator believes is on.</para>
/// </summary>
public sealed class MeetingSeriesSweepRegistrationTests
{
    private const string JobId = "Diten.Platform.MOD-0357.MeetingSeriesSweepJob";

    [Fact]
    public void The_job_is_registered_under_the_id_the_pack_specifies()
    {
        var registration = Find(Registrations(registerStandardJobs: true, enabled: true));

        Assert.Equal(JobId, registration.Descriptor.Id);
        Assert.Equal("MeetingSeriesSweepJob", registration.Descriptor.JobName);
        Assert.Equal("MOD-0357", registration.Descriptor.Owner);
        Assert.Equal(typeof(MeetingSeriesSweepJob), registration.HandlerType);
        Assert.Equal(typeof(MeetingSeriesSweepJobArgs), registration.ArgsType);
    }

    [Fact]
    public void It_runs_in_UTC_on_the_platform_queue()
    {
        var registration = Find(Registrations(registerStandardJobs: true, enabled: true));

        Assert.Equal("UTC", registration.Descriptor.TimeZoneId);
        Assert.Equal("platform", registration.Descriptor.Queue);
        Assert.Equal(BackgroundJobTriggerTypes.Recurring, registration.Descriptor.TriggerType);
    }

    [Fact]
    public void It_is_OFF_when_the_master_switch_is_off()
    {
        var registration = Find(Registrations(registerStandardJobs: false, enabled: true));
        Assert.False(registration.Descriptor.IsEnabled);
    }

    [Fact]
    public void It_is_OFF_when_its_own_switch_is_off()
    {
        var registration = Find(Registrations(registerStandardJobs: true, enabled: false));
        Assert.False(registration.Descriptor.IsEnabled);
    }

    [Fact]
    public void It_is_OFF_when_nobody_listed_it_at_all()
    {
        // The default state of a fresh deployment: EnabledJobs is empty, so an unlisted job is off rather than
        // implicitly on — the case an operator hits first, and per KS7, a configuration question, not a defect.
        var options = new BackgroundJobSchedulerOptions { RegisterStandardJobs = true };
        var registration = Find(new PlatformRecurringJobRegistrar(Options.Create(options), Options.Create(new WorkingCalendarImportOptions())).GetRecurringJobs());

        Assert.False(registration.Descriptor.IsEnabled);
    }

    [Fact]
    public void It_is_ON_only_when_BOTH_are_true()
    {
        // Non-vacuity for the three tests above: if IsEnabled were hard-wired false they would all pass and the
        // job could never be switched on at all.
        var registration = Find(Registrations(registerStandardJobs: true, enabled: true));
        Assert.True(registration.Descriptor.IsEnabled);
    }

    [Fact]
    public void Enabling_it_does_not_disturb_the_task_recurrence_sweep()
    {
        // The two sweeps are independent switches; sharing one would make enabling meeting series silently
        // change MOD-0024's own recurrence behaviour.
        var registrations = Registrations(registerStandardJobs: true, enabled: true);
        var taskRecurrence = registrations.Single(
            r => r.Descriptor.Id == "Diten.Platform.MOD-0024.TaskRecurrenceSweepJob");

        Assert.False(taskRecurrence.Descriptor.IsEnabled);
    }

    private static IReadOnlyCollection<RecurringJobRegistration> Registrations(bool registerStandardJobs, bool enabled)
    {
        var options = new BackgroundJobSchedulerOptions
        {
            RegisterStandardJobs = registerStandardJobs,
            EnabledJobs = new Dictionary<string, bool> { [JobId] = enabled }
        };

        return new PlatformRecurringJobRegistrar(Options.Create(options), Options.Create(new WorkingCalendarImportOptions())).GetRecurringJobs();
    }

    private static RecurringJobRegistration Find(IReadOnlyCollection<RecurringJobRegistration> registrations)
        => Assert.Single(registrations, r => r.Descriptor.Id == JobId);
}
