using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class RestoreObjectiveCommandHandler : IRequestHandler<RestoreObjectiveCommand, Response<ObjectiveDto>>
{
    private readonly IObjectiveService _service;

    public RestoreObjectiveCommandHandler(IObjectiveService service) => _service = service;

    public Task<Response<ObjectiveDto>> Handle(RestoreObjectiveCommand request, CancellationToken cancellationToken) =>
        _service.RestoreAsync(request.ObjectiveId, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
