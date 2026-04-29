using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.DevEnablementService.Domain.Entities;

/// <summary>
/// Tüm Mongo dokümanları için zorunlu base sınıf.
/// TenantId her zaman server-side (TenantContext) üzerinden set edilir.
/// </summary>
public abstract class BaseEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Multi-tenant ayırt edici — asla DTO/body'den kabul edilmez.
    /// Global reference data tiplerinde null olabilir.
    /// </summary>
    [BsonRepresentation(BsonType.String)]
    public Guid? TenantId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
}
