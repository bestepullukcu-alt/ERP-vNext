using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class GlobalProductCorrectionAuditIntentFactory
{
    public static LocalAuditIntent Create(
        GlobalProduct product,
        int expectedVersion,
        Guid operationId,
        Guid actorId,
        ProductAuditOperation operation,
        string proposedName,
        DateTimeOffset timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (product.Id == Guid.Empty || product.TenantId == Guid.Empty || operationId == Guid.Empty
            || actorId == Guid.Empty || expectedVersion < 0 || timestampUtc.Offset != TimeSpan.Zero
            || operation is not (ProductAuditOperation.GlobalProductCorrectionRequested
                or ProductAuditOperation.GlobalProductCorrectionApplied
                or ProductAuditOperation.GlobalProductCorrectionRejected
                or ProductAuditOperation.GlobalProductCorrectionManualReconciliationRequired))
            throw new ArgumentException("Global Product correction audit facts are invalid.");
        var key = $"global-product-correction:{operationId:D}:{operation}";
        var evidence = Encoding.UTF8.GetBytes(string.Join('\n',
            product.TenantId.ToString("D"), product.Id.ToString("D"), expectedVersion,
            operationId.ToString("D"), actorId.ToString("D"), operation,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(proposedName))));
        var postVersion = checked(expectedVersion + 1);
        return new()
        {
            IntentId = DeterministicGuid($"{product.TenantId:D}|{product.Id:D}|{operation}|{operationId:D}"),
            TenantId = product.TenantId,
            AggregateType = AuditAggregateType.GlobalProduct,
            AggregateId = product.Id,
            PreVersion = expectedVersion,
            PostVersion = postVersion,
            Operation = operation,
            ActorId = actorId.ToString("D"),
            CorrelationId = operationId.ToString("D"),
            CausationId = operationId.ToString("D"),
            CommandId = key,
            Sequence = postVersion,
            TimestampUtc = timestampUtc,
            TimestampUtcTicksV1 = timestampUtc.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(evidence)),
            SnapshotReference = $"GlobalProduct/{product.Id:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = key
        };
    }

    private static Guid DeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new(bytes);
    }
}
