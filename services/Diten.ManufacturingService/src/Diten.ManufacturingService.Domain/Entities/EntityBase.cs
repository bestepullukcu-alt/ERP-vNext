using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.ManufacturingService.Domain.Entities;

/// <summary>
/// Tüm Manufacturing Mongo dokümanları için zorunlu base sınıf (entity_base: EntityBase — MOD-0193 pack §4).
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
    /// Cross-LE erişim fail-closed → 404 (MOD-0193 §8 / DEC-INV-18).
    /// </summary>
    [BsonRepresentation(BsonType.String)]
    public Guid? LegalEntityId { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>İyimser eşzamanlılık (optimistic concurrency) için teknik alan (entity-base-template.md).</summary>
    public int Version { get; set; }

    public bool IsDeleted { get; set; } = false;
    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? DeletedAt { get; set; }
}
