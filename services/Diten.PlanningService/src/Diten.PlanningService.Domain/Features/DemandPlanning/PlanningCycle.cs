namespace Diten.PlanningService.Domain.Features.DemandPlanning;

// A cycle is an append-only snapshot. Later planning activity creates revisions, not cycle edits.
public sealed class PlanningCycle : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid CreatedByActorId { get; set; }
    public DateOnly AsOfDate { get; set; }
    public string CalendarId { get; set; } = string.Empty;
    public string CalendarVersion { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public DateOnly HorizonStart { get; set; }
    public DateOnly HorizonEnd { get; set; }
    public string PlanningPeriodKey { get; set; } = string.Empty;
    public List<PlanningWeek> Weeks { get; set; } = [];
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
}

public sealed class PlanningWeek
{
    public int Number { get; set; }
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
}
