using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

// FG FIX1 item 1 (MOD-0290 s21.2): a Draft FG is cancelled by the key that creates it (the LSKU precedent; the
// package's own `mdm.finished-goods.cancel-draft` key needs an Auth role-matrix change this round does not open).
public static class FinishedGoodDraftCancellationPermissions
{
    public const string CancelDraft = "mdm.finished-goods.create";
}

public sealed record CancelFinishedGoodDraftCommand(Guid FinishedGoodId, int ExpectedVersion, Guid OperationId, string ReasonCode)
    : IRequest<Response<FinishedGoodDraftCancellationResult>>, IAuditableCommand, IAuditMetadataProvider,
      // WP-MDM-SCOPE-INT-01 (CT decision): born fenced — no product write while the scope rollout measures the inventory.
      global::Diten.MdmService.Application.Features.ProductLegalEntityScopes.IProductLegalEntityScopeInventoryMutation
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Update,
        EntityType: "FinishedGood", SourceModule: "product-item-sku-master");
}

public sealed record FinishedGoodDraftCancellationResult(
    Guid FinishedGoodId, int Version, FinishedGoodCancellationDisposition Disposition, bool IsReplay);

/// <summary>
/// FG FIX1 item 1 (s21.5): when a cancelled Draft FG stops counting. Only a cancellation whose intent has been delivered
/// and compacted into EXACTLY ONE receipt that validates against the decision (tenant, source, idempotency, evidence,
/// central acknowledgement and key, compaction reference) is "Cancelled". Anything else — the intent still embedded,
/// no receipt, two receipts, a receipt that does not match, a decision without its evidence — is "PendingAudit" and
/// keeps blocking the parent. A flag alone is never trusted.
/// </summary>
public static class FinishedGoodDraftCancellationProof
{
    public static FinishedGoodCancellationDisposition Disposition(FinishedGood finishedGood)
    {
        ArgumentNullException.ThrowIfNull(finishedGood);
        if (finishedGood.DraftCancellation is not { } cancellation) return FinishedGoodCancellationDisposition.None;
        // FIX1 review m4: TOTAL — a corrupt document (no tenant, missing facts) is PendingAudit, never an exception
        // that loses a whole count or blocker read.
        if (finishedGood.TenantId == Guid.Empty || finishedGood.Id == Guid.Empty
            || cancellation.IntentId == Guid.Empty || string.IsNullOrEmpty(cancellation.IdempotencyKey)
            || string.IsNullOrEmpty(cancellation.EvidenceHash) || cancellation.OperationId == Guid.Empty
            || cancellation.RequesterSubjectId == Guid.Empty || string.IsNullOrWhiteSpace(cancellation.ReasonCode)
            || cancellation.BaseVersion < 0 || cancellation.BaseVersion == int.MaxValue
            || finishedGood.AuditIntents.Any(x => x.IntentId == cancellation.IntentId))
            return FinishedGoodCancellationDisposition.PendingAudit;
        // FIX1 review m6: the decision's facts derived again from the FG and compared, before any receipt is read.
        var expected = FinishedGoodDraftCancellationAuditIntentFactory.Expected(finishedGood, cancellation);
        if (expected.IntentId != cancellation.IntentId || expected.IdempotencyKey != cancellation.IdempotencyKey
            || expected.EvidenceHash != cancellation.EvidenceHash || expected.Fingerprint != cancellation.Fingerprint)
            return FinishedGoodCancellationDisposition.PendingAudit;
        var receipts = finishedGood.AuditIntentReceipts.Where(x => x.IntentId == cancellation.IntentId).Take(2).ToArray();
        if (receipts.Length != 1) return FinishedGoodCancellationDisposition.PendingAudit;
        var receipt = receipts[0];
        return receipt.TenantId == finishedGood.TenantId
               && receipt.SourceService == AuditIntentContract.SourceService
               && receipt.IdempotencyKey == cancellation.IdempotencyKey
               && receipt.EvidenceHash == cancellation.EvidenceHash
               && receipt.ContractVersion == AuditIntentDeliveryProcessor.RequiredContractVersion
               && !string.IsNullOrWhiteSpace(receipt.CentralAcknowledgement)
               && !string.IsNullOrWhiteSpace(receipt.CompactReceiptReference)
               && receipt.CentralIdempotencyKey == AuditIntentContract.BuildCentralIdempotencyKey(
                   finishedGood.TenantId, cancellation.IntentId, AuditIntentDeliveryProcessor.RequiredContractVersion)
            ? FinishedGoodCancellationDisposition.Cancelled
            : FinishedGoodCancellationDisposition.PendingAudit;
    }
}

