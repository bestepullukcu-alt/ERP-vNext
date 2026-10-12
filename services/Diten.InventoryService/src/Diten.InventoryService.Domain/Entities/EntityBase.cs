namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// Base for every MOD-0173 Mongo document (module pack front-matter <c>entity_base: EntityBase</c>).
/// TenantId and LegalEntityId are stamped server-side by the repository from the resolved JWT scope — never taken
/// from a request body or query (multi-tenancy hard rule 1, pack §8). Soft-delete fields exist for standard
/// compliance only: ledger documents are append-only and no code path sets <see cref="IsDeleted"/> (pack §4, §8).
/// </summary>
public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? TenantId { get; set; }

    public Guid? LegalEntityId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Optimistic-concurrency counter; incremented on every balance projection change.</summary>
    public int Version { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
