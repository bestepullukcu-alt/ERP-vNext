using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductRepository : IRepository<Product>
{
    /// <summary>Tenant-scoped code uniqueness, archived rows included (see <see cref="IBrandRepository"/>).</summary>
    Task<bool> ExistsByCodeAsync(string productCode, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Products linked to a brand — feeds the Brand detail Products tab. Archived rows are included.</summary>
    Task<IReadOnlyList<Product>> GetByBrandAsync(Guid brandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// WP-MDM-BRAND-ACCEPT-01 FIX1 (item 9) — inserts the product; false when the tenant already holds its code (the unique
    /// index answers a concurrent create that passed the pre-check), never an exception.
    /// </summary>
    Task<bool> TryCreateAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>
    /// WP-MDM-BRAND-ACCEPT-01 FIX2 (items 6, 7, 9) — inserts the product and reserves its external references in ONE
    /// transaction: <see cref="BrandWriteOutcome.DuplicateCode"/> for a taken code, <see cref="BrandWriteOutcome.ReferenceInUse"/>
    /// for a taken pair, <see cref="BrandWriteOutcome.Unavailable"/> when the store cannot write atomically.
    /// </summary>
    Task<BrandWriteOutcome> CreateWithReservationsAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>
    /// FIX2 (items 5, 6) — replaces the product (tenant-scoped) and, in the same transaction, reserves the references the
    /// edit ADDS and releases the ones it REMOVES. FIX3 (R-3) — only while the stored product is still at
    /// <paramref name="expectedVersion"/> and not archived: <see cref="BrandWriteOutcome.Stale"/> = changed, archived or gone
    /// meanwhile, and NOTHING was written (the loser's reservations go with its transaction).
    /// </summary>
    Task<BrandWriteOutcome> UpdateWithReservationsAsync(
        Product product, int expectedVersion,
        IReadOnlyList<BrandProductExternalReference> added, IReadOnlyList<BrandProductExternalReference> removed,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// FIX3 (R-3, M-3) — archives the product only while it is still at <paramref name="expectedVersion"/> and not archived, and
    /// releases its external-reference reservations in the same transaction (an archived record no longer holds a pair).
    /// </summary>
    Task<BrandWriteOutcome> ArchiveWithReservationsAsync(
        Product archived, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>FIX1 (item 10) — the product counterpart of <see cref="IBrandRepository.FindByExternalReferenceAsync"/>.</summary>
    Task<Product?> FindByExternalReferenceAsync(
        string sourceSystem, string externalId, Guid? excludeId, CancellationToken cancellationToken = default);
}
