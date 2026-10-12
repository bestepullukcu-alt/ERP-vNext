namespace Diten.InventoryService.Application.Common;

/// <summary>
/// Scoped caller scope, populated by the scope-resolution middleware (pack §8): tenant and user from the SIGNED JWT
/// only (<c>tenant_id</c>, <c>sub</c>); the legal entity from the JWT <c>legal_entity_id</c> claim, or — only when the
/// token carries none — from the <c>X-Legal-Entity-Id</c> header verified against MDM (waiver W-0173-02). Never from a
/// query or a body.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }

    Guid LegalEntityId { get; }

    /// <summary>JWT <c>sub</c> of the caller.</summary>
    Guid UserId { get; }

    /// <summary>Where <see cref="LegalEntityId"/> came from — one of <see cref="LegalEntitySources"/>; stored on every audit row.</summary>
    string LegalEntitySource { get; }

    bool IsResolved { get; }

    void SetScope(Guid tenantId, Guid legalEntityId, Guid userId, string legalEntitySource = LegalEntitySources.Token);
}
