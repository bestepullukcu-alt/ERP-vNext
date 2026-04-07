using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.MdmService.Domain.Entities;

public abstract class LookupEntityBase : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ItemType : LookupEntityBase
{
}

public sealed class TrackingPolicy : LookupEntityBase
{
}

public sealed class LifecycleState : LookupEntityBase
{
}

public sealed class UnitOfMeasure : LookupEntityBase
{
}

public sealed class UomConversion : EntityBase
{
    [BsonRepresentation(BsonType.String)]
    public Guid FromUomId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid ToUomId { get; set; }

    public decimal Factor { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ItemCategory : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid ItemTypeId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? ParentCategoryId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class ItemVariantModel : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid ItemTypeId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class AttributeDefinition : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "Text";
    public bool IsVariantAxis { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AttributeTemplate : EntityBase
{
    [BsonRepresentation(BsonType.String)]
    public Guid VariantModelId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid AttributeDefinitionId { get; set; }

    public bool IsRequired { get; set; }
    public bool IsVariantAxis { get; set; }
    public int SortOrder { get; set; }
}

public sealed class Item : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid ItemTypeId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid CategoryId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid BaseUomId { get; set; }

    public bool Stockable { get; set; }
    public bool Purchasable { get; set; }
    public bool Sellable { get; set; }
    public bool ServiceItem { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid TrackingPolicyId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid LifecycleStateId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? VariantModelId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class ItemAttributeValue : EntityBase
{
    [BsonRepresentation(BsonType.String)]
    public Guid ItemId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid AttributeDefinitionId { get; set; }

    public string Value { get; set; } = string.Empty;
}

public sealed class ItemVariant : EntityBase
{
    [BsonRepresentation(BsonType.String)]
    public Guid ItemId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<ItemVariantAttributeValue> AttributeValues { get; set; } = [];
}

public sealed class ItemVariantAttributeValue
{
    [BsonRepresentation(BsonType.String)]
    public Guid AttributeDefinitionId { get; set; }

    public string Value { get; set; } = string.Empty;
}
