using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;

public static class ProductAbbreviationAuditIntentFactory
{
    public static LocalAuditIntent Create(
        ProductAbbreviationRegisterEntry entry,
        ProductAbbreviationHistoryEntry history)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(history);
        if (entry.Id == Guid.Empty || entry.TenantId == Guid.Empty || entry.Version < 0
            || history.RegisterEntryId != entry.Id || history.TenantId != entry.TenantId
            || history.OccurredAtUtc.Offset != TimeSpan.Zero
            || !Guid.TryParseExact(history.CanonicalHumanSubjectId, "D", out _)
            || string.IsNullOrWhiteSpace(history.IdempotencyKey)
            || string.IsNullOrWhiteSpace(history.CorrelationId))
        {
            throw new ArgumentException("Product abbreviation audit facts are invalid.");
        }

        var operation = history.EventType switch
        {
            ProductAbbreviationHistoryEventType.ALLOCATION_REQUESTED
                => ProductAuditOperation.ProductAbbreviationAllocationRequested,
            ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED
                => ProductAuditOperation.ProductAbbreviationAllocationApproved,
            ProductAbbreviationHistoryEventType.ALLOCATION_REJECTED
                => ProductAuditOperation.ProductAbbreviationAllocationRejected,
            ProductAbbreviationHistoryEventType.ALLOCATION_CANCELLED
                => ProductAuditOperation.ProductAbbreviationAllocationCancelled,
            ProductAbbreviationHistoryEventType.CORRECTION_REQUESTED
                => ProductAuditOperation.ProductAbbreviationCorrectionRequested,
            ProductAbbreviationHistoryEventType.CORRECTION_APPROVED
                => ProductAuditOperation.ProductAbbreviationCorrectionApproved,
            ProductAbbreviationHistoryEventType.CORRECTION_REJECTED
                => ProductAuditOperation.ProductAbbreviationCorrectionRejected,
            ProductAbbreviationHistoryEventType.CORRECTION_CANCELLED
                => ProductAuditOperation.ProductAbbreviationCorrectionCancelled,
            ProductAbbreviationHistoryEventType.RETIREMENT_REQUESTED
                => ProductAuditOperation.ProductAbbreviationRetirementRequested,
            ProductAbbreviationHistoryEventType.RETIREMENT_APPROVED
                => ProductAuditOperation.ProductAbbreviationRetirementApproved,
            ProductAbbreviationHistoryEventType.RETIREMENT_REJECTED
                => ProductAuditOperation.ProductAbbreviationRetirementRejected,
            _ => throw new ArgumentOutOfRangeException(nameof(history), history.EventType,
                "Unsupported product abbreviation audit event.")
        };

        var intentId = DeterministicGuid(
            $"{entry.TenantId:D}|{entry.Id:D}|{operation}|{history.IdempotencyKey}");
        return new LocalAuditIntent
        {
            IntentId = intentId,
            TenantId = entry.TenantId,
            AggregateType = AuditAggregateType.ProductAbbreviation,
            AggregateId = entry.Id,
            PreVersion = entry.Version - 1,
            PostVersion = entry.Version,
            Operation = operation,
            ActorId = history.CanonicalHumanSubjectId,
            ContractVersion = "mod-0290.audit-intent.v1",
            CorrelationId = Guid.TryParseExact(history.CorrelationId, "D", out var correlationId)
                && correlationId != Guid.Empty
                    ? correlationId.ToString("D")
                    : DeterministicGuid(history.CorrelationId).ToString("D"),
            CausationId = history.IdempotencyKey,
            CommandId = history.IdempotencyKey,
            Sequence = entry.Version,
            TimestampUtc = history.OccurredAtUtc,
            TimestampUtcTicksV1 = history.OccurredAtUtc.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(EncodeFacts(entry, history))),
            SnapshotReference = $"ProductAbbreviation/{entry.Id:N}/{entry.Version}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = history.IdempotencyKey
        };
    }

    private static byte[] EncodeFacts(
        ProductAbbreviationRegisterEntry entry,
        ProductAbbreviationHistoryEntry history)
    {
        object?[] facts =
        [
            entry.TenantId.ToString("D"), entry.Id.ToString("D"), entry.GlobalProductId.ToString("D"),
            entry.NormalizedAbbreviation, history.EventType, history.BeforeStatus, history.AfterStatus,
            history.CanonicalHumanSubjectId, history.ActorType, history.IdempotencyKey, history.CorrelationId,
            history.Reason, history.EvidenceHash, history.OccurredAtUtc.ToString("O")
        ];
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

            var bytes = Encoding.UTF8.GetBytes(fact.ToString() ?? string.Empty);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            stream.Write(length);
            stream.Write(bytes);
        }

        return stream.ToArray();
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
