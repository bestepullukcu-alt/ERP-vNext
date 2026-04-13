using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class CreateObjectiveCommandHandler : IRequestHandler<CreateObjectiveCommand, Response<ObjectiveDto>>
{
    private readonly IObjectiveService _service;

    public CreateObjectiveCommandHandler(IObjectiveService service) => _service = service;

    public Task<Response<ObjectiveDto>> Handle(CreateObjectiveCommand request, CancellationToken cancellationToken) =>
        _service.CreateAsync(request.Objective, request.Actor, request.CorrelationId, cancellationToken);
}
