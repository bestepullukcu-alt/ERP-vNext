using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Readiness;

public sealed record GetAccountCoverageReadinessQuery(Guid AccountId, DateTimeOffset? EffectiveAt, string? BusinessUnit)
    : IRequest<Response<TerritoryReadinessResultDto>>;

public sealed record GetNodeCoverageAccountsQuery(Guid NodeId, DateTimeOffset? EffectiveAt, string? BusinessUnit, bool IncludeNonReady)
    : IRequest<Response<TerritoryReadinessResultDto>>;

public sealed record GetResourceCoverageReadinessQuery(string ResourceId, DateTimeOffset? EffectiveAt, string? BusinessUnit, bool IncludeNonReady)
    : IRequest<Response<TerritoryReadinessResultDto>>;

public sealed record GetContactTerritoryCoverageQuery(Guid ContactId, DateTimeOffset? EffectiveAt, string? BusinessUnit, string? Date, string? Weekday)
    : IRequest<Response<TerritoryReadinessResultDto>>;

public sealed record GetRouteCandidatesQuery(
    DateTimeOffset? EffectiveAt,
    string? BusinessUnit,
    Guid? TerritoryModelId,
    Guid? TerritoryNodeId,
    string? ResourceId,
    Guid? AccountId,
    Guid? ContactId,
    string? Date,
    string? Weekday,
    bool IncludeNonReady)
    : IRequest<Response<TerritoryReadinessResultDto>>;
