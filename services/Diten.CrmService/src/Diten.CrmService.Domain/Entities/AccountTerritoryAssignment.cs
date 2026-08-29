namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0151 FU05 account-to-territory coverage aggregate. Account is referenced read-only; coverage is never
/// persisted on the MOD-0149 Account master. History is append-only: an effective assignment is ended, not deleted.
/// </summary>
public sealed class AccountTerritoryAssignment : EntityBase
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountDisplayName { get; set; } = string.Empty;
    public Guid TerritoryModelId { get; set; }
    public Guid TerritoryNodeId { get; set; }
    public string TerritoryNodeCode { get; set; } = string.Empty;
    public string TerritoryNodeName { get; set; } = string.Empty;
    public List<TerritoryBusinessScope> BusinessScopes { get; set; } = [];
    public string AssignmentSource { get; set; } = string.Empty;
    public string AssignmentStatus { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public Guid? AppliedFromPreviewRunId { get; set; }
    public Guid? AppliedRuleId { get; set; }
    public string? AppliedRuleCode { get; set; }
    public Guid? MigratedFromAssignmentId { get; set; }
    public Guid? MigratedFromModelId { get; set; }
    public string ConflictPolicy { get; set; } = string.Empty;
    public string? OverrideReason { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? EndedBy { get; set; }
    public string? CorrelationId { get; set; }
}
