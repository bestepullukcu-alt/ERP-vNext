using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentRepository.Queries;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Handlers.QueryHandlers;

public sealed class OpenRepositoryObjectContentHandler
    : IRequestHandler<OpenRepositoryObjectContentQuery, Response<RepositoryObjectContentHandle>>
{
    private readonly DocumentRepositoryService _service;

    public OpenRepositoryObjectContentHandler(DocumentRepositoryService service) => _service = service;

    public Task<Response<RepositoryObjectContentHandle>> Handle(OpenRepositoryObjectContentQuery request, CancellationToken cancellationToken) =>
        _service.OpenReadAsync(request.ContentId, request.CorrelationId, cancellationToken);
}
