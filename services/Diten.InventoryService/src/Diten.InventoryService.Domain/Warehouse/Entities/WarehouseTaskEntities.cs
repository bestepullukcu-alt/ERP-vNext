using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Entities;

// MOD-0178 warehouse tasks (pack §3, §4.1–§4.3). A task keeps NO stock: Planned / Confirmed are the task's own quantities (what it plans
// and what its Posted confirmations moved); the stock itself is the MOD-0173 ledger's (pack §2 boundary rule 1). Every write to a task is
// a compare-and-set on EntityBase.Version (pack §8.3 "Sürüm koşulu — her yazım").

/// <summary>The opaque source of a task (§4.1 <c>SourceRef</c>; S1 manual tasks have none).</summary>
public sealed class WarehouseTaskSourceRef
{
    public string SourceModule { get; set; } = string.Empty;

    public string? SourceType { get; set; }

    public string? SourceDocumentId { get; set; }

    public string? SourceLineId { get; set; }

    public string? TransactionId { get; set; }
}

/// <summary>The movement that holds a confirmation's posting key (§4.2 <c>HolderMovement</c>; the MOD-0173 port's fields one to one).</summary>
public sealed class TaskHolderMovement
{
    public string TransactionId { get; set; } = string.Empty;

    public string TransactionNumber { get; set; } = string.Empty;

    public string SourceModule { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public string? SourceDocumentId { get; set; }

    public string? SourceLineId { get; set; }

    public decimal Quantity { get; set; }

    public string UomId { get; set; } = string.Empty;

    public decimal BaseQuantity { get; set; }

    public string? FromWarehouseId { get; set; }

    public string? FromLocationId { get; set; }

    public string? ToWarehouseId { get; set; }

    public string? ToLocationId { get; set; }

    public string? LotId { get; set; }

    public List<string>? SerialIds { get; set; }

    public string? FromStockStatus { get; set; }

    public string? ToStockStatus { get; set; }

    public DateTimeOffset PostingDate { get; set; }

    public string? ReversedByTransactionId { get; set; }
}

/// <summary>§4.2 — one confirmation of a task (embedded). Each Posted confirmation is one INVENTORY movement.</summary>
public sealed class TaskConfirmation
{
    public int Seq { get; set; }

    public decimal Quantity { get; set; }

    public Guid ToLocationId { get; set; }

    public string? DeviationReason { get; set; }

    public ConfirmationStatus Status { get; set; }

    /// <summary><c>wms:put:{taskId}:{seq}</c> — the INVENTORY idempotency key; the same on every attempt.</summary>
    public string PostingKey { get; set; } = string.Empty;

    /// <summary>Written ONCE in transaction 1 and sent unchanged on every attempt (the ledger fingerprint covers the dates, F-2).</summary>
    public DateTimeOffset TransactionDate { get; set; }

    public DateTimeOffset PostingDate { get; set; }

    public List<string>? SerialIds { get; set; }

    public string? TransactionId { get; set; }

    public string? TransactionNumber { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>The refusal's details as JSON text (a ledger refusal's details are kept as sent).</summary>
    public string? ErrorDetailsJson { get; set; }

    public int? ErrorStatus { get; set; }

    /// <summary><c>StatusDiverged</c> / <c>KeyReused</c> / <c>OverPlanned</c> / <c>HolderReversed</c> — the reason of an IntegrityError.</summary>
    public string? ErrorReason { get; set; }

    public RefusalSource? RefusalSource { get; set; }

    public bool RefusedOnRetry { get; set; }

    public DateTimeOffset? RefusedAtUtc { get; set; }

    public ConfirmationResolution? Resolution { get; set; }

    public ConfirmationResolution? PriorResolution { get; set; }

    public string? ResolutionReason { get; set; }

    public ConfirmationIntegrityResolution? IntegrityResolution { get; set; }

