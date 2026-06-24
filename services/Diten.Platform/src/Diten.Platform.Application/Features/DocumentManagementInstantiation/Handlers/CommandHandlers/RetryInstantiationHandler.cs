using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Commands;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Handlers.CommandHandlers;

public sealed class RetryInstantiationHandler
    : IRequestHandler<RetryInstantiationCommand, Response<InstantiationResultModel>>
{
    private readonly InstantiationService _service;

    public RetryInstantiationHandler(InstantiationService service)
    {
        _service = service;
    }

    public Task<Response<InstantiationResultModel>> Handle(RetryInstantiationCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _service.RetryAsync(request.OperationId, request.NodeKeys, request.CorrelationId, ct);
    }
}
