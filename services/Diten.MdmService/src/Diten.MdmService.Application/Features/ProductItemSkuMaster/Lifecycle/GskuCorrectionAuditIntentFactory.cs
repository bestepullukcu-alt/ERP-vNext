using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class GskuCorrectionAuditIntentFactory
{
    public static LocalAuditIntent Create(Gsku gsku, int expectedVersion, Guid operationId,
        Guid actorId, ProductAuditOperation operation, decimal quantity, string uomCode,
        ReferenceCatalogSelection applicabilitySelection, ReferenceCatalogSelection uomSelection,
        DateTimeOffset timestampUtc)
    {
        if (gsku.Id == Guid.Empty || gsku.TenantId == Guid.Empty || operationId == Guid.Empty
            || actorId == Guid.Empty || expectedVersion < 0 || quantity <= 0
            || string.IsNullOrWhiteSpace(uomCode) || timestampUtc.Offset != TimeSpan.Zero
            || !ValidSelection(applicabilitySelection, "pack-applicability", "SCALAR_QUANTITY_APPLIES")
            || !ValidSelection(uomSelection, "uom", uomCode)
            || operation is not (ProductAuditOperation.GskuCorrectionRequested
                or ProductAuditOperation.GskuCorrectionApplied
                or ProductAuditOperation.GskuCorrectionRejected
                or ProductAuditOperation.GskuCorrectionManualReconciliationRequired))
            throw new ArgumentException("GSKU_CORRECTION_AUDIT_FACTS_INVALID");
        var key = $"gsku-correction:{operationId:D}:{operation}";
        var evidence = Encoding.UTF8.GetBytes(string.Join('|', gsku.TenantId, gsku.Id,
            expectedVersion, operationId, actorId, operation, quantity, uomCode,
            applicabilitySelection.CatalogVersionId, applicabilitySelection.CatalogVersionNumber,
            applicabilitySelection.ResolvedAtUtc.UtcTicks,
            uomSelection.CatalogVersionId, uomSelection.CatalogVersionNumber,
            uomSelection.ResolvedAtUtc.UtcTicks));
        var postVersion = checked(expectedVersion + 1);
        return new()
        {
            IntentId = DeterministicGuid($"{gsku.TenantId:D}|{gsku.Id:D}|{operation}|{operationId:D}"),
            TenantId = gsku.TenantId, AggregateType = AuditAggregateType.Gsku, AggregateId = gsku.Id,
            PreVersion = expectedVersion, PostVersion = postVersion, Operation = operation,
            ActorId = actorId.ToString("D"), CorrelationId = operationId.ToString("D"),
            CausationId = operationId.ToString("D"), CommandId = key, Sequence = postVersion,
            TimestampUtc = timestampUtc, TimestampUtcTicksV1 = timestampUtc.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(evidence)),
            SnapshotReference = $"Gsku/{gsku.Id:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending, IdempotencyKey = key
        };
    }

    private static Guid DeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16]; hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50); bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new(bytes);
    }

    private static bool ValidSelection(ReferenceCatalogSelection x, string set, string value) =>
        x.SetCode == set && x.ValueCode == value && x.CatalogVersionId != Guid.Empty
        && x.CatalogVersionNumber > 0 && x.ResolutionMode == ReferenceCatalogResolutionMode.Latest
        && x.ResolvedAtUtc.Offset == TimeSpan.Zero;
}
