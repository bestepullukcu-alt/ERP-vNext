using Diten.Platform.Common.Persistence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

public sealed class QuotaUsage : TenantScopedEntity
{
    public required string QuotaKey { get; set; }
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal CurrentValue { get; set; }
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal LimitValue { get; set; }
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastWarningNotifiedAtUtc { get; set; }
    public DateTimeOffset? LastLimitBreachNotifiedAtUtc { get; set; }
    public bool WarningNotificationSentForPeriod { get; set; }
    public bool LimitBreachNotificationSentForPeriod { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string Source { get; set; } = QuotaLimitSources.PlanDefault;
    public Guid? SubscriptionId { get; set; }
    public Guid? PlanId { get; set; }
    public string? OverrideSource { get; set; }
}

public static class QuotaLimitSources
{
    public const string PlanDefault = "PlanDefault";
    public const string TenantOverride = "TenantOverride";
    public const string ManualOverride = "ManualOverride";
}
