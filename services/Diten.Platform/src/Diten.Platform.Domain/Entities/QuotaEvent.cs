using Diten.Platform.Common.Persistence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

public sealed class QuotaEvent : TenantScopedEntity
{
    public required string QuotaKey { get; set; }
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Delta { get; set; }
    public required string Reason { get; set; }
    public required string Source { get; set; }
    public string? OperationId { get; set; }
    public string? SourceReference { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsRejected { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
