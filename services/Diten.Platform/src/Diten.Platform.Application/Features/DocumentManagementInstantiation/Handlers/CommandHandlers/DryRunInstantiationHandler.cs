using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Commands;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Handlers.CommandHandlers;

public sealed class DryRunInstantiationHandler
    : IRequestHandler<DryRunInstantiationCommand, Response<InstantiationResultModel>>
{
    private readonly InstantiationService _service;

    public DryRunInstantiationHandler(InstantiationService service)
    {
        _service = service;
    }

    public Task<Response<InstantiationResultModel>> Handle(DryRunInstantiationCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _service.DryRunAsync(request.BaselineReleaseId, request.Scope, request.Selection, request.CorrelationId, ct);
    }
}
