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
    Cancelled
}
