using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

public sealed class InvoiceLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Description { get; init; }
    public required string Currency { get; init; }

    [BsonRepresentation(BsonType.Decimal128)]
    public required decimal Quantity { get; init; }

    [BsonRepresentation(BsonType.Decimal128)]
    public required decimal UnitPrice { get; init; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DiscountAmount { get; init; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TaxAmount { get; init; }

    [BsonRepresentation(BsonType.Decimal128)]
    public required decimal LineTotal { get; init; }
}
