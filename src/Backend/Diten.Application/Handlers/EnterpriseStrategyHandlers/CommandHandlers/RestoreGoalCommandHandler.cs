using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class RestoreGoalCommandHandler : IRequestHandler<RestoreGoalCommand, Response<GoalDto>>
{
    private readonly IGoalService _service;

    public RestoreGoalCommandHandler(IGoalService service) => _service = service;

    public Task<Response<GoalDto>> Handle(RestoreGoalCommand request, CancellationToken cancellationToken) =>
        _service.RestoreAsync(request.GoalId, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
