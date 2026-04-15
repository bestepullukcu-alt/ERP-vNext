using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class CreateGoalCommandHandler : IRequestHandler<CreateGoalCommand, Response<GoalDto>>
{
    private readonly IGoalService _service;

    public CreateGoalCommandHandler(IGoalService service)
    {
        _service = service;
    }

    public Task<Response<GoalDto>> Handle(CreateGoalCommand request, CancellationToken cancellationToken) =>
        _service.CreateAsync(request.Goal, request.Actor, request.CorrelationId, cancellationToken);
}
