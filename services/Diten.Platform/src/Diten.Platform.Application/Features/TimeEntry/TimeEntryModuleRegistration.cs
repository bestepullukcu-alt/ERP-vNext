using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Adapters;
using Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;
using Diten.Platform.Application.Features.TimeEntry.Providers;
using Diten.Platform.Application.Features.TimeEntry.SelfRegistration;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.Platform.Application.Features.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1a + T1b (ADR-004) — everything the module registers, in ONE place. <c>DependencyInjection.AddApplication</c>
/// calls it (its one additive line), and the module's HTTP tests call the SAME method, so the tests cannot drift into
/// a wiring production does not have. Handlers and validators come from the assembly scan; storage from Infrastructure.
/// </summary>
public static class TimeEntryModuleRegistration
{
    public static IServiceCollection AddTimeEntryModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // The only doors to MOD-0288 and MOD-0024 (pack §3.3); everything else in the module talks to these ports.
        services.AddScoped<ITimeEntryOrgGateway, OrgGatewayAdapter>();
        services.AddScoped<ITimeEntryTaskGateway, TaskGatewayAdapter>();
        services.AddScoped<ITimeEntryMeetingGateway, MeetingGatewayAdapter>();

        services.AddScoped<ITimesheetWeekReader, TimesheetWeekReader>();
        services.AddScoped<IApproverResolver, ApproverResolver>();
        services.AddScoped<ITimesheetApprovalService, TimesheetApprovalService>();
        services.AddScoped<ITimesheetFinalizer, TimesheetFinalizer>();
        services.AddScoped<ITimesheetDecisionPuller, TimesheetDecisionPuller>();
        services.TryAddSingleton<ITimesheetSubmissionProbe, NoOpTimesheetProbe>();
        services.TryAddSingleton<ITimesheetFinalizationProbe, NoOpTimesheetProbe>();

        // T1b — capture: the timer, its drafts, the read-time reconcile, meeting suggestions.
        services.AddScoped<ITimerDraftWriter, TimerDraftWriter>();
        services.AddScoped<ITimerService, TimerService>();
        services.AddScoped<ITimerReadModel, TimerReadModel>();
        services.AddScoped<ITimeSuggestionReader, TimeSuggestionReader>();
        services.TryAddScoped<ITimerAutoCloseNotifier, TimerAutoCloseNotifier>();

        // The two doors other modules use (pack §3.3): MOD-0024 tells us a task moved, and reads its spent time from us.
        services.AddScoped<ITaskTransitionObserver, TaskTransitionTimerObserver>();
        services.AddScoped<ITaskSpentTimeSource, TaskSpentTimeSource>();
        // T2b — whether the reader's timer is switched on, so the Task Center offers startTimer/stopTimer only then.
        services.AddScoped<ITimeEntryTimerAvailability, TimerAvailability>();
        // T2b — the approver's marks and the Task Center work-item id of a submitted week (read only).
        services.AddScoped<IApprovalWeekFacts, ApprovalWeekFacts>();

        // §5.1 item 3 — T2b shipped the card (real timeEntries block, start/stop actions): timeTracking is DECLARED.
        // TryAdd, so a host that must stay off (a test pinning the old projection) still wins with its own instance.
        services.TryAddSingleton(new TaskTimeTrackingOptions { DeclareTimeTracking = true });

        // The Task Center's approval card asks the owner what a "timesheet-week" approval is about (BL-437).
        services.AddScoped<IApprovalSourceResolver, TimesheetApprovalSourceResolver>();

        // Registered so Hangfire can resolve it; whether it RUNS is BackgroundJobs:RegisterStandardJobs + EnabledJobs.
        services.AddScoped<TimesheetDecisionSweepJob>();
        services.AddScoped<TimerMidnightCloseJob>();

        services.AddSingleton<IModuleManifestProvider, TimeEntryManifestProvider>();
        return services;
    }
}
