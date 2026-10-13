using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.ManufacturingService.Domain.Entities;

/// <summary>
/// MOD-0193 BOM sürümü — bir ana ürünün (MOD-0290 item) bileşen ve rota yapısının tek bir revizyonu.
/// Kimlik MOD-0290'dadır; bu kayıt yalnız "ne kadar ve hangi adımlarla" sorusunun SoR'udur (rapor §23.3).
/// Draft düzenlenebilir; Effective / Superseded değiştirilemez ve silinemez (GxP — düzeltme yeni sürümle).
/// <see cref="EntityBase.Version"/> eşzamanlılık belirtecidir; contract'taki <c>version</c> alanı <see cref="RevisionNo"/>'dur.
/// </summary>
public sealed class BomVersion : EntityBase
{
    [BsonRepresentation(BsonType.String)]
    public Guid ItemId { get; set; }

    /// <summary>Item başına artan revizyon numarası (contract <c>version</c>). Silinen taslağın numarası tekrar kullanılmaz.</summary>
    public int RevisionNo { get; set; }

    public string? Description { get; set; }

    [BsonRepresentation(BsonType.String)]
    public BomStatus Status { get; set; } = BomStatus.Draft;

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? EffectiveFrom { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary>MOD-0209 değişiklik kontrolü referansı — yürürlüğe almada zorunlu.</summary>
    public string? ChangeControlRef { get; set; }

    public List<BomComponentLine> Components { get; set; } = [];

    public BomRouting? Routing { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid CreatedBy { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? UpdatedBy { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? ReleasedBy { get; set; }

    /// <summary>Verilen anda yürürlükte mi: <c>EffectiveFrom ≤ at &lt; EffectiveTo</c> (EffectiveTo yoksa açık uçlu).</summary>
    public bool IsEffectiveAt(DateTimeOffset at) =>
        Status is BomStatus.Effective or BomStatus.Superseded
        && EffectiveFrom is { } from && from <= at
        && (EffectiveTo is null || at < EffectiveTo.Value);
}

public enum BomStatus
{
    Draft,
    Effective,
    Superseded
}

public sealed class BomComponentLine
{
    [BsonRepresentation(BsonType.String)]
    public Guid ComponentItemId { get; set; }

    /// <summary>1 birim ana ürün başına miktar — decimal string (float YASAK).</summary>
    public string Quantity { get; set; } = string.Empty;

    public string UomId { get; set; } = string.Empty;

    public int Position { get; set; }

    public List<string>? Alternates { get; set; }
}

public sealed class BomRouting
{
    public string RoutingId { get; set; } = string.Empty;
    public List<RoutingStep> Steps { get; set; } = [];
}

public sealed class RoutingStep
{
    public int StepNo { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string? WorkCenter { get; set; }
}
