using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Type;

/// <summary>Lists concept types for the tenant. Archived rows are included by default so history stays visible.</summary>
public sealed record ListConceptTypesQuery(
    Guid? SubjectId = null,
    string? Status = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<ConceptTypeListDto>>;

public sealed record GetConceptTypeQuery(Guid ConceptTypeId) : IRequest<Response<ConceptTypeDto>>;
