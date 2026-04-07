using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Items;

public sealed class ApiListResponse<T>
{
    public List<T> Data { get; set; } = [];
}

public class LookupOptionViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ItemCategoryOptionViewModel : LookupOptionViewModel
{
    public Guid ItemTypeId { get; set; }
    public Guid? ParentCategoryId { get; set; }
}

public sealed class ItemVariantModelOptionViewModel : LookupOptionViewModel
{
    public Guid ItemTypeId { get; set; }
}

public sealed class ItemVariantTemplateViewModel
{
    public Guid AttributeDefinitionId { get; set; }
    public string AttributeCode { get; set; } = string.Empty;
    public string AttributeName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsVariantAxis { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ItemAttributeValueViewModel
{
    public Guid AttributeDefinitionId { get; set; }
    public string AttributeCode { get; set; } = string.Empty;
    public string AttributeName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsVariantAxis { get; set; }
}

public sealed class ItemVariantViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<ItemAttributeValueViewModel> AttributeValues { get; set; } = [];
}

public sealed class ItemEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? ShortDescription { get; set; }

    [Required]
    public Guid ItemTypeId { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid BaseUomId { get; set; }

    public bool Stockable { get; set; } = true;
    public bool Purchasable { get; set; } = true;
    public bool Sellable { get; set; } = true;
    public bool ServiceItem { get; set; }

    [Required]
    public Guid TrackingPolicyId { get; set; }

    [Required]
    public Guid LifecycleStateId { get; set; }

    public bool IsActive { get; set; } = true;
    public Guid? VariantModelId { get; set; }

    public string AttributeValuesJson { get; set; } = "[]";
    public string VariantsJson { get; set; } = "[]";

    public List<LookupOptionViewModel> ItemTypes { get; set; } = [];
    public List<ItemCategoryOptionViewModel> Categories { get; set; } = [];
    public List<LookupOptionViewModel> BaseUoms { get; set; } = [];
    public List<LookupOptionViewModel> TrackingPolicies { get; set; } = [];
    public List<LookupOptionViewModel> LifecycleStates { get; set; } = [];
    public List<ItemVariantModelOptionViewModel> VariantModels { get; set; } = [];

    public List<ItemVariantTemplateViewModel> VariantTemplates { get; set; } = [];
    public List<ItemAttributeValueViewModel> AttributeValues { get; set; } = [];
    public List<ItemVariantViewModel> Variants { get; set; } = [];

    public string? ItemType { get; set; }
    public string? Category { get; set; }
    public string? BaseUom { get; set; }
    public string? TrackingPolicy { get; set; }
    public string? LifecycleState { get; set; }
    public string? VariantModel { get; set; }
}

public sealed class ItemIndexViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BaseUom { get; set; } = string.Empty;
    public string TrackingPolicy { get; set; } = string.Empty;
    public string LifecycleState { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ItemCategoryAdminViewModel
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

public sealed class ItemVariantModelAdminViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ItemTypeId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<ItemVariantTemplateViewModel> Attributes { get; set; } = [];
}

public sealed class ItemAttributePayload
{
    public Guid AttributeDefinitionId { get; set; }
    public string Value { get; set; } = string.Empty;
}

public sealed class ItemVariantPayload
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<ItemAttributePayload> AttributeValues { get; set; } = [];
}

public sealed class ItemSavePayload
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
    public bool IsActive { get; set; }
    public Guid? VariantModelId { get; set; }
    public List<ItemAttributePayload> AttributeValues { get; set; } = [];
    public List<ItemVariantPayload> Variants { get; set; } = [];
}

public sealed class ItemVariantModelAttributePayload
{
    public Guid? AttributeDefinitionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "Text";
    public bool IsRequired { get; set; }
    public bool IsVariantAxis { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ItemVariantModelSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ItemTypeId { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ItemVariantModelAttributePayload> Attributes { get; set; } = [];
}
