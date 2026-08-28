using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Diten.Platform.Application.Features.Tasks.BackgroundJobs;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Phase 4 — how the recurrence sweep is REGISTERED, pinned.
///
/// <para><b>Why the identifiers are asserted literally.</b> The job id is also the CONFIGURATION KEY: enabling
/// the job means writing this exact string into <c>BackgroundJobs:EnabledJobs</c>. A rename here would silently
/// disable a job every operator believes is on, and the failure would look like "recurrence stopped working"
/// with nothing in the logs — the job simply never registers.</para>
///
/// <para><b>Off unless switched on twice.</b> The pack requires this to be documented rather than discovered,
/// "otherwise 'recurrence doesn't work' is misreported as a bug". Both switches are asserted below, separately,
/// so neither can quietly become sufficient on its own.</para>
/// </summary>
public sealed class TaskRecurrenceSweepRegistrationTests
{
    private const string JobId = "Diten.Platform.MOD-0024.TaskRecurrenceSweepJob";

    [Fact]
    public void The_job_is_registered_under_the_id_the_pack_specifies()
    {
        var registration = Find(Registrations(registerStandardJobs: true, enabled: true));

        Assert.Equal(JobId, registration.Descriptor.Id);
        Assert.Equal("TaskRecurrenceSweepJob", registration.Descriptor.JobName);
        Assert.Equal("MOD-0024", registration.Descriptor.Owner);
        Assert.Equal(typeof(TaskRecurrenceSweepJob), registration.HandlerType);
        Assert.Equal(typeof(TaskRecurrenceSweepJobArgs), registration.ArgsType);
    }

    [Fact]
    public void It_runs_in_UTC_on_the_platform_queue()
    {
        /*
         * UTC because nothing records a tenant's time zone, so "the 31st" has to be measured against SOMETHING
         * stated rather than guessed — and the schedule arithmetic makes the same choice. A mismatch between the
         * two would fire the job at an hour the schedule does not consider due.
         */
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
        // implicitly on. This is the case an operator hits first.
        var options = new BackgroundJobSchedulerOptions { RegisterStandardJobs = true };
        var registration = Find(new PlatformRecurringJobRegistrar(Options.Create(options)).GetRecurringJobs());

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
    public void Enabling_it_does_not_disturb_the_escalation_sweep()
    {
        // The two sweeps are independent switches; sharing one would make enabling recurrence silently change
        // MOD-0023's behaviour.
        var registrations = Registrations(registerStandardJobs: true, enabled: true);
        var escalation = registrations.Single(
            r => r.Descriptor.Id == "Diten.Platform.MOD-0023.WorkflowEscalationSweepJob");

        Assert.False(escalation.Descriptor.IsEnabled);
    }

    private static IReadOnlyCollection<RecurringJobRegistration> Registrations(bool registerStandardJobs, bool enabled)
    {
        var options = new BackgroundJobSchedulerOptions
        {
            RegisterStandardJobs = registerStandardJobs,
            EnabledJobs = new Dictionary<string, bool> { [JobId] = enabled }
        };

        return new PlatformRecurringJobRegistrar(Options.Create(options)).GetRecurringJobs();
    }

    private static RecurringJobRegistration Find(IReadOnlyCollection<RecurringJobRegistration> registrations)
        => Assert.Single(registrations, r => r.Descriptor.Id == JobId);
}
