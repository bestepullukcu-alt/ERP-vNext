namespace Diten.WebUI.Models.DemandIdeas;

public sealed class DemandIdeaCapturePageViewModel
{
    public string PageMode { get; init; } = "create";
    public bool IsReadOnly => string.Equals(PageMode, "view", StringComparison.OrdinalIgnoreCase);
    public bool IsCreate => string.Equals(PageMode, "create", StringComparison.OrdinalIgnoreCase);
    public string? Id { get; init; }
    public string RecordIdDisplay { get; init; } = "";
    public string Title { get; init; } = "";
    public string ProblemStatement { get; init; } = "";
    public string ExpectedOutcome { get; init; } = "";
    public string RequestType { get; init; } = "";
    public string StrategicAlignment { get; init; } = "";
    public string BusinessUnit { get; init; } = "";
    public string Requestor { get; init; } = "";
    public string Sponsor { get; init; } = "";
    public string OwnerName { get; init; } = "";
    public string ProposedScope { get; init; } = "";
    public string OutOfScope { get; init; } = "";
    public string Assumptions { get; init; } = "";
    public string Constraints { get; init; } = "";
    public string BusinessImpact { get; init; } = "";
    public string ExpectedBenefits { get; init; } = "";
    public string ValueNotes { get; init; } = "";
    public string Category { get; init; } = "";
    public string DemandSource { get; init; } = "";
    public string Priority { get; init; } = "";
    public string ComplianceImpact { get; init; } = "";
    public string EstimatedComplexity { get; init; } = "";
    public string RiskSensitivity { get; init; } = "";
    public string Classification { get; init; } = "";
    public IReadOnlyList<string> SupportingLinks { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CaptureAttachmentVm> Attachments { get; init; } = Array.Empty<CaptureAttachmentVm>();
    public string Notes { get; init; } = "";
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string ReviewerComments { get; init; } = "";
    public string InternalNotes { get; init; } = "";
    /// <summary>Formatted timestamp for reviewer thread (e.g. Mar 15, 2026 at 2:30 PM).</summary>
    public string? ReviewerCommentTimestampLabel { get; init; }
    public bool ShowReviewerSection { get; init; }
    public string Status { get; init; } = "Draft";
    /// <summary>Bootstrap badge classes for current status (e.g. bg-label-warning).</summary>
    public string StatusBadgeCss { get; init; } = "bg-label-secondary";
    public DateTime? ReviewDueDate { get; init; }
    public DateTime? DueDate { get; init; }
    public bool CanTransfer { get; init; }
    public bool CanEditFields { get; init; } = true;
    public bool CanSaveDraft { get; init; } = true;
    public bool CanSubmit { get; init; } = true;
    public bool CanAssignReviewer { get; init; } = true;
    public bool ShowApprove { get; init; }
    public bool ShowReject { get; init; }
    public bool ShowTransfer { get; init; }
    public bool ShowExport { get; init; }
    public IReadOnlyList<RelatedIdeaCardVm> RelatedIdeas { get; init; } = Array.Empty<RelatedIdeaCardVm>();
    public IReadOnlyList<DuplicateCardVm> PossibleDuplicates { get; init; } = Array.Empty<DuplicateCardVm>();
    public IReadOnlyList<string> LinkedStrategicThemes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<StakeholderRowVm> AssignedStakeholders { get; init; } = Array.Empty<StakeholderRowVm>();
    public IReadOnlyList<SubmissionCheckItemVm> SubmissionChecklist { get; init; } = Array.Empty<SubmissionCheckItemVm>();
    public IReadOnlyList<ActivityLogItemVm> RecentActivity { get; init; } = Array.Empty<ActivityLogItemVm>();
    public IReadOnlyList<WorkflowTimelineItemVm> WorkflowTimeline { get; init; } = Array.Empty<WorkflowTimelineItemVm>();
    public IReadOnlyList<DecisionHistoryItemVm> DecisionHistory { get; init; } = Array.Empty<DecisionHistoryItemVm>();
    public IReadOnlyList<NextStepCardVm> NextStepChecklist { get; init; } = Array.Empty<NextStepCardVm>();
    public TransferBlockVm? Transfer { get; init; }
    public bool ShowTransferInSidebar { get; init; }
    /// <summary>Transferred or otherwise non-editable while not in explicit view mode.</summary>
    public bool IsRecordLocked => !IsReadOnly && !CanEditFields;
}

public sealed record CaptureAttachmentVm(string FileName, string SizeLabel);
public sealed record RelatedIdeaCardVm(string Id, string Title, int MatchPercent);
public sealed record DuplicateCardVm(string Id, string Title, string Reason);
public sealed record StakeholderRowVm(string Name, string Initials, string Role);
public sealed record SubmissionCheckItemVm(string Key, string Label, bool Done);
public sealed record ActivityLogItemVm(string Text, string WhenLabel, string IconClass);
/// <param name="MetaLine">e.g. &quot;Mar 12, 2026 • Sarah Chen&quot;; for pending steps often null.</param>
/// <param name="IsPending">Future step not yet reached (grey hollow node).</param>
public sealed record WorkflowTimelineItemVm(string Key, string Label, bool Complete, bool Current, string? MetaLine = null, bool IsPending = false);
public sealed record DecisionHistoryItemVm(string WhenLabel, string Actor, string Decision, string Note);
public sealed record NextStepCardVm(string Title, string Description, bool Done);
public sealed record TransferBlockVm(string Status, string TargetType, string TargetId, DateTime? TransferDate, string? TransferredBy, string? LinkedRecordUrl);
