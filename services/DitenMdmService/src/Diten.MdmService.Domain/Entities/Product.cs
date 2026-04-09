using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.MdmService.Domain.Entities;

public enum ProductType
{
    FinishedProduct = 1,
    SemiFinishedProduct = 2,
    Service = 3,
    Technology = 4
}

public sealed class Product : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public ProductType ProductType { get; set; } = ProductType.FinishedProduct;

    [BsonRepresentation(BsonType.String)]
    public Guid CategoryId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid LifecycleStateId { get; set; }

    public bool IsSaleable { get; set; } = true;
    public bool IsPurchasable { get; set; }
    public bool IsManufacturable { get; set; }
}

public sealed class ProductLifecycleHistory : EntityBase
{
    [BsonRepresentation(BsonType.String)]
    public Guid ProductId { get; set; }

    public string FromState { get; set; } = string.Empty;
    public string ToState { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
}
