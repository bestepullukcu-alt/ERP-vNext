namespace Diten.InventoryService.Application.Common;

/// <summary>
/// Scoped <see cref="ITenantContext"/>. Reading an unresolved scope throws instead of defaulting to
/// <see cref="Guid.Empty"/> — an empty scope would silently pool every tenant's rows under one key.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid _tenantId;
    private Guid _legalEntityId;
    private Guid _userId;
    private string _legalEntitySource = LegalEntitySources.Token;

    public Guid TenantId => IsResolved ? _tenantId : throw Unresolved();

    public Guid LegalEntityId => IsResolved ? _legalEntityId : throw Unresolved();

    public Guid UserId => IsResolved ? _userId : throw Unresolved();

    public string LegalEntitySource => IsResolved ? _legalEntitySource : throw Unresolved();

    public bool IsResolved { get; private set; }

    public void SetScope(Guid tenantId, Guid legalEntityId, Guid userId, string legalEntitySource = LegalEntitySources.Token)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Tenant, legal entity and user must be non-empty.");
        }

        if (legalEntitySource is not (LegalEntitySources.Token or LegalEntitySources.HeaderUnverifiedMembership))
        {
            throw new ArgumentException($"Unknown legal-entity source '{legalEntitySource}'.", nameof(legalEntitySource));
        }

        _tenantId = tenantId;
        _legalEntityId = legalEntityId;
        _userId = userId;
        _legalEntitySource = legalEntitySource;
        IsResolved = true;
    }

    private static InvalidOperationException Unresolved()
        => new("Inventory scope is not resolved. Is the scope-resolution middleware in the pipeline?");
}
