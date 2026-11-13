using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentRepository.Queries;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Handlers.QueryHandlers;

/// <summary>MOD-0262-FU01 DL-2 — trusted consumer metadata read (never <c>ObjectKey</c>); not audited on success (§21).</summary>
public sealed class GetTrustedRepositoryObjectByIdHandler
    : IRequestHandler<GetTrustedRepositoryObjectByIdQuery, Response<RepositoryObjectModel>>
{
    private readonly DocumentRepositoryService _service;

    public GetTrustedRepositoryObjectByIdHandler(DocumentRepositoryService service) => _service = service;

    public Task<Response<RepositoryObjectModel>> Handle(GetTrustedRepositoryObjectByIdQuery request, CancellationToken cancellationToken) =>
        _service.GetForTrustedConsumerAsync(request.ContentId, request.Consumer, request.CorrelationId, cancellationToken);
}
