using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// ABB FIX3 item 8 — the register's own write paths (one transaction each: RequestEntryAsync, DecideAsync), with
/// evidence built the way the workflow builds it. Tests seed and decide through these, never through a write the
/// production code does not have.
/// </summary>
internal static class ProductAbbreviationTestWrites
{
    public static ProductAbbreviationAllocationLedger Allocation(
        string abbreviation,
        string idempotencyKey,
        Guid productId,
        Guid entryId,
        string allocatedBy)
        => new()
        {
            // As the workflow does: the ledger row's id follows from the key, so a repeated request names the same row.
            Id = new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes("ledger|" + idempotencyKey))),
            NormalizedAbbreviation = abbreviation,
            GlobalProductId = productId,
            RegisterEntryId = entryId,
            IdempotencyKey = idempotencyKey,
            PayloadHash = $"{abbreviation}|{productId:N}|{entryId:N}",
            AllocationState = ProductAbbreviationAllocationState.DURABLY_ALLOCATED,
            AllocatedByCanonicalSubjectId = allocatedBy,
            AllocatedAtUtc = DateTimeOffset.UtcNow
        };

    public static ProductAbbreviationDecisionEvidence Evidence(
        ProductAbbreviationRegisterEntry stored,
        ProductAbbreviationHistoryEventType eventType,
        string idempotencyKey,
        string actor)
    {
        var history = new ProductAbbreviationHistoryEntry
        {
            Id = Guid.NewGuid(),
            TenantId = stored.TenantId,
            RegisterEntryId = stored.Id,
            GlobalProductId = stored.GlobalProductId,
            NormalizedAbbreviation = stored.NormalizedAbbreviation,
            EventType = eventType,
            CanonicalHumanSubjectId = actor,
            ActorType = "tenant_user",
            IdempotencyKey = idempotencyKey,
            CorrelationId = idempotencyKey,
            EvidenceHash = idempotencyKey,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            PostVersion = stored.Version
        };
        return new(history, ProductAbbreviationAuditIntentFactory.Create(stored, history));
    }

    public static Task<ProductAbbreviationRegisterWriteResult> RequestAsync(
        IProductAbbreviationRegisterRepository register,
        Guid productId,
        string abbreviation,
        string idempotencyKey,
        string requestedBy,
        Guid? replacesEntryId = null,
        int? expectedFormerVersion = null,
        DateTimeOffset? requestedAtUtc = null,
        Guid? entryId = null)
    {
        var id = entryId ?? Guid.NewGuid();
        var entry = new ProductAbbreviationRegisterEntry
        {
            Id = id,
            GlobalProductId = productId,
            NormalizedAbbreviation = abbreviation,
            AllocationIdempotencyKey = idempotencyKey,
            RequestedByCanonicalSubjectId = requestedBy,
            RequestedAtUtc = requestedAtUtc ?? DateTimeOffset.UtcNow,
            ReplacesEntryId = replacesEntryId
        };
        return register.RequestEntryAsync(new ProductAbbreviationRequestWrite(
            entry,
            Allocation(abbreviation, idempotencyKey, productId, id, requestedBy),
            stored => Evidence(
                stored,
                replacesEntryId.HasValue
                    ? ProductAbbreviationHistoryEventType.CORRECTION_REQUESTED
                    : ProductAbbreviationHistoryEventType.ALLOCATION_REQUESTED,
                idempotencyKey,
                requestedBy),
            expectedFormerVersion));
    }

    public static Task<ProductAbbreviationRegisterWriteResult> DecideAsync(
        IProductAbbreviationRegisterRepository register,
        ProductAbbreviationDecisionKind kind,
        Guid entryId,
        int expectedVersion,
        string actor,
        string idempotencyKey,
        string? retirementRequestId = null)
        => register.DecideAsync(new ProductAbbreviationDecision(
            kind,
            entryId,
            expectedVersion,
            actor,
            idempotencyKey,
            "reason",
            DateTimeOffset.UtcNow,
            retirementRequestId,
            post => Evidence(post, EventFor(kind, post), idempotencyKey, actor)));

    private static ProductAbbreviationHistoryEventType EventFor(
        ProductAbbreviationDecisionKind kind,
        ProductAbbreviationRegisterEntry post)
        => kind switch
        {
            ProductAbbreviationDecisionKind.Approve => ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED,
            ProductAbbreviationDecisionKind.Reject => post.ReplacesEntryId.HasValue
                ? ProductAbbreviationHistoryEventType.CORRECTION_REJECTED
                : ProductAbbreviationHistoryEventType.ALLOCATION_REJECTED,
            ProductAbbreviationDecisionKind.Cancel => post.ReplacesEntryId.HasValue
                ? ProductAbbreviationHistoryEventType.CORRECTION_CANCELLED
                : ProductAbbreviationHistoryEventType.ALLOCATION_CANCELLED,
            ProductAbbreviationDecisionKind.RequestRetirement => ProductAbbreviationHistoryEventType.RETIREMENT_REQUESTED,
            ProductAbbreviationDecisionKind.ApproveRetirement => ProductAbbreviationHistoryEventType.RETIREMENT_APPROVED,
            _ => ProductAbbreviationHistoryEventType.RETIREMENT_REJECTED
        };
}