    public Guid? ResolvedBy { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public string? ResolveReason { get; set; }

    public TaskHolderMovement? HolderMovement { get; set; }

    /// <summary>The flight lock (§4.2): the attempt that may write the result, and until when.</summary>
    public Guid? AttemptId { get; set; }

    public DateTimeOffset? InFlightUntilUtc { get; set; }

    public ConfirmationChannel Channel { get; set; } = ConfirmationChannel.Ui;

    public string? ScanRef { get; set; }

    public Guid ConfirmedBy { get; set; }

    public string? ConfirmedByName { get; set; }

    public DateTimeOffset ConfirmedAtUtc { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? LastAttemptAtUtc { get; set; }

    public TaskConfirmation Copy()
    {
        var copy = (TaskConfirmation)MemberwiseClone();
        copy.SerialIds = SerialIds is null ? null : [.. SerialIds];
        return copy;
    }
}

/// <summary>§4.1 — a putaway task (S1; S2 pick tasks share the head record with <see cref="Kind"/> Pick).</summary>
public sealed class WarehouseTask : EntityBase
{
    /// <summary>RCS-001 <c>{COMPANY}-PUT-{000001}</c> from the temporary counter (Q-07 B); written once, never changes.</summary>
    public string Number { get; set; } = string.Empty;

    public string CompanyCodePrefix { get; set; } = string.Empty;

    public string? WarehouseCode { get; set; }

    public WarehouseTaskKind Kind { get; set; } = WarehouseTaskKind.Putaway;

    public WarehouseTaskStatus Status { get; set; } = WarehouseTaskStatus.Open;

    public WarehouseTaskOrigin Origin { get; set; } = WarehouseTaskOrigin.Manual;

    public WarehouseTaskSourceRef? SourceRef { get; set; }

    public string? SourceSystem { get; set; }

    public string? ExternalRef { get; set; }

    public Guid WarehouseId { get; set; }

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string? LotId { get; set; }

    public string? LotNumber { get; set; }

    public bool SerialControlled { get; set; }

    public StockStatus StockStatus { get; set; } = StockStatus.Available;

    public Guid FromLocationId { get; set; }

    public Guid? ToLocationId { get; set; }

    public Guid? SuggestedToLocationId { get; set; }

    public WarehouseTargetSource TargetSource { get; set; } = WarehouseTargetSource.Manual;

    public decimal PlannedQuantity { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    /// <summary>The sum of the Posted confirmations (task-internal, not a stock balance).</summary>
    public decimal ConfirmedQuantity { get; set; }

    public List<TaskConfirmation> Confirmations { get; set; } = [];

    public Guid? AssigneeUserId { get; set; }

    public string? AssigneeName { get; set; }

    public WarehouseTaskPriority Priority { get; set; } = WarehouseTaskPriority.Normal;

    public string? Note { get; set; }

    public string? ShortCloseReason { get; set; }

    public string? CancelReason { get; set; }

    public Guid CreatedBy { get; set; }

    public string? CreatedByName { get; set; }

    public Guid? UpdatedBy { get; set; }

    public string? UpdatedByName { get; set; }

    /// <summary>The create key (unique per company + <see cref="CreatedBy"/>) and its request fingerprint (§8.1).</summary>
    public string? IdempotencyKey { get; set; }

    public string? RequestFingerprint { get; set; }

    public Guid CorrelationId { get; set; }

    /// <summary>
    /// §4.6 / §8.16: <c>true</c> while a serial-controlled task is Open / InProgress (the partial unique index allows ONE open task per serial
    /// bucket); unset (null) otherwise — the index holds only equality filters.
    /// </summary>
    public bool? OpenSerialSlot { get; set; }

    /// <summary>The last timeline <c>Seq</c> written for this task (every timeline row of a write takes the next one, in the same CAS write).</summary>
    public int EventSeq { get; set; }

    public WarehouseTask Copy()
    {
        var copy = (WarehouseTask)MemberwiseClone();
        copy.Confirmations = Confirmations.Select(c => c.Copy()).ToList();
        return copy;
    }
}

/// <summary>
/// §4.3 — the insert-only timeline of a task (<c>wms_task_events</c>), written in the SAME transaction as the write it describes, only when
/// that write changed the task. <see cref="Data"/> carries what the central record does not (values as text; lists as JSON). The request key,
/// operation and fingerprint are stored on the command's key event and never on the wire (WMS draft §3.4).
/// </summary>
public sealed class WarehouseTaskEvent : EntityBase
{
    public Guid TaskId { get; set; }

    public int Seq { get; set; }

    public WarehouseTaskEventKind Kind { get; set; }

    /// <summary>The acting user; null for a system write (the sweeper's defensive branch).</summary>
    public Guid? ActorUserId { get; set; }

    public string? ActorName { get; set; }

    public bool System { get; set; }

    public DateTimeOffset AtUtc { get; set; }

    public Dictionary<string, string?> Data { get; set; } = [];

    public Guid CorrelationId { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? RequestOp { get; set; }

    public string? RequestFingerprint { get; set; }
}
