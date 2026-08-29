using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Queries;

/// <summary>A lightweight period picker for consumer UIs. It exposes exactly what the read seam exposes and no more.
/// Like the grid it FILTERS by scope and never resolves: a picker shows what exists, and choosing is the human's job.
/// </summary>
public sealed record GetCyclePeriodSelectorQuery(
    int? Year,
    string? CycleStatus,
    string? ScopeType,
    string? Country,
    Guid? LegalEntityId,
    string? BusinessUnitId) : IRequest<Response<CyclePeriodSelectorDto>>;
