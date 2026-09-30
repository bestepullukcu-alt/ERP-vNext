namespace Diten.InventoryService.Application.Common;

/// <summary>
/// Scoped caller scope, populated by the scope-resolution middleware from the SIGNED JWT only
/// (<c>tenant_id</c>, <c>legal_entity_id</c>, <c>sub</c> — pack §8). Never from a header alone, a query or a body.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }

    Guid LegalEntityId { get; }

    /// <summary>JWT <c>sub</c> of the caller.</summary>
    Guid UserId { get; }

    bool IsResolved { get; }

    void SetScope(Guid tenantId, Guid legalEntityId, Guid userId);
}
