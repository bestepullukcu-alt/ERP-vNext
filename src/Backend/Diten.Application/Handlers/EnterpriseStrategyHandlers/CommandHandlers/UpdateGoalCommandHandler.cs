using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class UpdateGoalCommandHandler : IRequestHandler<UpdateGoalCommand, Response<GoalDto>>
{
    private readonly IGoalService _service;

    public UpdateGoalCommandHandler(IGoalService service)
    {
        _service = service;
    }

    public Task<Response<GoalDto>> Handle(UpdateGoalCommand request, CancellationToken cancellationToken) =>
        _service.UpdateAsync(request.GoalId, request.Goal, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
