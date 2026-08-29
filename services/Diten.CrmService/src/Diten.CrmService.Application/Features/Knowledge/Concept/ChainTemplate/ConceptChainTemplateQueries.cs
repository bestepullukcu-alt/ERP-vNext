using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.ChainTemplate;

/// <summary>Lists chain templates for the tenant. Archived rows included by default. <c>effectiveAt</c> filters to
/// templates effective at the instant (in-memory).</summary>
public sealed record ListConceptChainTemplatesQuery(
    Guid? SubjectId = null,
    string? Status = null,
    DateTimeOffset? EffectiveAt = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<ConceptChainTemplateListDto>>;

public sealed record GetConceptChainTemplateQuery(Guid ConceptChainTemplateId)
    : IRequest<Response<ConceptChainTemplateDto>>;
