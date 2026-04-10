using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.MdmService.Domain.Entities;

public enum PackagingType
{
    Box = 1,
    Pallet = 2,
    Bottle = 3,
    Can = 4,
    Carton = 5,
    Drum = 6
}

public enum PackagingLevel
{
    Primary = 1,
    Secondary = 2,
    Tertiary = 3,
    Quaternary = 4
}

public sealed class Dimensions
{
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal Length { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? LengthUnitId { get; set; }
}

public sealed class Weight
{
    public decimal NetWeight { get; set; }
    public decimal GrossWeight { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? WeightUnitId { get; set; }
}

public sealed class PackagingDefinition : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    
    public PackagingType PackagingType { get; set; } = PackagingType.Box;
    public PackagingLevel PackagingLevel { get; set; } = PackagingLevel.Primary;

    [BsonRepresentation(BsonType.String)]
    public Guid? ChildPackagingId { get; set; }

    public int UnitsPerPack { get; set; } = 1;

    public Dimensions? Dimensions { get; set; }
    public Weight? Weight { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? LifecycleStateId { get; set; }
}
