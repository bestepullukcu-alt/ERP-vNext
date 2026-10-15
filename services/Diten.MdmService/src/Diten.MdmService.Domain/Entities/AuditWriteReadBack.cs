namespace Diten.MdmService.Domain.Entities;

/// <summary>
/// Did a write that carried this audit intent land on the aggregate? Its intent is there — or, once the intent was
/// delivered and compacted, its receipt is (the receipt keeps the intent's id, which is derived from the write, its
/// idempotency key and its evidence). GskuRepository's rule, shared with the identity read-backs by WP-MDM-GSKU-ACCEPT-01
/// FIX2 item 7: before it, a delivered intent made a finished withdrawal or decision look unfinished and quarantined it.
/// </summary>
public static class AuditWriteReadBack
{
    public static bool Persisted(
        IAuditIntentAggregate aggregate,
        LocalAuditIntent expected,
        Func<LocalAuditIntent, bool> exactIntent)
    {
        var intents = aggregate.AuditIntents.Count(exactIntent);
        if (intents == 1) return true;
        if (intents > 1 || aggregate.AuditIntents.Any(x => x.IdempotencyKey == expected.IdempotencyKey)) return false;
        return aggregate.AuditIntentReceipts.Count(receipt =>
            receipt.IdempotencyKey == expected.IdempotencyKey
            && receipt.IntentId == expected.IntentId
            && receipt.TenantId == expected.TenantId
            && receipt.EvidenceHash == expected.EvidenceHash
            && !string.IsNullOrWhiteSpace(receipt.CentralAcknowledgement)
            && !string.IsNullOrWhiteSpace(receipt.CompactReceiptReference)) == 1;
    }
}
