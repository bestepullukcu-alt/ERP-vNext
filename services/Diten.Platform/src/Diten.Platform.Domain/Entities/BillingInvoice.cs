using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Billing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

public sealed class BillingInvoice : TenantScopedEntity
{
    public string? InvoiceNumber { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public required Guid BillingPlanId { get; init; }
    public required int BillingPlanVersion { get; init; }
    public required string BillingPlanNameSnapshot { get; init; }

    [BsonRepresentation(BsonType.Decimal128)]
    public required decimal BillingPlanAmountSnapshot { get; init; }

    public required string BillingPlanCurrencySnapshot { get; init; }
    public required string CustomerReference { get; set; }
    public required string Currency { get; init; }
    public DateTimeOffset? DueDate { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public IReadOnlyList<InvoiceLine> Lines { get; set; } = [];

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal SubTotal { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TaxTotal { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DiscountTotal { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal GrandTotal { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal PaidAmount { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal BalanceDue { get; set; }
}
