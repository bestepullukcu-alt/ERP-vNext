using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Link;

/// <summary>MOD-0162 FU03 content ↔ concept link write surface. Always anchored to a node; an optional relationship
/// context must contain that node. Archived content / archived node accept no new link. <c>TenantId</c> server-resolved.</summary>
public sealed record CreateKnowledgeContentConceptLinkCommand(
    Guid KnowledgeContentId,
    Guid ConceptNodeId,
    Guid? ConceptRelationshipId = null,
    string? LinkRole = null,
    int SortOrder = 0) : IRequest<Response<Guid>>;

public sealed record ArchiveKnowledgeContentConceptLinkCommand(Guid LinkId) : IRequest<Response<bool>>;
