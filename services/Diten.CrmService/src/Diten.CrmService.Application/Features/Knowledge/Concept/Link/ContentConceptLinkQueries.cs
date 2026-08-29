using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Link;

/// <summary>Lists content ↔ concept links for the tenant. Archived rows included by default.</summary>
public sealed record ListContentConceptLinksQuery(
    Guid? ContentId = null,
    Guid? ConceptNodeId = null,
    string? LinkRole = null,
    bool IncludeArchived = true) : IRequest<Response<KnowledgeContentConceptLinkListDto>>;
