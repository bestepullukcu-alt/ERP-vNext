using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Products;

public sealed class ApiListResponse<T>
{
    public List<T> Data { get; set; } = [];
}

public sealed class ProductTypeOptionViewModel
{
    public int Value { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProductCategoryOptionViewModel
{
    public Guid Id { get; set; }
    public int ProductType { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProductLifecycleOptionViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProductIndexPageViewModel
{
    public List<ProductTypeOptionViewModel> ProductTypes { get; set; } = [];
    public List<ProductCategoryOptionViewModel> Categories { get; set; } = [];
    public List<ProductLifecycleOptionViewModel> LifecycleStates { get; set; } = [];
}

public sealed class ProductEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? ShortName { get; set; }
    public string? Description { get; set; }

    [Range(1, int.MaxValue)]
    public int ProductType { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid LifecycleStateId { get; set; }

    public bool IsSaleable { get; set; } = true;
    public bool IsPurchasable { get; set; }
    public bool IsManufacturable { get; set; }

    public List<ProductTypeOptionViewModel> ProductTypes { get; set; } = [];
    public List<ProductCategoryOptionViewModel> Categories { get; set; } = [];
    public List<ProductLifecycleOptionViewModel> LifecycleStates { get; set; } = [];
}

public sealed class ProductLifecycleTransitionViewModel
{
    public Guid LifecycleStateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ButtonClass { get; set; } = "btn-label-secondary";
}

public sealed class ProductDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public int ProductType { get; set; }
    public string ProductTypeCode { get; set; } = string.Empty;
    public string ProductTypeName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public Guid LifecycleStateId { get; set; }
    public string LifecycleStateCode { get; set; } = string.Empty;
    public string LifecycleStateName { get; set; } = string.Empty;
    public string LifecycleBadgeClass { get; set; } = "bg-label-secondary";
    public bool IsSaleable { get; set; }
    public bool IsPurchasable { get; set; }
    public bool IsManufacturable { get; set; }
    public List<ProductLifecycleTransitionViewModel> AvailableTransitions { get; set; } = [];
}

public sealed class ProductLifecycleSavePayload
{
    public Guid LifecycleStateId { get; set; }
}

public sealed class ProductSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public int ProductType { get; set; }
    public Guid CategoryId { get; set; }
    public Guid LifecycleStateId { get; set; }
    public bool IsSaleable { get; set; } = true;
    public bool IsPurchasable { get; set; }
    public bool IsManufacturable { get; set; }
}

public sealed class ProductApiDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Description { get; set; }
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

public sealed class LookupApiViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
