using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

/// <summary>An immutable, process-authorized permit, not an application actor or transport credential.</summary>
public sealed class SelectedAuditIntentDeliveryRequest
{
    public SelectedAuditIntentDeliveryRequest(Guid executionId, Guid tenantId,
        IEnumerable<SelectedAuditIntentDeliveryItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var snapshot = items.Take(101).ToArray();
        if (executionId == Guid.Empty || tenantId == Guid.Empty || snapshot.Length is < 1 or > 100
            || snapshot.Any(item => item is null || item.Locator is null
                || item.Locator.TenantId != tenantId || item.Locator.AggregateId == Guid.Empty
                || item.Locator.IntentId == Guid.Empty || !IsAllowed(item.Locator.AggregateType)
                || item.ExpectedClaimGeneration < 0 || item.ExpectedClaimGeneration == long.MaxValue
                || item.EvidenceFingerprint is null || item.EvidenceFingerprint.Length != 64
                || item.EvidenceFingerprint.Any(character => !Uri.IsHexDigit(character)))
            || snapshot.Select(item => item.Locator).Distinct().Count() != snapshot.Length)
            throw new ArgumentException("SELECTED_AUDIT_INTENT_SELECTION_INVALID");
        ExecutionId = executionId;
        TenantId = tenantId;
        Items = Array.AsReadOnly(snapshot);
    }

    public Guid ExecutionId { get; }
    public Guid TenantId { get; }
    public IReadOnlyList<SelectedAuditIntentDeliveryItem> Items { get; }

    public static bool IsAllowed(AuditAggregateType type) => type is
        AuditAggregateType.GlobalProduct or AuditAggregateType.ProductDefinitionRevision
        or AuditAggregateType.Gsku or AuditAggregateType.Lsku or AuditAggregateType.ProductAbbreviation
        or AuditAggregateType.ProductLegalEntityScopePolicy or AuditAggregateType.CodeReservation;
}

public sealed record SelectedAuditIntentDeliveryItem(
    AuditIntentLocator Locator, long ExpectedClaimGeneration, string EvidenceFingerprint);
