using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class UpdateObjectiveCommandHandler : IRequestHandler<UpdateObjectiveCommand, Response<ObjectiveDto>>
{
    private readonly IObjectiveService _service;

    public UpdateObjectiveCommandHandler(IObjectiveService service) => _service = service;

    public Task<Response<ObjectiveDto>> Handle(UpdateObjectiveCommand request, CancellationToken cancellationToken) =>
        _service.UpdateAsync(request.ObjectiveId, request.Objective, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
