namespace Diten.MdmService.Application.Features.ItemCategories;

public abstract class ItemCategoryUpsertRequestBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ItemTypeId { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ItemCategoryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ItemTypeId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public string? ParentCategory { get; set; }
    public bool IsActive { get; set; }
}

public sealed class BulkDeleteItemCategoriesResponse
{
    public int DeletedCount { get; set; }
}
