namespace Diten.TalentEcosystemService.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    /// <summary>
    /// Legal entity that owns this record within the tenant. Stamped from the selected legal entity
    /// (X-Legal-Entity-Id) on write; reads roll up over the selected entity and its descendants.
    /// Records created before legal-entity scoping are backfilled to the tenant's root holding.
    /// </summary>
    public Guid LegalEntityId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
