using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

/// <summary>
/// MOD-0155 FU01 — the create/edit form (Golden Compact). Every vocabulary field is fed from the runtime contract, so
/// nothing here hardcodes a target type, purpose, visit type, status or source. <c>PlannedDate</c> is a bare calendar
/// day; the motor-filled slot fields and the derived provenance blocks are never authored here (V26).
/// </summary>
public sealed class PlannedVisitEditViewModel
{
    public Guid? PlannedVisitId { get; set; }

    [Required]
    public string VisitCode { get; set; } = string.Empty;

    [Required]
    public string TargetType { get; set; } = "account";

    [Required]
    public Guid? TargetId { get; set; }

    /// <summary>Display snapshot of the chosen target (rendered read-only; never posted as authority).</summary>
    public string? TargetDisplay { get; set; }

    [Required]
    public string ResourceId { get; set; } = string.Empty;

    [Required]
    public string ResourceType { get; set; } = "person";

    public string? ResourceDisplayName { get; set; }

    [Required]
    public DateTimeOffset? PlannedDate { get; set; }

    public string? PlannedStartTime { get; set; }
    public string? PlannedEndTime { get; set; }
    public int? PlannedDurationMinutes { get; set; }

    [Required]
    public string VisitPurpose { get; set; } = string.Empty;

    [Required]
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

    /// <summary>strategy | manual marker (D10). Server-set; the form carries the badge state so an override survives a
    /// failed post.</summary>
    public string? ContentSource { get; set; }

    /// <summary>draft or planned on create; not editable after (the lifecycle moves through confirm/cancel/archive).</summary>
    public string? PlanStatus { get; set; }

    /// <summary>manual only in FU01.</summary>
    public string? Source { get; set; }

    public int? ExpectedVersion { get; set; }

    public bool IsArchived => string.Equals(PlanStatus, "archived", StringComparison.OrdinalIgnoreCase);
    public bool IsCancelled => string.Equals(PlanStatus, "cancelled", StringComparison.OrdinalIgnoreCase);
    public bool IsConfirmed => string.Equals(PlanStatus, "confirmed", StringComparison.OrdinalIgnoreCase);
    public bool IsDraft => string.Equals(PlanStatus, "draft", StringComparison.OrdinalIgnoreCase);

    public bool CanManage { get; set; }
}

/// <summary>What the Index page needs before it renders.</summary>
public sealed class PlannedVisitIndexViewModel
{
    public bool CanManage { get; set; }
    public bool CanConfirm { get; set; }
}

/// <summary>The gateway envelope, mirrored so the proxy can read <c>data</c> / <c>errors</c> without a shared package.</summary>
public sealed class PlannedVisitGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}

/// <summary>The API's plan detail, as much of it as the Edit / Details pages need (provenance blocks included).</summary>
public sealed class PlannedVisitDetailApiModel
{
    public Guid PlannedVisitId { get; set; }
    public string VisitCode { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? AccountContactLinkId { get; set; }
    public string PlannedDate { get; set; } = string.Empty;
    public string? PlannedStartTime { get; set; }
    public string? PlannedEndTime { get; set; }
    public int? PlannedDurationMinutes { get; set; }
    public PlannedVisitResourceRefApiModel Resource { get; set; } = new();
    public string? PositionCode { get; set; }
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
    public string PlanStatus { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public PlannedVisitSlotApiModel Slot { get; set; } = new();
    public PlannedVisitFrequencyApiModel? Frequency { get; set; }
    public PlannedVisitConsentApiModel? Consent { get; set; }
    public PlannedVisitContentApiModel? Content { get; set; }
    public PlannedVisitSelectionApiModel? Selection { get; set; }
    public PlannedVisitAvailabilityApiModel? Availability { get; set; }
    public int Version { get; set; }
}

public sealed class PlannedVisitResourceRefApiModel
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

public sealed class PlannedVisitSlotApiModel
{
    public int? SequenceOrder { get; set; }
    public string? SlotStartTime { get; set; }
    public string? SlotEndTime { get; set; }
    public bool IsPacked { get; set; }
}

public sealed class PlannedVisitFrequencyApiModel
{
    public string FrequencyStatus { get; set; } = "unknown";
    public string? SelectedPolicyName { get; set; }
    public string? FrequencyType { get; set; }
    public int? RequiredVisitCount { get; set; }
    public string? PeriodType { get; set; }
    public List<string> ReasonCodes { get; set; } = [];
}

public sealed class PlannedVisitConsentApiModel
{
    public bool FilterApplied { get; set; }
    public string EligibilityStatus { get; set; } = "unknown";
    public string Decision { get; set; } = string.Empty;
    public string Channel { get; set; } = "visit";
    public string Purpose { get; set; } = string.Empty;
    public List<string> ReasonCodes { get; set; } = [];
    public string EvaluatorVersion { get; set; } = string.Empty;
}

public sealed class PlannedVisitContentApiModel
{
    public Guid? JourneyId { get; set; }
    public Guid? StageId { get; set; }
    public int? StageIndex { get; set; }
    public string? StageCode { get; set; }
    public string ContentSource { get; set; } = "manual";
    public bool IsOverridden { get; set; }
    public string? JourneyDisplayName { get; set; }
    public string? StageDisplayName { get; set; }
}

public sealed class PlannedVisitSelectionApiModel
{
    public string SelectionMode { get; set; } = "manual";
    public string? DecidedBy { get; set; }
}

public sealed class PlannedVisitAvailabilityApiModel
{
    public string? Weekday { get; set; }
    public string? AvailableStartTime { get; set; }
    public string? AvailableEndTime { get; set; }
    public bool? AppointmentRequired { get; set; }
    public bool? WithinAvailableWindow { get; set; }
    public List<string> ReasonCodes { get; set; } = [];
}
