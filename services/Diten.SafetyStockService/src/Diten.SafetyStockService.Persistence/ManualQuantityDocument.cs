using MongoDB.Bson.Serialization.Attributes;

namespace Diten.SafetyStockService.Persistence;

public sealed class ManualQuantityDocument
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string LegalEntityId { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public string PolicyRevisionId { get; set; } = string.Empty;
    public string SkuId { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string BaseUomId { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveUntilUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string PreparedBy { get; set; } = string.Empty;
    public string? SubmittedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public string State { get; set; } = "Draft";
    public long Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class ManualQuantityReceiptDocument
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string QuantityId { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public string PolicyRevisionId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string BaseUomId { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveUntilUtc { get; set; }
    public string State { get; set; } = string.Empty;
    public long Version { get; set; }
    public string Outcome { get; set; } = string.Empty;
}
