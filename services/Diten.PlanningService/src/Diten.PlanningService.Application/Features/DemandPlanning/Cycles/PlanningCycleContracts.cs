using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning.Cycles;

public enum PlanningCalendarKind { VerifiedCorporate, Iso8601Fallback }

public sealed record AuthorizedPlanningContext(
    Guid LegalEntityId,
    string TimeZoneId,
    string CalendarId,
    string CalendarVersion,
    PlanningCalendarKind CalendarKind,
    IReadOnlyList<PlanningWeek>? CorporateWeeks);

// A server-side contract/mock seam. Its production implementation must prove the actor's
// LegalEntity data scope and whether a corporate calendar exists; an outage is not absence.
public interface IPlanningCycleAuthority : ILegalEntityAssignmentAuthority
{
    Task<AuthorizedPlanningContext?> ResolveAsync(
        Guid tenantId, Guid legalEntityId, DateOnly asOfDate,
        DateOnly firstWeekStart, CancellationToken cancellationToken);
}

public enum CycleInsertOutcome { Created, Existing, Conflict }
public sealed record CycleInsertResult(CycleInsertOutcome Outcome, PlanningCycle? Cycle);

public interface IPlanningCycleStore
{
    Task<CycleInsertResult> InsertOrGetAsync(
        PlanningCycle cycle, CancellationToken cancellationToken);

    Task<PlanningCycle?> ReadAsync(Guid tenantId, Guid legalEntityId,
        Guid cycleId, CancellationToken cancellationToken);
}
