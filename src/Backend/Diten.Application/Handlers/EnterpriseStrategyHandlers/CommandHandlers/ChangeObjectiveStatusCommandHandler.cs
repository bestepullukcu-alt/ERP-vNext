using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class ChangeObjectiveStatusCommandHandler : IRequestHandler<ChangeObjectiveStatusCommand, Response<ObjectiveDto>>
{
    private readonly IObjectiveService _service;

    public ChangeObjectiveStatusCommandHandler(IObjectiveService service) => _service = service;

    public Task<Response<ObjectiveDto>> Handle(ChangeObjectiveStatusCommand request, CancellationToken cancellationToken) =>
        _service.ChangeStatusAsync(request.ObjectiveId, request.Status, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
