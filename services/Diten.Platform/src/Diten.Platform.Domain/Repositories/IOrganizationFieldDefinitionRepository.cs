using Diten.Platform.Domain.Entities.Organization;

namespace Diten.Platform.Domain.Repositories;

/// <summary>MOD-0288-FU02 — tenant-scoped Organization Unit field definitions.</summary>
public interface IOrganizationFieldDefinitionRepository
{
    Task<OrganizationFieldDefinition> CreateAsync(OrganizationFieldDefinition definition, CancellationToken ct = default);

    Task<OrganizationFieldDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Every non-deleted definition of the current tenant, active and inactive alike.</summary>
    Task<IReadOnlyList<OrganizationFieldDefinition>> GetAllAsync(CancellationToken ct = default);

    /*
     * ⚠ A CONDITIONAL WRITE, NOT AN "UPDATE". The mutation names the version it read, and the write only lands
     * if that version is still current — a stale writer gets `false`, which the handler turns into 409. The
     * shape is a return value rather than an exception because losing a race is an ordinary outcome here, not
     * an error condition.
     *
     * ⚠ AND IT IS THE ONLY MUTATION. There is no delete: §5 authorizes create, update and DEACTIVATE, and
     * deactivation is an update. The unique index is still partial on non-deleted rows, mirroring
     * ux_organization_units_tenant_code_active exactly as the pack requires — that filter is what a future
     * delete would need, and getting it right now costs nothing while adding it later would mean rebuilding a
     * unique index on live data.
     */

    Task<bool> TryUpdateAsync(OrganizationFieldDefinition definition, int expectedVersion, CancellationToken ct = default);
}
