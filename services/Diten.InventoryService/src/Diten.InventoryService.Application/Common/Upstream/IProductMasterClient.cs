using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>
/// Consumer of the frozen PRODUCT-MASTER-BUNDLE v1 (MOD-0290, Model A only — DEC-INV-17). HTTP only: no MDM
/// database, no MDM internal type (pack §2). A lookup returns <c>null</c> for a contract 404; every other failure
/// (unreachable, timeout, 5xx, undeclared status, unreadable body) throws <see cref="ProductMasterUnavailableException"/>
/// so the caller fails CLOSED (pack §8) — there is no silent-pass path.
/// </summary>
public interface IProductMasterClient
{
    Task<IReadOnlyList<ProductRefValidation>> ValidateRefsAsync(IReadOnlyList<ProductRefQuery> refs, CancellationToken cancellationToken = default);

    Task<ProductReference?> GetProductAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<SkuReference?> GetSkuAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default);

    Task<SkuUomInfo?> GetSkuUomAsync(Guid skuId, CancellationToken cancellationToken = default);
}
