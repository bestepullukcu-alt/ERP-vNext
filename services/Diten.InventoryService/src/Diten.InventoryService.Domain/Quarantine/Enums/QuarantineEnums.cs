namespace Diten.InventoryService.Domain.Quarantine.Enums;

// MOD-0175 record states (pack §4). None of these is a STOCK status: stock statuses and movement types are only the
// INVENTORY-BUNDLE values of Domain.Enums.StockStatus / MovementType (DEC-INV-08, KQ-E; architecture-tested).

/// <summary>Where the quantity of a hold came from (pack §4 <c>Source</c>).</summary>
public enum HoldSource
{
    GoodsReceipt,
    Production,
    CustomerReturn,
    Manual,
    Retest,
    Rework,
    OtherInbound,

    /// <summary>QUARANTINE 1.3.0 (MOD-0177 CT-R3): taken out of use for a recall case (sourceDocument MOD-0177 / RECALL_CASE).</summary>
    Recall
}

/// <summary>"Bekliyor" / "İncelemede".</summary>
public enum HoldReviewState
{
    Waiting,
    InReview
}

/// <summary>Record state of a hold: "Karar Verildi" = <see cref="Decided"/>.</summary>
public enum HoldStatus
{
    Open,
    DecisionPending,
    Decided
}

public enum DecisionKind
{
    HoldDecision,
    Disposition,
    Unblock,
    LotRelease
}

/// <summary>One line of a hold decision (KQ-A: one line, or two when partial).</summary>
public enum LineDecision
{
    Release,
    Reject,
    Block
}

/// <summary>Disposition of blocked stock (KQ-E: <see cref="Sample"/> is a decision kind and a location, never a stock status).</summary>
public enum DispositionKind
{
    Destroy,
    ReturnToSupplier,
    Rework,
    Sample
}

/// <summary>Decision state = the last <see cref="DecisionEventKind"/> (contract §5); <see cref="Draft"/> = no event yet.</summary>
public enum DecisionState
{
    Draft,
    Submitted,
    PendingApproval,
    Approved,
    Applied,
    Rejected,
    Cancelled,
    TimedOut,
    Withdrawn,
    StartFailed,
    ApplyFailed,
    FourEyesViolation
}

/// <summary>The insert-only events of a decision (pack §3 <c>QuarantineDecisionEvent</c>).</summary>
public enum DecisionEventKind
{
    Submitted,
    WorkflowStarted,
    StartFailed,
    Approved,
    Rejected,
    Cancelled,
    TimedOut,
    Applied,
    ApplyFailed,
    FourEyesViolation,
    Withdrawn
}

/// <summary>Outcome the approval port reports for a decision (Q1: the internal test port; Q2: the Workflow instance).</summary>
public enum ApprovalOutcome
{
    Approved,
    Rejected,
    Cancelled,
    TimedOut
}

public enum BlockedOrigin
{
    QuarantineDecision,
    ManualBlock,
    Rework,
    OtherInbound,
    LotBlocked
}

public enum BlockedStatus
{
    Open,
    Closed,
    SampleRetained
}

public enum HistorySubject
{
    Hold,
    Blocked
}

public enum HistoryAction
{
    Quarantined,
    ReviewStarted,
    DecisionSubmitted,
    Approved,
    Rejected,
    Cancelled,
    TimedOut,
    Withdrawn,
    Applied,
    ApplyFailed,
    FourEyesViolation,
    Blocked,
    ReviewCompleted,
    Disposed,
    Unblocked,
    SampleRetained,

    /// <summary>QUARANTINE 1.2.0 (FU-0175-ITEM-01): <c>updateHold</c> changed the analysis note / attachment references.</summary>
    NoteUpdated
}
