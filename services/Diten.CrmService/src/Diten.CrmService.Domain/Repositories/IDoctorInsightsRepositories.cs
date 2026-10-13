using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>WP-VW-W4 (W4-BE-c) — one profile per (tenant, contact); optimistic replace on the technical version.</summary>
public interface IDoctorProfileRepository
{
    Task<DoctorProfile?> GetByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorProfile>> ListByContactsAsync(Guid tenantId, IReadOnlyCollection<Guid> contactIds, CancellationToken cancellationToken);

    Task InsertAsync(DoctorProfile entity, CancellationToken cancellationToken);

    Task<bool> ReplaceAsync(DoctorProfile entity, int expectedVersion, CancellationToken cancellationToken);
}

/// <summary>WP-VW-W4 (W4-BE-c) — the append-only profile history (newest first).</summary>
public interface IDoctorProfileChangeRepository
{
    Task InsertManyAsync(IReadOnlyCollection<DoctorProfileChange> rows, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorProfileChange>> ListByContactAsync(Guid tenantId, Guid contactId, int take, CancellationToken cancellationToken);
}

/// <summary>WP-VW-W4 (W4-BE-c) — one potential per (tenant, contact, product); optimistic replace.</summary>
public interface IDoctorProductPotentialRepository
{
    Task<DoctorProductPotential?> GetAsync(Guid tenantId, Guid contactId, Guid productId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorProductPotential>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken);

    /// <summary>The potentials of a set of contacts, optionally of one product only.</summary>
    Task<IReadOnlyList<DoctorProductPotential>> ListByContactsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> contactIds, Guid? productId, CancellationToken cancellationToken);

    /// <summary>The tenant's potentials, optionally of one product only (the marketing aggregate).</summary>
    Task<IReadOnlyList<DoctorProductPotential>> ListAsync(Guid tenantId, Guid? productId, CancellationToken cancellationToken);

    Task InsertAsync(DoctorProductPotential entity, CancellationToken cancellationToken);

    Task<bool> ReplaceAsync(DoctorProductPotential entity, int expectedVersion, CancellationToken cancellationToken);
}

/// <summary>WP-VW-W4 (W4-BE-c) — the append-only potential history (newest first).</summary>
public interface IDoctorProductPotentialChangeRepository
{
    Task InsertManyAsync(IReadOnlyCollection<DoctorProductPotentialChange> rows, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorProductPotentialChange>> ListByContactAsync(Guid tenantId, Guid contactId, int take, CancellationToken cancellationToken);
}

/// <summary>WP-VW-W4 (W4-BE-c) — the tenant default (product null) and the per-product thresholds.</summary>
public interface IProductPotentialThresholdRepository
{
    Task<IReadOnlyList<ProductPotentialThreshold>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<ProductPotentialThreshold?> GetAsync(Guid tenantId, Guid? productId, CancellationToken cancellationToken);

    Task InsertAsync(ProductPotentialThreshold entity, CancellationToken cancellationToken);

    Task<bool> ReplaceAsync(ProductPotentialThreshold entity, int expectedVersion, CancellationToken cancellationToken);
}

/// <summary>WP-VW-W4 (W4-BE-c) — the append-only thresholds history (newest first).</summary>
public interface IProductPotentialThresholdChangeRepository
{
    Task InsertManyAsync(IReadOnlyCollection<ProductPotentialThresholdChange> rows, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ProductPotentialThresholdChange> Items, long Total)> ListAsync(
        Guid tenantId, int skip, int take, CancellationToken cancellationToken);
}
