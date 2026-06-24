using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Commands;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Handlers.CommandHandlers;

public sealed class ExecuteInstantiationHandler
    : IRequestHandler<ExecuteInstantiationCommand, Response<InstantiationResultModel>>
{
    private readonly InstantiationService _service;

    public ExecuteInstantiationHandler(InstantiationService service)
    {
        _service = service;
    }

    public Task<Response<InstantiationResultModel>> Handle(ExecuteInstantiationCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _service.ExecuteAsync(request.BaselineReleaseId, request.Scope, request.Selection, request.CorrelationId, ct);
    }
}
