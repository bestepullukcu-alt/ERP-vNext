using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Territory.Nodes;

public sealed record MicroZoneProfileDto(Guid? AnchorAccountId, string? ClusterNotes, string? PlanningCenterType);

public sealed record TerritoryNodeDto(
    Guid Id,
    Guid ModelId,
    Guid? ParentTerritoryId,
    string TerritoryCode,
    string Name,
    string TerritoryLevel,
    string Status,
    string StoredStatus,
    string ComputedStatus,
    bool IsExpired,
    string? CountryCode,
    string? DivisionCode,
    string? RegionCode,
    string? AreaCode,
    string? ZoneCode,
    string? MicroZoneCode,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int SortOrder,
    MicroZoneProfileDto? MicroZoneProfile,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record TerritoryHierarchyDto(Guid ModelId, IReadOnlyList<TerritoryNodeDto> Nodes);

/// <summary>WP-SEG-DETAILS8 — the minimal reverse-lookup projection of a node: just enough for a consumer that holds a
/// node id to render its name and (re)establish the parent-model context. No lifecycle, coverage or micro-zone detail —
/// this is a label resolver, not the node read.</summary>
public sealed record TerritoryNodeLookupDto(Guid Id, string Name, Guid ModelId, string Code);

public static class TerritoryNodeMapper
{
    private static bool IsExpired(TerritoryNode node)
        => node.EffectiveTo is { } end && end < DateTimeOffset.UtcNow;

    public static MicroZoneProfileDto? ToDto(MicroZoneProfile? p)
        => p is null ? null : new MicroZoneProfileDto(p.AnchorAccountId, p.ClusterNotes, p.PlanningCenterType);

    public static TerritoryNodeDto ToDto(TerritoryNode n) => new(
        n.Id, n.ModelId, n.ParentTerritoryId, n.TerritoryCode, n.Name, n.TerritoryLevel, n.Status,
        n.Status, IsExpired(n) ? "expired" : n.Status, IsExpired(n),
        n.CountryCode, n.DivisionCode, n.RegionCode, n.AreaCode, n.ZoneCode, n.MicroZoneCode,
        n.EffectiveFrom, n.EffectiveTo, n.SortOrder, ToDto(n.MicroZoneProfile), n.CorrelationId, n.CreatedAt, n.UpdatedAt);
}
