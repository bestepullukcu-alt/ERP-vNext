using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.BankMaster;

/// <summary>
/// MOD-0289 DL-1 (path b, <c>mdm-guvenilir-kaynak-niyeti</c>) — the audit intent every bank / branch write carries in the
/// SAME document write as its business change. The evidence hash covers WHAT changed by name (changed field names, the
/// status before and after, the reason code) next to who, which command, which versions and when; it never covers a bank
/// name, BIC or any other value. Category <c>MasterData</c> is Platform's mapping (P-12); the operation is the
/// <see cref="MasterDataAuditOperation"/> — <see cref="LocalAuditIntent.Operation"/> stays at its default.
/// </summary>
public static class BankAuditIntentFactory
{
    public static LocalAuditIntent Create(
        AuditAggregateType aggregateType,
        Guid tenantId,
        Guid aggregateId,
        MasterDataAuditOperation operation,
        int preVersion,
        int postVersion,
        Guid commandId,
        Guid correlationId,
        Guid actorId,
        DateTimeOffset timestampUtc,
        string facts)
    {
        if (aggregateType is not (AuditAggregateType.Bank or AuditAggregateType.BankBranch))
            throw new ArgumentException("A bank intent belongs to the Bank or BankBranch aggregate.", nameof(aggregateType));
        if (tenantId == Guid.Empty || aggregateId == Guid.Empty || commandId == Guid.Empty
            || correlationId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("A bank audit intent needs a tenant, an aggregate, a command, a correlation and an actor.");

        var evidence = string.Join('|',
            tenantId.ToString("D"),
            aggregateType.ToString(),
            aggregateId.ToString("D"),
            operation.ToString(),
            preVersion,
            postVersion,
            commandId.ToString("D"),
            actorId.ToString("D"),
            timestampUtc.ToString("O"),
            facts);

        var intent = new LocalAuditIntent
        {
            // The delivery refuses a claimed intent that carries no contract version (the central log's envelope version).
            ContractVersion = AuditIntentDeliveryProcessor.RequiredContractVersion,
            IntentId = Guid.NewGuid(),
            TenantId = tenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            PreVersion = preVersion,
            PostVersion = postVersion,
            MasterDataOperation = operation,
            ActorId = actorId.ToString("D"),
            CorrelationId = correlationId.ToString("D"),
            CausationId = commandId.ToString("D"),
            CommandId = commandId.ToString("D"),
            Sequence = postVersion + 1L,
            TimestampUtc = timestampUtc,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))),
            SnapshotReference = $"{aggregateType}/{aggregateId:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = commandId.ToString("D")
        };
        AuditIntentTemporalStorage.ApplyCurrentVersion(intent);
        return intent;
    }

    /// <summary>The intent's shape as the stores insist on it: exactly one master data operation, current temporal shadow, 64-hex evidence.</summary>
    public static bool IsWellFormed(LocalAuditIntent? intent, AuditAggregateType aggregateType, Guid tenantId, Guid aggregateId) =>
        intent is not null
        && intent.AggregateType == aggregateType
        && intent.TenantId == tenantId && tenantId != Guid.Empty
        && intent.AggregateId == aggregateId
        && intent.MasterDataOperation is not null
        && intent.IntentId != Guid.Empty
        && intent.DeliveryState == AuditIntentDeliveryState.Pending
        && intent.TemporalStorageVersion == AuditIntentTemporalStorage.CurrentVersion
        && intent.EvidenceHash.Length == 64 && intent.EvidenceHash.All(Uri.IsHexDigit)
        && !string.IsNullOrWhiteSpace(intent.IdempotencyKey);
}
