using System.ComponentModel.DataAnnotations;
using Diten.Web.Models;

namespace Diten.Web.Models.Products;

public sealed class ProductIndexPageViewModel
{
    public List<LookupApiViewModel> Categories { get; set; } = [];
    public List<LookupApiViewModel> LifecycleStates { get; set; } = [];
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

    [Range(1, 4)]
    public int ProductType { get; set; } = 1;

    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid LifecycleStateId { get; set; }

    public bool IsSaleable { get; set; } = true;
    public bool IsPurchasable { get; set; } = true;
    public bool IsManufacturable { get; set; }

    public List<LookupApiViewModel> Categories { get; set; } = [];
    public List<LookupApiViewModel> LifecycleStates { get; set; } = [];

    public string CategoryName { get; set; } = string.Empty;
    public string LifecycleStateName { get; set; } = string.Empty;
}

public sealed class ProductDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public int ProductType { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public Guid LifecycleStateId { get; set; }
    public string LifecycleStateCode { get; set; } = string.Empty;
    public string LifecycleStateName { get; set; } = string.Empty;
    public bool IsSaleable { get; set; }
    public bool IsPurchasable { get; set; }
    public bool IsManufacturable { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
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
    public bool IsSaleable { get; set; }
    public bool IsPurchasable { get; set; }
    public bool IsManufacturable { get; set; }
}
