using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductRepository : IRepository<Product>
{
    /// <summary>Tenant-scoped code uniqueness, archived rows included (see <see cref="IBrandRepository"/>).</summary>
    Task<bool> ExistsByCodeAsync(string productCode, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Products linked to a brand — feeds the Brand detail Products tab. Archived rows are included.</summary>
    Task<IReadOnlyList<Product>> GetByBrandAsync(Guid brandId, CancellationToken cancellationToken = default);
}
