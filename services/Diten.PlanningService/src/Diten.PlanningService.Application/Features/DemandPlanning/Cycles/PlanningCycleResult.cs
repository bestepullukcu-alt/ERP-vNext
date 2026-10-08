using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning.Cycles;

public sealed record PlanningCycleResult(
    Guid PlanningCycleId, Guid TenantId, Guid LegalEntityId,
    DateOnly AsOfDate, string CalendarId, string CalendarVersion,
    string TimeZoneId, DateOnly HorizonStart, DateOnly HorizonEnd,
    string PlanningPeriodKey, IReadOnlyList<PlanningWeek> Weeks);
