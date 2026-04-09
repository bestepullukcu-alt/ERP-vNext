using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.MdmService.Domain.Entities;

public enum ProductType
{
    FinishedGood = 1,
    Service = 2,
    Digital = 3
}

public sealed class Product : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public ProductType ProductType { get; set; } = ProductType.FinishedGood;

    [BsonRepresentation(BsonType.String)]
    public Guid CategoryId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid LifecycleStateId { get; set; }

    public bool IsSaleable { get; set; } = true;
    public bool IsPurchasable { get; set; }
    public bool IsManufacturable { get; set; }
}
