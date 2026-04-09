using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.Products;

public abstract class ProductUpsertRequestBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public ProductType ProductType { get; set; }
    public Guid CategoryId { get; set; }
    public Guid LifecycleStateId { get; set; }
    public bool IsSaleable { get; set; } = true;
    public bool IsPurchasable { get; set; }
    public bool IsManufacturable { get; set; }
}

public class ProductListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string ProductTypeCode { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Guid LifecycleStateId { get; set; }
    public string LifecycleStateCode { get; set; } = string.Empty;
    public string LifecycleState { get; set; } = string.Empty;
    public bool IsSaleable { get; set; }
    public bool IsPurchasable { get; set; }
    public bool IsManufacturable { get; set; }
}

public sealed class ProductDetailDto : ProductListItemDto
{
    public string? Description { get; set; }
}

internal static class ProductMapping
{
    public static ProductListItemDto ToListDto(
        Product entity,
        ProductCatalog.ProductCategoryDefinition category,
        Domain.Entities.LifecycleState lifecycleState)
    {
        var productType = ProductCatalog.GetProductTypeDefinition(entity.ProductType);

        return new ProductListItemDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            ShortName = entity.ShortName,
            ProductTypeCode = productType.Code,
            ProductType = productType.Name,
            CategoryId = entity.CategoryId,
            CategoryCode = category.Code,
            Category = category.Name,
            LifecycleStateId = lifecycleState.Id,
            LifecycleStateCode = lifecycleState.Code,
            LifecycleState = lifecycleState.Name,
            IsSaleable = entity.IsSaleable,
            IsPurchasable = entity.IsPurchasable,
            IsManufacturable = entity.IsManufacturable
        };
    }

    public static ProductDetailDto ToDetailDto(
        Product entity,
        ProductCatalog.ProductCategoryDefinition category,
        Domain.Entities.LifecycleState lifecycleState)
    {
        var dto = ToListDto(entity, category, lifecycleState);

        return new ProductDetailDto
        {
            Id = dto.Id,
            Code = dto.Code,
            Name = dto.Name,
            ShortName = dto.ShortName,
            Description = entity.Description,
            ProductTypeCode = dto.ProductTypeCode,
            ProductType = dto.ProductType,
            CategoryId = dto.CategoryId,
            CategoryCode = dto.CategoryCode,
            Category = dto.Category,
            LifecycleStateId = dto.LifecycleStateId,
            LifecycleStateCode = dto.LifecycleStateCode,
            LifecycleState = dto.LifecycleState,
            IsSaleable = dto.IsSaleable,
            IsPurchasable = dto.IsPurchasable,
            IsManufacturable = dto.IsManufacturable
        };
    }
}
