using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class LocalAuditIntent
{
    public string SourceService { get; set; } = AuditIntentContract.SourceService;
    public int SchemaVersion { get; set; } = 1;
    public string? ContractVersion { get; set; }
    public Guid IntentId { get; set; }
    public Guid TenantId { get; set; }
    public AuditAggregateType AggregateType { get; set; }
    public Guid AggregateId { get; set; }
    public int PreVersion { get; set; }
    public int PostVersion { get; set; }
    public ProductAuditOperation Operation { get; set; }
    // MOD-0289 (KARAR D24/2, D142): the operation of a master data intent. Null for every product intent — and then the
    // element is not written (ShouldSerializeMasterDataOperation; the Domain project has no Bson reference for an attribute),
    // so a product intent's stored shape is unchanged. When set, it is the operation; Operation stays at its default.
    public MasterDataAuditOperation? MasterDataOperation { get; set; }

    // The driver's AutoMap picks up ShouldSerialize<Member>: no element for a product intent.
    public bool ShouldSerializeMasterDataOperation() => MasterDataOperation is not null;
    public string ActorId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string CausationId { get; set; } = string.Empty;
    public string CommandId { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public long? TimestampUtcTicksV1 { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
    public string? SnapshotReference { get; set; }
    public AuditIntentDeliveryState DeliveryState { get; set; } = AuditIntentDeliveryState.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public long? NextRetryAtUtcTicksV1 { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? CentralAcknowledgement { get; set; }
    public string? CentralIdempotencyKey { get; set; }
    public string? AcknowledgedContractVersion { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? LastError { get; set; }
    public string? LeaseOwner { get; set; }
    public string? ClaimToken { get; set; }
    public long ClaimGeneration { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public long? LeaseUntilUtcTicksV1 { get; set; }
    public int? TemporalStorageVersion { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? DeadLetteredAt { get; set; }
    public DateTimeOffset? CompactedAt { get; set; }
    public string? CompactReceiptReference { get; set; }
    public AuditIntentFailureClass FailureClass { get; set; }
    public string? FailureReason { get; set; }
}
