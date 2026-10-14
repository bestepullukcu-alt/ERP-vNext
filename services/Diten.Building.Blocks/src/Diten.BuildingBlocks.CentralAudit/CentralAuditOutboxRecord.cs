namespace Diten.BuildingBlocks.CentralAudit;

/// <summary>
/// One central-audit event waiting in (or delivered from) the SERVICE's own outbox collection
/// (<see cref="CentralAuditOutboxRecord.CollectionName"/>). <see cref="Id"/> is the event id: Platform files the event once
/// per id, so a resend after a lost acknowledgement is harmless.
///
/// <para>A row is never deleted by this package. Delivered rows stay with <see cref="CentralAuditOutboxStatus.Delivered"/>;
/// a row Platform refuses stays with <see cref="CentralAuditOutboxStatus.Rejected"/> and is counted by the health check.</para>
/// <para>Its BSON shape is fixed by <see cref="MongoCentralAuditOutboxStore"/> (Guid = string, enum = name), not by
/// the host service's global conventions: every service files the same shape.</para>
/// </summary>
public sealed class CentralAuditOutboxRecord
{
    public const string CollectionName = "central_audit_outbox";

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid? CompanyId { get; set; }

    public Guid? ActorId { get; set; }

    public string? ActorDisplayName { get; set; }

    public string ActorType { get; set; } = CentralAuditActorTypes.TenantUser;

    public string SourceService { get; set; } = string.Empty;

    public string SourceModule { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    /// <summary>The command's type name (Platform <c>RequestType</c>).</summary>
    public string RequestType { get; set; } = string.Empty;

    public Guid CorrelationId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    /// <summary>True when the row was written inside the command's own Mongo transaction (committed with it or not at all).</summary>
    public bool WrittenInCommandTransaction { get; set; }

    /// <summary>
    /// False only for a row the dispatcher released as an orphan: its transaction committed but its command never confirmed
    /// the end. Delivered with Platform outcome <c>Unconfirmed</c>, never <c>Succeeded</c> (CT HOLD 2026-10-14, item 3).
    /// </summary>
    public bool ConfirmedByCommand { get; set; } = true;

    public CentralAuditOutboxStatus Status { get; set; } = CentralAuditOutboxStatus.Pending;

    public int Attempts { get; set; }

    public DateTime NextAttemptAtUtc { get; set; }

    public string? LastError { get; set; }

    public DateTime? DeliveredAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    internal CentralAuditOutboxRecord Copy() => (CentralAuditOutboxRecord)MemberwiseClone();
}

public enum CentralAuditOutboxStatus
{
    /// <summary>Waiting for delivery (first attempt or a retry after a transient failure).</summary>
    Pending = 0,

    /// <summary>Platform accepted it (queued or already had it).</summary>
    Delivered = 1,

    /// <summary>Platform refused it (400): retrying cannot help. The row stays; health reports it; a person looks.</summary>
    Rejected = 2,

    /// <summary>
    /// Written in the command's transaction; the command has not finished yet. The behavior turns it into
    /// <see cref="Pending"/> when the command ends (succeeded / failed). A row the command never confirmed (the process
    /// died after the commit) is released by the dispatcher after <c>OrphanAfter</c> with
    /// <see cref="CentralAuditOutboxRecord.ConfirmedByCommand"/> = false — delivered as Platform outcome <c>Unconfirmed</c>,
    /// never as succeeded — with a warning; it is never dropped.
    /// </summary>
    AwaitingCommand = 3,

    /// <summary>
    /// Written in a transaction of a command that then FAILED (a transaction can commit without business data — "reservation
    /// not found" — or before a later step fails). A failed command has no central record, so the row is never delivered;
    /// it stays as local evidence next to the module's own history.
    /// </summary>
    Abandoned = 4
}
