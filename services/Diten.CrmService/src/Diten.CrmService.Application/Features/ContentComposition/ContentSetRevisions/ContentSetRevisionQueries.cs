using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.Rendering;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;

public sealed record GetContentSetRevisionByIdQuery(Guid RevisionId) : IRequest<Response<ContentSetRevisionDto>>;

/// <summary>Lists the revisions of one content set, newest-first.</summary>
public sealed record ListContentSetRevisionsQuery(Guid ContentSetId) : IRequest<Response<ContentSetRevisionListDto>>;

/// <summary>SCMM-16B — opens the rendered artifact's byte stream for a revision. The content id is resolved from the
/// revision's bound artifact (never a client input); another tenant's revision resolves to 404 (non-leakage), and an
/// unrendered revision is 404.</summary>
public sealed record GetContentSetRevisionArtifactQuery(Guid RevisionId) : IRequest<Response<ContentArtifactReadResult>>;
