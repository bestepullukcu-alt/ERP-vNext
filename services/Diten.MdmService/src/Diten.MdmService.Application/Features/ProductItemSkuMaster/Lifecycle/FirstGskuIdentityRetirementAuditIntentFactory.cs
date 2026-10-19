using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class FirstGskuIdentityRetirementAuditIntentFactory
{
    public static LocalAuditIntent CreateGsku(
        Gsku gsku, int expectedVersion, Guid operationId, string operationFingerprint,
        Guid actorId, string reasonCode, DateTimeOffset timestampUtc) =>
        Create(gsku, AuditAggregateType.Gsku, ProductAuditOperation.GskuIdentityRetired,
            gsku.Id, expectedVersion, operationId, operationFingerprint,
            actorId, reasonCode, timestampUtc);

    public static LocalAuditIntent CreateRevision(
        ProductDefinitionRevision revision, int expectedVersion, Guid operationId,
        string operationFingerprint, Guid actorId, string reasonCode, DateTimeOffset timestampUtc) =>
        Create(revision, AuditAggregateType.ProductDefinitionRevision,
            ProductAuditOperation.ProductDefinitionRevisionIdentityRetired,
            revision.Id, expectedVersion, operationId, operationFingerprint,
            actorId, reasonCode, timestampUtc);

    private static LocalAuditIntent Create(
        EntityBase aggregate, AuditAggregateType aggregateType, ProductAuditOperation operation,
        Guid aggregateId, int expectedVersion, Guid operationId, string operationFingerprint,
        Guid actorId, string reasonCode, DateTimeOffset timestampUtc)
    {
        if (aggregate.TenantId == Guid.Empty || aggregateId == Guid.Empty || expectedVersion < 0
            || operationId == Guid.Empty || actorId == Guid.Empty
            || !Exact(operationFingerprint, 64) || !Exact(reasonCode, 128)
            || timestampUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("First GSKU retirement audit facts are invalid.");
        }

        var operationKey = operationId.ToString("D");
        var postVersion = checked(expectedVersion + 1);
        var evidence = EncodeFacts(
            aggregate.TenantId.ToString("D"), aggregateId.ToString("D"), aggregateType,
            operation, actorId.ToString("D"), reasonCode, operationFingerprint,
            timestampUtc.ToString("O"));
        return new LocalAuditIntent
        {
            IntentId = DeterministicGuid(
                $"{aggregate.TenantId:D}|{aggregateType}|{aggregateId:D}|{operation}|{operationKey}"),
            TenantId = aggregate.TenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            PreVersion = expectedVersion,
            PostVersion = postVersion,
            Operation = operation,
            ActorId = actorId.ToString("D"),
            CorrelationId = operationKey,
            CausationId = operationKey,
            CommandId = operationKey,
            Sequence = postVersion,
            TimestampUtc = timestampUtc,
            TimestampUtcTicksV1 = timestampUtc.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(evidence)),
            SnapshotReference = $"{aggregateType}/{aggregateId:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = operationKey
        };
    }

    private static byte[] EncodeFacts(params object[] facts)
    {
        using var stream = new MemoryStream();
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var fact in facts)
        {
            var bytes = Encoding.UTF8.GetBytes(Convert.ToString(
                fact, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            stream.Write(length);
            stream.Write(bytes);
        }
        return stream.ToArray();
    }

    private static bool Exact(string value, int length) => value.Length is > 0
        && value.Length <= length && value == value.Trim() && value.All(character => !char.IsControl(character));

    private static Guid DeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
