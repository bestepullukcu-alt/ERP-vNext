using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;

public sealed record GetContentSetRevisionByIdQuery(Guid RevisionId) : IRequest<Response<ContentSetRevisionDto>>;

/// <summary>Lists the revisions of one content set, newest-first.</summary>
public sealed record ListContentSetRevisionsQuery(Guid ContentSetId) : IRequest<Response<ContentSetRevisionListDto>>;
