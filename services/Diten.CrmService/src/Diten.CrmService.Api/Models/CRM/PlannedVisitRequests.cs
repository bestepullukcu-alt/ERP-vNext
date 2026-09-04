namespace Diten.CrmService.Api.Models.CRM;

/// <summary>
/// MOD-0155 FU01 request bodies. <c>TenantId</c> appears in none of them — it is resolved server-side from the claim.
/// The motor-filled slot fields and the derived provenance blocks are NOT inputs (V26); the journey/stage (fields 26/27)
/// are the one exception and DO write into the content-position ref (D10). <c>ContentSource</c> is validated but not
/// authored freely — a UI passes <c>strategy</c> when a strategy chain default-filled the journey, otherwise the server
/// treats a rep-entered journey as <c>manual</c>.
/// </summary>
public sealed class CreatePlannedVisitRequest
{
    public string VisitCode { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }

    /// <summary>ISO "yyyy-MM-dd". Required.</summary>
    public string? PlannedDate { get; set; }

    public string? PlannedStartTime { get; set; }
    public string? PlannedEndTime { get; set; }
    public int? PlannedDurationMinutes { get; set; }

    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceDisplayName { get; set; }
    public string? PositionCode { get; set; }
    public Guid? PositionId { get; set; }

    public string VisitPurpose { get; set; } = string.Empty;
    public string VisitType { get; set; } = string.Empty;
    public string? Objective { get; set; }
    public string? Notes { get; set; }

    public string? BusinessUnit { get; set; }
    public Guid? TerritoryNodeId { get; set; }
    public Guid? TerritoryModelId { get; set; }
    public Guid? CampaignId { get; set; }

    /// <summary>Content-position editable surface (field 26). Writes ContentRef.JourneyId (D10).</summary>
    public Guid? ContentEngagementJourneyId { get; set; }

    /// <summary>Content-position editable surface (field 27). Writes ContentRef.StageId (D10).</summary>
    public Guid? ContentEngagementJourneyStageId { get; set; }

    /// <summary>draft (default) or planned. confirmed/cancelled/archived are reached only through transitions.</summary>
    public string? PlanStatus { get; set; }

    /// <summary>manual only in FU01 (default). Reserved values are refused.</summary>
    public string? Source { get; set; }

    /// <summary>strategy | manual marker (D10). Optional.</summary>
    public string? ContentSource { get; set; }

    /// <summary>Snapshot provenance (D10/D11) — not validated, not an FK, not a form field.</summary>
    public Guid? StrategyTemplateId { get; set; }
    public Guid? SegmentId { get; set; }
}

/// <summary>An edit. <c>VisitCode</c> and <c>PlanStatus</c> are absent: the code is never renamed, and the lifecycle
/// moves only through confirm / cancel / archive.</summary>
public sealed class UpdatePlannedVisitRequest
{
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string? PlannedDate { get; set; }
    public string? PlannedStartTime { get; set; }
    public string? PlannedEndTime { get; set; }
    public int? PlannedDurationMinutes { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceDisplayName { get; set; }
    public string? PositionCode { get; set; }
    public Guid? PositionId { get; set; }
    public string VisitPurpose { get; set; } = string.Empty;
    public string VisitType { get; set; } = string.Empty;
    public string? Objective { get; set; }
    public string? Notes { get; set; }
    public string? BusinessUnit { get; set; }
    public Guid? TerritoryNodeId { get; set; }
    public Guid? TerritoryModelId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? ContentEngagementJourneyId { get; set; }
    public Guid? ContentEngagementJourneyStageId { get; set; }
    public string? ContentSource { get; set; }
    public Guid? StrategyTemplateId { get; set; }
    public Guid? SegmentId { get; set; }
    public int? ExpectedVersion { get; set; }
}

/// <summary>The cancel dialog's body: a reason is required (V21).</summary>
public sealed class CancelPlannedVisitRequest
{
    public string? CancellationReason { get; set; }
    public int? ExpectedVersion { get; set; }
}
