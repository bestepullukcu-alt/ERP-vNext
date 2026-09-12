using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Queries;

/// <summary>
/// MOD-0262-FU01 — resolves a content id to a readable stream. The object key is looked up from the
/// tenant-scoped row, never supplied by the caller, so holding a raw key grants no access.
/// </summary>
public sealed record OpenRepositoryObjectContentQuery(Guid ContentId, string CorrelationId)
    : IRequest<Response<RepositoryObjectContentHandle>>;
