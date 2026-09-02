using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery;

public static class ProductIdentityWorkflowOperationRecoveryAuditIntentFactory
{
    public static IReadOnlyList<LocalAuditIntent> Create(
        Guid tenantId,
        ProductIdentityWorkflowOperationRecoveryScope scope,
        ProductIdentityWorkflowOperationRecoveryEvidence recovery)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(recovery);
        var operation = recovery.Disposition == ProductIdentityWorkflowRecoveryDisposition.Superseded
            ? ProductAuditOperation.ProductIdentityWorkflowOperationSuperseded
            : ProductAuditOperation.ProductIdentityWorkflowOperationAbandonedBeforeWorkflowStart;
        return scope switch
        {
            GlobalProductWorkflowRecoveryScope item =>
                [CreateOne(tenantId, AuditAggregateType.GlobalProduct, item.GlobalProductId,
                    item.GlobalProductVersion, operation, recovery, scope)],
            FirstGskuWorkflowRecoveryScope item =>
                [
                    CreateOne(tenantId, AuditAggregateType.ProductDefinitionRevision,
                        item.ProductDefinitionRevisionId, item.ProductDefinitionRevisionVersion,
                        operation, recovery, scope),
                    CreateOne(tenantId, AuditAggregateType.Gsku, item.GskuId, item.GskuVersion,
                        operation, recovery, scope)
                ],
            LskuWorkflowRecoveryScope item =>
                [CreateOne(tenantId, AuditAggregateType.Lsku, item.LskuId, item.LskuVersion,
                    operation, recovery, scope)],
            FinishedGoodWorkflowRecoveryScope item =>
                [CreateOne(tenantId, AuditAggregateType.FinishedGood, item.FinishedGoodId,
                    item.FinishedGoodVersion, operation, recovery, scope)],
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };
    }

    private static LocalAuditIntent CreateOne(
        Guid tenantId,
        AuditAggregateType aggregateType,
        Guid aggregateId,
        int expectedVersion,
        ProductAuditOperation operation,
        ProductIdentityWorkflowOperationRecoveryEvidence recovery,
        ProductIdentityWorkflowOperationRecoveryScope scope)
    {
        if (tenantId == Guid.Empty || aggregateId == Guid.Empty || expectedVersion < 0
            || recovery.OperatorSubjectId == Guid.Empty || recovery.CommandId == Guid.Empty)
        {
            throw new ArgumentException("Recovery audit facts are invalid.");
        }

        var postVersion = checked(expectedVersion + 1);
        var operationKey = $"product-identity-recovery:{recovery.CommandId:D}:{(int)aggregateType}:{aggregateId:D}";
        var timestamp = new DateTimeOffset(recovery.RecoveredAtUtcTicksV1, TimeSpan.Zero);
        var evidence = EncodeFacts(
            "product-identity-workflow-operation-recovery-audit-v1",
            tenantId.ToString("D"), ((int)scope.Family).ToString(CultureInfo.InvariantCulture),
            ((int)recovery.Disposition).ToString(CultureInfo.InvariantCulture),
            aggregateId.ToString("D"), ((int)aggregateType).ToString(CultureInfo.InvariantCulture),
            expectedVersion.ToString(CultureInfo.InvariantCulture), postVersion.ToString(CultureInfo.InvariantCulture),
            recovery.CommandId.ToString("D"), recovery.OperatorSubjectId.ToString("D"),
            recovery.ReasonCode, recovery.Comment,
            recovery.WorkflowNotFoundEvidenceId.ToString("D"),
            recovery.WorkflowNotFoundEvidenceFingerprint,
            recovery.WorkflowNotFoundObservedAtUtcTicksV1.ToString(CultureInfo.InvariantCulture),
            recovery.RecoveredAtUtcTicksV1.ToString(CultureInfo.InvariantCulture));
        return new LocalAuditIntent
        {
            IntentId = DeterministicGuid($"{tenantId:D}|{aggregateId:D}|{(int)operation}|{operationKey}"),
            TenantId = tenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            PreVersion = expectedVersion,
            PostVersion = postVersion,
            Operation = operation,
            ActorId = recovery.OperatorSubjectId.ToString("D"),
            CorrelationId = recovery.CommandId.ToString("D"),
            CausationId = recovery.CommandId.ToString("D"),
            CommandId = recovery.CommandId.ToString("D"),
            Sequence = postVersion,
            TimestampUtc = timestamp,
            TimestampUtcTicksV1 = timestamp.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(evidence)),
            SnapshotReference = $"{aggregateType}/{aggregateId:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = operationKey
        };
    }

    internal static Guid DeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    internal static string Fingerprint(params object?[] facts) =>
        Convert.ToHexString(SHA256.HashData(EncodeFacts(facts))).ToLowerInvariant();

    private static byte[] EncodeFacts(params object?[] facts)
    {
        using var stream = new MemoryStream();
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var fact in facts)
        {
            if (fact is null)
            {
                BinaryPrimitives.WriteInt32BigEndian(length, -1);
                stream.Write(length);
                continue;
            }
            var value = fact is IFormattable formatted
                ? formatted.ToString(null, CultureInfo.InvariantCulture)
                : fact.ToString() ?? string.Empty;
            var bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            stream.Write(length);
            stream.Write(bytes);
        }
        return stream.ToArray();
    }
}
