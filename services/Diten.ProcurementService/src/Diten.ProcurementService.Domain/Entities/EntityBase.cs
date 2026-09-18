using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.ProcurementService.Domain.Entities;

/// <summary>
/// Tüm Procurement Mongo dokümanları için zorunlu base sınıf (entity_base: EntityBase — MOD-0140 pack §4).
/// TenantId ve LegalEntityId her zaman server-side (TenantContext / TenantResolutionMiddleware) üzerinden set edilir;
/// asla DTO/request body'den kabul edilmez (multi-tenancy.md hard rule 1). Soft-delete zorunlu.
/// </summary>
public abstract class EntityBase
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Multi-tenant ayırt edici — asla DTO/body'den kabul edilmez; her sorguda filtre.
    /// </summary>
    [BsonRepresentation(BsonType.String)]
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Legal-Entity izolasyon anahtarı — server-resolved (JWT/header), asla payload'dan; her sorguda filtre.
    /// Cross-LE erişim fail-closed → 404 (MOD-0140 §8 / domain-config Scoping).
    /// </summary>
    [BsonRepresentation(BsonType.String)]
    public Guid? LegalEntityId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>İyimser eşzamanlılık (optimistic concurrency) için teknik alan (entity-base-template.md).</summary>
    public int Version { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
}
