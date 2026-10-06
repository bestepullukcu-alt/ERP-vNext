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

    /// <summary>FIX1 (item 10) — the product counterpart of <see cref="IBrandRepository.FindByExternalReferenceAsync"/>.</summary>
    Task<Product?> FindByExternalReferenceAsync(
        string sourceSystem, string externalId, Guid? excludeId, CancellationToken cancellationToken = default);
}
