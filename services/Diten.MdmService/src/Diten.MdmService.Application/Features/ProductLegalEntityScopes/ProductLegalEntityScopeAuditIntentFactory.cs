using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes;

public static class ProductLegalEntityScopeAuditIntentFactory
{
    public static LocalAuditIntent Create(
        ProductLegalEntityScopeRolloutState rollout,
        ProductAuditOperation operation,
        int preVersion,
        int postVersion,
        Guid commandId,
        Guid actorId,
        string evidenceHash,
        DateTimeOffset timestampUtc)
    {
        if (operation is not (ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated
            or ProductAuditOperation.ProductLegalEntityScopeEnforcementSuspended)
            || evidenceHash.Length != 64 || !evidenceHash.All(Uri.IsHexDigit))
            throw new ArgumentException("Rollout audit intent facts are invalid.");
        return new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(), TenantId = rollout.TenantId,
            AggregateType = AuditAggregateType.ProductLegalEntityScopeRolloutState,
            AggregateId = rollout.Id, PreVersion = preVersion, PostVersion = postVersion,
            Operation = operation, ActorId = actorId.ToString("D"),
            CorrelationId = commandId.ToString("D"), CausationId = rollout.CreationCommandId.ToString("D"),
            CommandId = commandId.ToString("D"), Sequence = postVersion + 1L,
            TimestampUtc = timestampUtc, TimestampUtcTicksV1 = timestampUtc.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = evidenceHash.ToUpperInvariant(),
            SnapshotReference = $"ProductLegalEntityScopeRollout/{rollout.Id:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = commandId.ToString("D")
        };
    }

    public static LocalAuditIntent Create(
        ProductLegalEntityScopePolicy policy,
        ProductAuditOperation operation,
        int preVersion,
        int postVersion,
        Guid commandId,
        Guid actorId,
        DateTimeOffset timestampUtc)
    {
        var evidence = string.Join('|',
            policy.TenantId.ToString("D"),
            policy.Id.ToString("D"),
            policy.GlobalProductId.ToString("D"),
            operation.ToString(),
            preVersion,
            postVersion,
            commandId.ToString("D"),
            timestampUtc.ToString("O"));

        return new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(),
            TenantId = policy.TenantId,
            AggregateType = AuditAggregateType.ProductLegalEntityScopePolicy,
            AggregateId = policy.Id,
            PreVersion = preVersion,
            PostVersion = postVersion,
            Operation = operation,
            ActorId = actorId.ToString("D"),
            CorrelationId = commandId.ToString("D"),
            CausationId = policy.CreationCommandId.ToString("D"),
            CommandId = commandId.ToString("D"),
            Sequence = postVersion + 1L,
            TimestampUtc = timestampUtc,
            TimestampUtcTicksV1 = timestampUtc.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))),
            SnapshotReference = $"ProductLegalEntityScopePolicy/{policy.Id:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = commandId.ToString("D")
        };
    }
}
