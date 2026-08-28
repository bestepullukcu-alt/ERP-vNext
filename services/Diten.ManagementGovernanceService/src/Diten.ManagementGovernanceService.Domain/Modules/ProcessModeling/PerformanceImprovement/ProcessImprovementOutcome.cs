namespace Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling.PerformanceImprovement;

public sealed class ProcessImprovementOutcome : EntityBase
{
    public ProcessImprovementOutcome(
        Guid id,
        Guid tenantId,
        DateTime createdAtUtc,
        Guid processImprovementOpportunityId,
        string summary,
        DateTime observedAtUtc)
        : base(id, tenantId, createdAtUtc)
    {
        if (processImprovementOpportunityId == Guid.Empty) throw new ArgumentException(nameof(processImprovementOpportunityId));
        if (observedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("ObservedAtUtc must be UTC.", nameof(observedAtUtc));
        ProcessImprovementOpportunityId = processImprovementOpportunityId;
        Summary = ProcessModelingText.Required(summary, 4000);
        ObservedAtUtc = observedAtUtc;
    }

    public Guid ProcessImprovementOpportunityId { get; }
    public string Summary { get; }
    public DateTime ObservedAtUtc { get; }
}
