namespace Diten.InventoryService.Domain.Recall.Enums;

// MOD-0177 (pack §4, RECALL-BUNDLE §2). Every enum travels on the wire by its exact C# name (RecallWire.TryParseName).

/// <summary>Pack §8.7 (mockup L:7). <see cref="Closed"/> and <see cref="Cancelled"/> are terminal.</summary>
public enum RecallCaseStatus
{
    Draft,
    PendingScopeApproval,
    Active,
    EffectivenessCheck,
    Closed,
    Cancelled
}

/// <summary>Mockup "Toptan / Eczane / Hasta".</summary>
public enum RecallLevel
{
    Wholesale,
    Pharmacy,
    Patient
}

/// <summary>The trace direction of a case (pack §4.1; the engine is R2).</summary>
public enum RecallTraceDirection
{
    Backward,
    Forward,
    Both
}

/// <summary>
/// The kind of a scope item (pack §4.3). R1 enters <see cref="Lot"/>, <see cref="Stock"/> and <see cref="Shipment"/> by hand; R2 fills the
/// same items from the trace.
/// </summary>
public enum RecallScopeItemKind
{
    Lot,
    Stock,
    Shipment
}

/// <summary>KR-F channels (mockup modal: Telefon, E-posta (harici), Faks, Yüz yüze).</summary>
public enum RecallNotificationChannel
{
    Phone,
    ExternalEmail,
    Fax,
    InPerson,
    Other
}

/// <summary>The decisions that are prepared before they are signed (pack §8.8 pending-action handshake).</summary>
public enum RecallPendingActionKind
{
    ApproveScope,
    CompleteEffectiveness,
    Close
}

/// <summary>Insert-only case timeline (pack §4.2) — the R1 subset; later slices add kinds (additive).</summary>
public enum RecallCaseEventKind
{
    Opened,
    ScopeItemsAdded,
    ScopeMarked,
    ScopeSubmitted,
    ScopeReturned,
    ScopeApproved,
    SegregationConfirmed,
    CustomerNotified,
    ConsumptionDeclared,
    EffectivenessStarted,
    EffectivenessCompleted,
    Closed,
    Cancelled,

    // R2 (pack §8.3, §8.12)
    TraceRun,
    OutOfScopeFinding,

    // R3 (pack §8.4, §8.5, §8.6, §8.12)
    QuarantineRequested,
    QuarantineFailed,
    QuarantineRetried,
    ExitRefused,
    ReturnLinked,
    ScopeRevised
}

/// <summary>Pack §8.4 KR-B — the state of one quarantine request (one per included Stock node of an approved scope revision).</summary>
public enum RecallQuarantineRequestStatus
{
    Pending,
    Held,
    PartiallyHeld,
    BlockedByReservation,
    NothingAvailable,
    Failed,
    NotNeeded
}

/// <summary>Why no request goes to MOD-0175 (pack §8.4 "durumu gösterilir").</summary>
public enum RecallNotNeededReason
{
    AlreadyQuarantined,
    AlreadyBlocked,
    LotBlocked,
    LotExpired,
    Excluded
}

/// <summary>Where a linked return came from (pack §8.6 KR-H; MOD-0186 when MVP-6 is live).</summary>
public enum RecallReturnSource
{
    Mod0186Return,
    ManualCustomerReturn
}

/// <summary>Where a scope item came from: entered by hand (R1, kept at re-trace) or from the trace (R2, reconciled at submit).</summary>
public enum RecallScopeItemOrigin
{
    Manual,
    Trace
}

/// <summary>Why a trace snapshot was stored (pack §4.4).</summary>
public enum RecallSnapshotPurpose
{
    Submitted,
    Approved,

    /// <summary>WP-0177-R5: the trace of a drill run's step 1 (KR-G, pack §8.10) — the one non-drill-collection row a drill writes.</summary>
    Drill
}
