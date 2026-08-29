namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0151 territory hierarchy node (aggregate, model-scoped, FU01 scope). A single node type carries every
/// hierarchy level (division/country/region/area/zone/microzone) via <see cref="TerritoryLevel"/> — MicroZone is
/// NOT a separate aggregate/permission/collection (pack §8 / §12). Identity is the inherited <see cref="EntityBase.Id"/>;
/// <see cref="TerritoryCode"/> is the human code, unique within its model. Coverage/assignment fields belong to later FUs.
/// </summary>
public sealed class TerritoryNode : EntityBase
{
    public Guid ModelId { get; set; }

    /// <summary>Parent node within the same model/tenant. Null for a root node. Cycles forbidden.</summary>
    public Guid? ParentTerritoryId { get; set; }

    /// <summary>Human-readable code, unique within (TenantId, ModelId). Trimmed/normalized.</summary>
    public string TerritoryCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>MOD-0048 published <c>territory-level</c> value code (carries rank/sortOrder metadata).</summary>
    public string TerritoryLevel { get; set; } = string.Empty;

    public string? CountryCode { get; set; }
    public string? DivisionCode { get; set; }
    public string? RegionCode { get; set; }
    public string? AreaCode { get; set; }
    public string? ZoneCode { get; set; }
    public string? MicroZoneCode { get; set; }

    /// <summary>MOD-0048 published <c>territory-node-status</c> value code. FU01 defaults to <c>draft</c>.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Only populated when <see cref="TerritoryLevel"/> == <c>microzone</c>; null on every other level.</summary>
    public MicroZoneProfile? MicroZoneProfile { get; set; }

    public string? CorrelationId { get; set; }
}
