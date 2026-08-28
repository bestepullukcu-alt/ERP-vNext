namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU12 — periodic review / extension / overdue enums (GMG-QMS-SOP-0001 §9.15, §15). Kept in a dedicated file
// so FU12 ownership never edits earlier FU enum surfaces.

public enum PeriodicReviewStatus
{
    NotStarted = 0,
    Initiated = 1,
    InProgress = 2,
    Completed = 3,
    Extended = 4,
    Overdue = 5,
    Cancelled = 6
}

/// <summary>The documented outcome of a periodic review (SOP §9.15). A decision RECOMMENDS a lifecycle action; it does
/// not silently perform one (lifecycle transitions stay with the FU08 engine).</summary>
public enum PeriodicReviewDecision
{
    ContinueEffective = 0,
    Revise = 1,
    Retire = 2,
    Suspend = 3,
    NoChange = 4
}

public enum PeriodicReviewExtensionStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,

    /// <summary>An approved extension whose extended due date passed without the review completing (SOP §9.15).</summary>
    Expired = 3,
    Cancelled = 4
}

public enum ReviewEscalationType
{
    OverdueCritical = 0,
    ExtensionExpired = 1,
    ManagementReview = 2,
    GqdDeterminationRequired = 3
}

public enum ReviewEscalationSeverity
{
    Warning = 0,
    Major = 1,
    Critical = 2
}

public enum ReviewEscalationStatus
{
    Open = 0,
    Acknowledged = 1,
    Resolved = 2,
    Closed = 3
}

/// <summary>
/// Who must act on a review escalation. Deliberately separate from the FU09 <see cref="ApprovalRequiredRole"/> so FU12
/// can add <see cref="ManagementReview"/> without mutating the approval role surface.
/// </summary>
public enum ReviewEscalationRole
{
    GQD = 0,
    QADocumentation = 1,
    ManagementReview = 2
}
