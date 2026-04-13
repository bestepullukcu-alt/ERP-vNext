using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class ChangeGoalStatusCommandHandler : IRequestHandler<ChangeGoalStatusCommand, Response<GoalDto>>
{
    private readonly IGoalService _service;

    public ChangeGoalStatusCommandHandler(IGoalService service)
    {
        _service = service;
    }

    public Task<Response<GoalDto>> Handle(ChangeGoalStatusCommand request, CancellationToken cancellationToken) =>
        _service.ChangeStatusAsync(request.GoalId, request.Status, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
