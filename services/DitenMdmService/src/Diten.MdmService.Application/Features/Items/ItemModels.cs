namespace Diten.MdmService.Application.Features.Items;

public abstract class ItemUpsertRequestBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public Guid ItemTypeId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid BaseUomId { get; set; }
    public bool Stockable { get; set; }
    public bool Purchasable { get; set; }
    public bool Sellable { get; set; }
    public bool ServiceItem { get; set; }
    public Guid TrackingPolicyId { get; set; }
    public Guid LifecycleStateId { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? VariantModelId { get; set; }
    public List<ItemAttributeValueInputDto> AttributeValues { get; set; } = [];
    public List<ItemVariantInputDto> Variants { get; set; } = [];
}

public sealed class ItemAttributeValueInputDto
{
    public Guid AttributeDefinitionId { get; set; }
    public string Value { get; set; } = string.Empty;
}

public sealed class ItemVariantAttributeValueInputDto
{
    public Guid AttributeDefinitionId { get; set; }
    public string Value { get; set; } = string.Empty;
}

public sealed class ItemVariantInputDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<ItemVariantAttributeValueInputDto> AttributeValues { get; set; } = [];
}

public sealed class ItemVariantTemplateDto
{
    public Guid AttributeDefinitionId { get; set; }
    public string AttributeCode { get; set; } = string.Empty;
    public string AttributeName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsVariantAxis { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ItemAttributeValueDto
{
    public Guid AttributeDefinitionId { get; set; }
    public string AttributeCode { get; set; } = string.Empty;
    public string AttributeName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsVariantAxis { get; set; }
}

public sealed class ItemVariantDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<ItemAttributeValueDto> AttributeValues { get; set; } = [];
}

public class ItemListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid ItemTypeId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string Category { get; set; } = string.Empty;
    public Guid BaseUomId { get; set; }
    public string BaseUom { get; set; } = string.Empty;
    public Guid TrackingPolicyId { get; set; }
    public string TrackingPolicy { get; set; } = string.Empty;
    public Guid LifecycleStateId { get; set; }
    public string LifecycleState { get; set; } = string.Empty;
    public Guid? VariantModelId { get; set; }
    public string? VariantModel { get; set; }
    public bool Stockable { get; set; }
    public bool Purchasable { get; set; }
    public bool Sellable { get; set; }
    public bool ServiceItem { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ItemDetailDto : ItemListItemDto
{
    public string? ShortDescription { get; set; }
    public List<ItemAttributeValueDto> AttributeValues { get; set; } = [];
    public List<ItemVariantDto> Variants { get; set; } = [];
    public List<ItemVariantTemplateDto> VariantTemplates { get; set; } = [];
}

public sealed class BulkDeleteItemsResponse
{
    public int DeletedCount { get; set; }
}
