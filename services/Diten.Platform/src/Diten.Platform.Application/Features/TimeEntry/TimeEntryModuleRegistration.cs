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
/// MOD-0280-FU01 T1a (ADR-004) — everything the module registers, in ONE place. <c>DependencyInjection.AddApplication</c>
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

        services.AddScoped<ITimesheetWeekReader, TimesheetWeekReader>();
        services.AddScoped<IApproverResolver, ApproverResolver>();
        services.AddScoped<ITimesheetApprovalService, TimesheetApprovalService>();
        services.AddScoped<ITimesheetFinalizer, TimesheetFinalizer>();
        services.AddScoped<ITimesheetDecisionPuller, TimesheetDecisionPuller>();
        services.TryAddSingleton<ITimesheetSubmissionProbe, NoOpTimesheetProbe>();
        services.TryAddSingleton<ITimesheetFinalizationProbe, NoOpTimesheetProbe>();

        // The Task Center's approval card asks the owner what a "timesheet-week" approval is about (BL-437).
        services.AddScoped<IApprovalSourceResolver, TimesheetApprovalSourceResolver>();

        // Registered so Hangfire can resolve it; whether it RUNS is BackgroundJobs:RegisterStandardJobs + EnabledJobs.
        services.AddScoped<TimesheetDecisionSweepJob>();

        services.AddSingleton<IModuleManifestProvider, TimeEntryManifestProvider>();
        return services;
    }
}
