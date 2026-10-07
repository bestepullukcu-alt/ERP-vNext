using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// FG FIX1 review m4 / m6: the cancellation proof is TOTAL — a corrupt or partial document is PendingAudit, never an
/// exception that would lose a whole gauge count or blocker read — and the decision's facts are derived again from the
/// Finished Good, so a forged copy next to a valid receipt is not Cancelled.
/// </summary>
public sealed class FinishedGoodDraftCancellationProofTests
{
    [Fact]
    public void A_valid_decision_with_its_one_matching_receipt_is_cancelled()
    {
        Assert.Equal(FinishedGoodCancellationDisposition.Cancelled, FinishedGoodDraftCancellationProof.Disposition(Settled()));
        Assert.Equal(FinishedGoodCancellationDisposition.None,
            FinishedGoodDraftCancellationProof.Disposition(new FinishedGood { Id = Guid.NewGuid(), TenantId = Guid.NewGuid() }));
    }

    public static TheoryData<string> Corruptions => new()
    {
        "no-tenant", "no-id", "no-intent", "no-key", "no-hash", "no-operation", "no-requester", "no-reason",
        "null-reason", "negative-version", "max-version", "null-code", "null-command", "other-reason", "other-actor",
        "other-operation", "other-version", "junk-fingerprint", "null-fingerprint", "no-receipt", "two-receipts",
        "intent-still-embedded"
    };

    [Theory]
    [MemberData(nameof(Corruptions))]
    public void A_corrupt_or_forged_document_is_pending_never_an_exception(string corruption)
    {
        var fg = Settled();
        var c = fg.DraftCancellation!;
        switch (corruption)
        {
            case "no-tenant": fg.TenantId = Guid.Empty; break;
            case "no-id": fg.Id = Guid.Empty; break;
            case "no-intent": c.IntentId = Guid.Empty; break;
            case "no-key": c.IdempotencyKey = ""; break;
            case "no-hash": c.EvidenceHash = ""; break;
            case "no-operation": c.OperationId = Guid.Empty; break;
            case "no-requester": c.RequesterSubjectId = Guid.Empty; break;
            case "no-reason": c.ReasonCode = " "; break;
            case "null-reason": c.ReasonCode = null!; break;
            case "negative-version": c.BaseVersion = -1; break;
            case "max-version": c.BaseVersion = int.MaxValue; break;
            case "null-code": fg.CanonicalCode = null!; break;
            case "null-command": fg.CreationCommandId = null!; break;
            case "other-reason": c.ReasonCode = "DUPLICATE"; break;
            case "other-actor": c.RequesterSubjectId = Guid.NewGuid(); break;
            case "other-operation": c.OperationId = Guid.NewGuid(); break;
            case "other-version": c.BaseVersion += 1; break;
            case "junk-fingerprint": c.Fingerprint = new string('0', 64); break;
            case "null-fingerprint": c.Fingerprint = null!; break;
            case "no-receipt": fg.AuditIntentReceipts.Clear(); break;
            case "two-receipts": fg.AuditIntentReceipts.Add(fg.AuditIntentReceipts[0]); break;
            case "intent-still-embedded": fg.AuditIntents.Add(new LocalAuditIntent { IntentId = c.IntentId }); break;
        }

        var disposition = FinishedGoodDraftCancellationProof.Disposition(fg);

        Assert.Equal(FinishedGoodCancellationDisposition.PendingAudit, disposition);
    }

    private static FinishedGood Settled()
    {
        var fg = new FinishedGood
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), CanonicalCode = "FG-000001",
            CreationCommandId = "FG-" + Guid.NewGuid().ToString("N").ToUpperInvariant()
        };
        var operation = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var intent = FinishedGoodDraftCancellationAuditIntentFactory.Create(fg, 3, operation, actor, "CREATED_BY_MISTAKE",
            DateTimeOffset.UtcNow);
        fg.DraftCancellation = new FinishedGoodDraftCancellation
        {
            OperationId = operation,
            Fingerprint = FinishedGoodDraftCancellationAuditIntentFactory.Fingerprint(fg.TenantId, fg.Id, 3,
                "CREATED_BY_MISTAKE", actor),
            IntentId = intent.IntentId, IdempotencyKey = intent.IdempotencyKey, EvidenceHash = intent.EvidenceHash,
            ReasonCode = "CREATED_BY_MISTAKE", RequesterSubjectId = actor, BaseVersion = 3,
            RequestedAtUtc = DateTimeOffset.UtcNow
        };
        fg.AuditIntentReceipts.Add(new LocalAuditIntentReceipt
        {
            SourceService = AuditIntentContract.SourceService, IntentId = intent.IntentId, TenantId = fg.TenantId,
            IdempotencyKey = intent.IdempotencyKey, EvidenceHash = intent.EvidenceHash,
            CentralAcknowledgement = "ack", CompactReceiptReference = "ack",
            ContractVersion = AuditIntentDeliveryProcessor.RequiredContractVersion,
            CentralIdempotencyKey = AuditIntentContract.BuildCentralIdempotencyKey(fg.TenantId, intent.IntentId,
                AuditIntentDeliveryProcessor.RequiredContractVersion)
        });
        return fg;
    }
}
