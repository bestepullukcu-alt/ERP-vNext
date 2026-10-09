namespace Diten.MdmService.Application.Contracts.Audit;

public sealed record TrustedSourceAuditIntentEnvelope(
    string SourceService,
    string ContractVersion,
    Guid IntentId,
    Guid TenantId,
    string AggregateType,
    Guid AggregateId,
    int PreVersion,
    int PostVersion,
    string Operation,
    string ActorId,
    Guid CorrelationId,
    string CausationId,
    string CommandId,
    long Sequence,
    DateTimeOffset TimestampUtc,
    string EvidenceHash,
    string? SnapshotReference,
    string IdempotencyKey);
