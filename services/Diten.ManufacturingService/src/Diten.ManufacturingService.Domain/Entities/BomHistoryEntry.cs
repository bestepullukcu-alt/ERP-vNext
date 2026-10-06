using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.ManufacturingService.Domain.Entities;

/// <summary>
/// BOM sürüm geçmişi — AUD-001 yol c eşdeğer izi (<c>bom-surum-gecmisi</c>). Yalnız eklenir; güncellenmez, silinmez
/// (depo arayüzünde Update/Delete/Replace yok). BOM yazımıyla AYNI işlemde yazılır: geçmiş yazılamazsa BOM da yazılmaz
/// (GxP, K2 fail-closed). Kaydın kendi ekranında (BOM Details → Geçmiş) <c>manufacturing.bom.read</c> ile okunur.
/// Değişen alanların yalnız ADI yazılır, değeri değil (AUD-001 §3).
/// </summary>
public sealed class BomHistoryEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid TenantId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid LegalEntityId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid BomVersionId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid ItemId { get; set; }

    public int RevisionNo { get; set; }

    [BsonRepresentation(BsonType.String)]
    public BomHistoryOperation Operation { get; set; }

    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }

    public List<string> ChangedFields { get; set; } = [];

    public string? ChangeControlRef { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid ActorId { get; set; }

    /// <summary>O andaki görünen ad — kullanıcı sonradan silinse de okunur (AUD-001 §3 alan 2).</summary>
    public string ActorDisplayName { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset OccurredAtUtc { get; set; }

    public string Outcome { get; set; } = "Succeeded";
}

public enum BomHistoryOperation
{
    Created,
    Updated,
    Released,
    Superseded,
    Deleted
}
