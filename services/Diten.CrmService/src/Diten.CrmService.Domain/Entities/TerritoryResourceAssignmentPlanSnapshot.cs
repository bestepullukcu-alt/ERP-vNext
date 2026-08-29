namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0151 FU04B plan baseline (aggregate, model-scoped, pack §7.5a). Write-once copy of the proposed resource
/// assignment plan as it stood at model activation — the reference point every later "plan vs current" comparison
/// is measured against.
///
/// <para><b>Immutable.</b> Written inside the activation lifecycle operation and never updated or deleted. A later
/// re-activation (inactive → active) writes a NEW <see cref="SnapshotVersion"/>; the earlier one stays.</para>
///
/// <para><b>Display copy, not a system of record.</b> Person/Position master stays with MOD-0288 and the assignment
/// SoR stays with <see cref="TerritoryResourceAssignment"/>; <see cref="TerritoryResourceAssignmentPlanSnapshotLine.SourceAssignmentId"/>
/// is the only key back into the live chain. The legacy RoleCode is deliberately absent (pack §22.4 position rule).</para>
/// </summary>
public sealed class TerritoryResourceAssignmentPlanSnapshot : EntityBase
{
    public Guid TerritoryModelId { get; set; }

    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    public string CapturedBy { get; set; } = string.Empty;

    /// <summary>Correlation id of the activation that produced this baseline.</summary>
    public string? ActivationCorrelationId { get; set; }

    /// <summary>1 for the first activation, incremented on every re-activation of the same model.</summary>
    public int SnapshotVersion { get; set; } = 1;

    public List<TerritoryResourceAssignmentPlanSnapshotLine> Lines { get; set; } = [];
}

/// <summary>One planned responsibility, frozen at activation time.</summary>
public sealed class TerritoryResourceAssignmentPlanSnapshotLine
{
    public Guid? TerritoryNodeId { get; set; }
    public string TerritoryNodeCode { get; set; } = string.Empty;
    public string TerritoryNodeName { get; set; } = string.Empty;

    public List<string> BusinessScopes { get; set; } = [];

    public string PositionCode { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string PositionType { get; set; } = string.Empty;

    /// <summary>External id in the owning master (Person / User / HCM Employee) — a plain string, never a Guid.</summary>
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceDisplayName { get; set; } = string.Empty;

    public DateTimeOffset PlannedEffectiveFrom { get; set; }
    public DateTimeOffset? PlannedEffectiveTo { get; set; }

    public bool IsPrimary { get; set; }

    /// <summary>The proposed assignment this line was taken from. Follows replacement/transfer provenance forward.</summary>
    public Guid SourceAssignmentId { get; set; }
}
