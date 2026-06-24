using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Workflow;

// MOD-0023 Batch 07 — tenant-scoped SLA metadata for the simple UTC escalation runner.
public sealed class SlaEscalationRule : TenantScopedEntity
{
    public required Guid TemplateId { get; set; }
    public string StageCode { get; set; } = "stage-1";
    public string StepCode { get; set; } = "step-1";
    public int DueInMinutes { get; set; }
    public int EscalateAfterMinutes { get; set; }
    public int? TimeoutAfterMinutes { get; set; }
    public List<string> EscalationPrincipalIds { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public string RuleVersion { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset? DeletedAt { get; set; }
}
