using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Billing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

public sealed class PaymentRecord : TenantScopedEntity
{
    public required Guid InvoiceId { get; init; }
    public required string IdempotencyKey { get; init; }
    public required string Currency { get; init; }

    [BsonRepresentation(BsonType.Decimal128)]
    public required decimal AppliedAmount { get; init; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal RefundedAmount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Applied;
    public string? Reference { get; init; }
}
