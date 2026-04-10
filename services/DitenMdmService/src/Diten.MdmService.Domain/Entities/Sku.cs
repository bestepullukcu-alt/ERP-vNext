using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.MdmService.Domain.Entities;

public sealed class SkuPackaging
{
    public string Form { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

public sealed class Sku : EntityBase
{
    public string Code { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public Guid ProductId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid CompositionId { get; set; }

    public CompositionVersion CompositionVersion { get; set; } = new();

    public SkuPackaging Packaging { get; set; } = new();

    public string? Barcode { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid LifecycleStateId { get; set; }
}
