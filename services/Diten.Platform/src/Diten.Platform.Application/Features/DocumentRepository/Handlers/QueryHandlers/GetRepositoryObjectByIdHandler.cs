using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentRepository.Queries;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Handlers.QueryHandlers;

public sealed class GetRepositoryObjectByIdHandler
    : IRequestHandler<GetRepositoryObjectByIdQuery, Response<RepositoryObjectModel>>
{
    private readonly DocumentRepositoryService _service;

    public GetRepositoryObjectByIdHandler(DocumentRepositoryService service) => _service = service;

    public Task<Response<RepositoryObjectModel>> Handle(GetRepositoryObjectByIdQuery request, CancellationToken cancellationToken) =>
        _service.GetAsync(request.ContentId, request.CorrelationId, cancellationToken);
}
