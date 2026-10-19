using Diten.Platform.Application.Contracts.Audit;

namespace Diten.Platform.API.Models.Audit;

public sealed record TrustedSourceAuditIntentRequest(
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
    string IdempotencyKey)
{
    public TrustedSourceAuditIntentEnvelope ToEnvelope() => new(
        SourceService,
        ContractVersion,
        IntentId,
        TenantId,
        AggregateType,
        AggregateId,
        PreVersion,
        PostVersion,
        Operation,
        ActorId,
        CorrelationId,
        CausationId,
        CommandId,
        Sequence,
        TimestampUtc,
        EvidenceHash,
        SnapshotReference,
        IdempotencyKey);
}
