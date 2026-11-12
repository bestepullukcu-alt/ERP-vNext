using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Queries;

/// <summary>
/// MOD-0262-FU01 DL-2 — pointer metadata for a trusted consumer. Sees only the row its consumer module wrote, in a scope
/// the consumer may use; every other row is 404. A successful metadata read is not audited (§21).
/// </summary>
public sealed record GetTrustedRepositoryObjectByIdQuery(Guid ContentId, TrustedRepositoryConsumer Consumer, string CorrelationId)
    : IRequest<Response<RepositoryObjectModel>>;
