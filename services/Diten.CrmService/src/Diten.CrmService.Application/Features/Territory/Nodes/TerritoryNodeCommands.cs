using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Nodes;

/// <summary>Value-object input for a microzone node's profile. Only allowed when TerritoryLevel == <c>microzone</c>.
/// <c>AnchorAccountId</c> is metadata only (no route/distance/nearby logic in FU01).</summary>
public sealed record MicroZoneProfileInput(Guid? AnchorAccountId, string? ClusterNotes, string? PlanningCenterType);

/// <summary>Creates a node inside a DRAFT model. ModelId comes from the route; TenantId is server-resolved (not a field).</summary>
public sealed record CreateTerritoryNodeCommand(
    Guid ModelId,
    Guid? ParentTerritoryId,
    string TerritoryCode,
    string Name,
    string TerritoryLevel,
    string? CountryCode,
    string? DivisionCode,
    string? RegionCode,
    string? AreaCode,
    string? ZoneCode,
    string? MicroZoneCode,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int SortOrder,
    MicroZoneProfileInput? MicroZoneProfile,
    string? CorrelationId) : IRequest<Response<Guid>>;

/// <summary>Updates a node inside a DRAFT model. Non-draft models are immutable in FU01 (409).</summary>
public sealed record UpdateTerritoryNodeCommand(
    Guid ModelId,
    Guid Id,
    Guid? ParentTerritoryId,
    string TerritoryCode,
    string Name,
    string TerritoryLevel,
    string? CountryCode,
    string? DivisionCode,
    string? RegionCode,
    string? AreaCode,
    string? ZoneCode,
    string? MicroZoneCode,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int SortOrder,
    MicroZoneProfileInput? MicroZoneProfile,
    string? CorrelationId) : IRequest<Response<bool>>;

public sealed record SoftDeleteDraftTerritoryNodeCommand(
    Guid ModelId, Guid Id, string? Reason, string? CorrelationId) : IRequest<Response<bool>>;
