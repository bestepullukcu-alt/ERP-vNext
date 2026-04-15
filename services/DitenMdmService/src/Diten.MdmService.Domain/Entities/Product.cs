using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.MdmService.Domain.Entities;

public sealed class Product : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    
    public int ProductType { get; set; } // 1: Finished, 2: Semi-Finished, 3: Service, 4: Technology
    
    [BsonRepresentation(BsonType.String)]
    public Guid CategoryId { get; set; }
    
    [BsonRepresentation(BsonType.String)]
    public Guid LifecycleStateId { get; set; }
    
    public bool IsSaleable { get; set; }
    public bool IsPurchasable { get; set; }
    public bool IsManufacturable { get; set; }
}
