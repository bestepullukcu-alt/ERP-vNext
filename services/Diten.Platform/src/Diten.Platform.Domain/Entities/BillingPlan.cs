using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Billing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

public sealed class BillingPlan : TenantScopedEntity
{
    public required string PlanCode { get; init; }
    public required int PlanVersion { get; init; }
    public BillingPlanStatus Status { get; set; } = BillingPlanStatus.Draft;
    public required string Name { get; init; }

    [BsonRepresentation(BsonType.Decimal128)]
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }
    public BillingInterval BillingInterval { get; init; }
}