// FG FIX1 item 1: the 72 intent (FinishedGoodDraftCancelled) — the irreversible accepted decision; its receipt is the
// completion evidence, not a second business event.
public static class FinishedGoodDraftCancellationAuditIntentFactory
{
    // FIX1 review m1: the actor is part of the request — another actor with the same key is a conflict, not a replay.
    public static string Fingerprint(Guid tenantId, Guid finishedGoodId, int expectedVersion, string reasonCode,
        Guid actorId) =>
        Convert.ToHexString(SHA256.HashData(Encode(tenantId.ToString("D"), finishedGoodId.ToString("D"),
            expectedVersion, reasonCode, actorId.ToString("D")))).ToLowerInvariant();

    // FIX1 review m6: the facts a cancellation of this FG must carry, derived again from the FG's immutable data and
    // the decision's own (operation, actor, reason, base version) — never trusted from the stored copies alone.
    public static (Guid IntentId, string IdempotencyKey, string EvidenceHash, string Fingerprint) Expected(
        FinishedGood finishedGood, FinishedGoodDraftCancellation cancellation)
    {
        var intent = Create(finishedGood, cancellation.BaseVersion, cancellation.OperationId,
            cancellation.RequesterSubjectId, cancellation.ReasonCode, DateTimeOffset.UnixEpoch);
        return (intent.IntentId, intent.IdempotencyKey, intent.EvidenceHash,
            Fingerprint(finishedGood.TenantId, finishedGood.Id, cancellation.BaseVersion, cancellation.ReasonCode,
                cancellation.RequesterSubjectId));
    }

    public static LocalAuditIntent Create(
        FinishedGood finishedGood, int expectedVersion, Guid operationId, Guid actorId, string reasonCode,
        DateTimeOffset timestampUtc)
    {
        if (finishedGood.Id == Guid.Empty || finishedGood.TenantId == Guid.Empty || expectedVersion < 0
            || operationId == Guid.Empty || actorId == Guid.Empty || string.IsNullOrWhiteSpace(reasonCode)
            || timestampUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Finished Good cancellation audit facts are invalid.");
        }

        var key = $"finished-good-draft-cancel:{operationId:D}";
        var postVersion = checked(expectedVersion + 1);
        return new LocalAuditIntent
        {
            IntentId = DeterministicGuid(
                $"{finishedGood.TenantId:D}|{finishedGood.Id:D}|{ProductAuditOperation.FinishedGoodDraftCancelled}|{key}"),
            TenantId = finishedGood.TenantId,
            AggregateType = AuditAggregateType.FinishedGood,
            AggregateId = finishedGood.Id,
            PreVersion = expectedVersion,
            PostVersion = postVersion,
            Operation = ProductAuditOperation.FinishedGoodDraftCancelled,
            ActorId = actorId.ToString("D"),
            // Deliverable by the existing generic G4 worker (it claims only an intent with the contract version and a
            // GUID correlation): the cancellation is finished by its receipt, so its intent must be able to reach one.
            ContractVersion = AuditIntentDeliveryProcessor.RequiredContractVersion,
            CorrelationId = operationId.ToString("D"),
            CausationId = operationId.ToString("D"),
            CommandId = key,
            Sequence = postVersion,
            TimestampUtc = timestampUtc,
            TimestampUtcTicksV1 = timestampUtc.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encode(finishedGood.TenantId.ToString("D"),
                finishedGood.Id.ToString("D"), operationId.ToString("D"), actorId.ToString("D"), reasonCode,
                finishedGood.CanonicalCode, finishedGood.CreationCommandId))),
            SnapshotReference = $"FinishedGood/{finishedGood.Id:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = key
        };
    }

    private static byte[] Encode(params object?[] facts)
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
            var bytes = Encoding.UTF8.GetBytes(fact is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : fact.ToString() ?? string.Empty);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            stream.Write(length);
            stream.Write(bytes);
        }
        return stream.ToArray();
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0F) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3F) | 0x80);
        return new Guid(guid);
    }
}
