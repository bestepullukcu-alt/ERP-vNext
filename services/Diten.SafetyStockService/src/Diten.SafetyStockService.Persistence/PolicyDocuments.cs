using MongoDB.Bson.Serialization.Attributes;

namespace Diten.SafetyStockService.Persistence;

public sealed class PolicyDocument
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string LegalEntityId { get; set; } = string.Empty;
    public string SkuId { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string CurrentRevisionId { get; set; } = string.Empty;
    public long Version { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class PolicyRevisionDocument
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string LegalEntityId { get; set; } = string.Empty;
    public string SkuId { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string? PreviousRevisionId { get; set; }
    public int CoverageDays { get; set; }
    public string MethodId { get; set; } = "MethodA";
    public string State { get; set; } = "Draft";
    public long Version { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string PreparedBy { get; set; } = string.Empty;
    public List<string> MaterialContributors { get; set; } = [];
    public string? DecisionBy { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
}

public sealed class PolicyAuditDocument
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string LegalEntityId { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string BeforeState { get; set; } = string.Empty;
    public string AfterState { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class PolicyCommandReceiptDocument
{
    [BsonId] public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public long Version { get; set; }
    public string Outcome { get; set; } = string.Empty;
}
