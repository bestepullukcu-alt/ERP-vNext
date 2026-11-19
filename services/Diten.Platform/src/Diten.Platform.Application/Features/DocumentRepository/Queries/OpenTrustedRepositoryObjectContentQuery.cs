using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Queries;

/// <summary>
/// MOD-0262-FU01 DL-2 — opens the stored bytes for a trusted consumer. Same visibility as the metadata read. The read
/// is audited by the handler (central trail, <c>document-repository.trusted-consumer.content-read</c>) once the stream
/// is open and before the first byte leaves (§21).
/// </summary>
public sealed record OpenTrustedRepositoryObjectContentQuery(
    Guid ContentId,
    TrustedRepositoryConsumer Consumer,
    string CorrelationId,
    Guid AuditCorrelationId)
    : IRequest<Response<RepositoryObjectContentHandle>>;
