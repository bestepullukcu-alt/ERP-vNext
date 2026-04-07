namespace Diten.MdmService.Application.Features.ItemVariantModels;

public sealed class VariantModelAttributeDefinitionInputDto
{
    public Guid? AttributeDefinitionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "Text";
    public bool IsRequired { get; set; }
    public bool IsVariantAxis { get; set; }
    public int SortOrder { get; set; }
}

public abstract class ItemVariantModelUpsertRequestBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ItemTypeId { get; set; }
    public bool IsActive { get; set; } = true;
    public List<VariantModelAttributeDefinitionInputDto> Attributes { get; set; } = [];
}

public sealed class ItemVariantModelAttributeDto
{
    public Guid AttributeDefinitionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsVariantAxis { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ItemVariantModelDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ItemTypeId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<ItemVariantModelAttributeDto> Attributes { get; set; } = [];
}

public sealed class BulkDeleteItemVariantModelsResponse
{
    public int DeletedCount { get; set; }
}
