using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public sealed record AuditIntentClaimedPayload(
    string SourceService,
    string ContractVersion,
    Guid IntentId,
    Guid TenantId,
    AuditAggregateType AggregateType,
    Guid AggregateId,
    int PreVersion,
    int PostVersion,
    ProductAuditOperation Operation,
    string ActorId,
    Guid CorrelationId,
    string CausationId,
    string CommandId,
    long Sequence,
    DateTimeOffset TimestampUtc,
    string EvidenceHash,
    string? SnapshotReference,
    string IdempotencyKey);
