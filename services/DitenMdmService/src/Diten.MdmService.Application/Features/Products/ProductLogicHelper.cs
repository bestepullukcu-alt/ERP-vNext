using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.Products;

internal static class ProductLogicHelper
{
    public static async Task ValidateUpsertAsync(
        ProductUpsertRequestBase request,
        Guid? excludeId,
        IProductRepository productRepository,
        IItemLookupRepository lookupRepository,
        CancellationToken cancellationToken)
    {
        if (await productRepository.ExistsByCodeAsync(request.Code.Trim(), excludeId, cancellationToken))
        {
            throw new InvalidOperationException("Product code must be unique within the tenant.");
        }

        if (!Enum.IsDefined(request.ProductType))
        {
            throw new InvalidOperationException("Product type is invalid.");
        }

        if (!ProductCatalog.IsCategoryValidForProductType(request.CategoryId, request.ProductType))
        {
            throw new InvalidOperationException("Selected category must belong to the selected product type.");
        }

        _ = await lookupRepository.GetLifecycleStateByIdAsync(request.LifecycleStateId, cancellationToken)
            ?? throw new KeyNotFoundException("Lifecycle state not found.");
    }

    public static Product ToEntity(ProductUpsertRequestBase request, Product? existing)
    {
        var entity = existing ?? new Product();
        entity.Code = request.Code.Trim();
        entity.Name = request.Name.Trim();
        entity.ShortName = string.IsNullOrWhiteSpace(request.ShortName) ? null : request.ShortName.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.ProductType = request.ProductType;
        entity.CategoryId = request.CategoryId;
        entity.LifecycleStateId = request.LifecycleStateId;
        entity.IsSaleable = request.IsSaleable;
        entity.IsPurchasable = request.IsPurchasable;
        entity.IsManufacturable = request.IsManufacturable;
        return entity;
    }

    public static void ValidateLifecycleTransition(string currentCode, string targetCode, string? reason)
    {
        var normalizedCurrent = (currentCode ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedTarget = (targetCode ?? string.Empty).Trim().ToUpperInvariant();

        if (normalizedCurrent == normalizedTarget)
        {
            return;
        }

        var allowed = normalizedCurrent switch
        {
            "DRAFT" => new[] { "ACTIVE" },
            "ACTIVE" => new[] { "BLOCKED", "OBSOLETE" },
            "BLOCKED" => new[] { "ACTIVE", "OBSOLETE" },
            "OBSOLETE" => new[] { "ACTIVE" },
            _ => Array.Empty<string>()
        };

        if (!allowed.Contains(normalizedTarget))
        {
            throw new InvalidOperationException("Requested lifecycle transition is not allowed.");
        }
    }

    public static async Task ValidateDeleteAsync(
        Product product,
        IItemLookupRepository lookupRepository,
        CancellationToken cancellationToken)
    {
        var lifecycleState = await lookupRepository.GetLifecycleStateByIdAsync(product.LifecycleStateId, cancellationToken)
            ?? throw new KeyNotFoundException("Lifecycle state not found.");

        if (!string.Equals(lifecycleState.Code, "DRAFT", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only products in DRAFT state can be deleted.");
        }
    }
}
