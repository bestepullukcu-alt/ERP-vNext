using Diten.Platform.Common.Persistence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

public sealed class RefundRecord : TenantScopedEntity
{
    public required Guid InvoiceId { get; set; }
    public required Guid PaymentRecordId { get; init; }

    [BsonRepresentation(BsonType.Decimal128)]
    public required decimal RefundAmount { get; init; }

    public required string Currency { get; init; }
    public required string Reason { get; init; }
}
