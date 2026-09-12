using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Queries;

/// <summary>MOD-0262-FU01 — pointer metadata for one stored object. Never returns the internal object key.</summary>
public sealed record GetRepositoryObjectByIdQuery(Guid ContentId, string CorrelationId)
    : IRequest<Response<RepositoryObjectModel>>;
