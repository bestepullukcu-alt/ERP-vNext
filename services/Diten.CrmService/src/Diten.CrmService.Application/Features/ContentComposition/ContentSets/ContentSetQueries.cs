using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSets;

/// <summary>Lists content sets for the tenant. Archived rows included by default so history stays visible.</summary>
public sealed record ListContentSetsQuery(
    string? Status = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<ContentSetListDto>>;

public sealed record GetContentSetQuery(Guid ContentSetId) : IRequest<Response<ContentSetDto>>;
