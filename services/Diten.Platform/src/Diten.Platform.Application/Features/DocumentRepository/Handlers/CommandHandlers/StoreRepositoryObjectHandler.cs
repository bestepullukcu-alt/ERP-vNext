using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentRepository.Commands;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Handlers.CommandHandlers;

public sealed class StoreRepositoryObjectHandler
    : IRequestHandler<StoreRepositoryObjectCommand, Response<RepositoryObjectModel>>
{
    private readonly DocumentRepositoryService _service;

    public StoreRepositoryObjectHandler(DocumentRepositoryService service) => _service = service;

    public Task<Response<RepositoryObjectModel>> Handle(StoreRepositoryObjectCommand request, CancellationToken cancellationToken) =>
        _service.StoreAsync(request.Input, request.CorrelationId, cancellationToken);
}
