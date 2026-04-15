namespace Diten.MdmService.Application.Features.Products;

public sealed class ProductDto
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

public sealed class ProductListDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProductTypeCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string LifecycleState { get; set; } = string.Empty;
    public string LifecycleStateCode { get; set; } = string.Empty;
    public bool IsSaleable { get; set; }
    public bool IsPurchasable { get; set; }
    public bool IsManufacturable { get; set; }
}
