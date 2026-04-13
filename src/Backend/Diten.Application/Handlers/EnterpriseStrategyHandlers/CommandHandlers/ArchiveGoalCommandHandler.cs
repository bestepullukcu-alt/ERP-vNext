using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class ArchiveGoalCommandHandler : IRequestHandler<ArchiveGoalCommand, Response<GoalDto>>
{
    private readonly IGoalService _service;

    public ArchiveGoalCommandHandler(IGoalService service) => _service = service;

    public Task<Response<GoalDto>> Handle(ArchiveGoalCommand request, CancellationToken cancellationToken) =>
        _service.ArchiveAsync(request.GoalId, request.ExpectedVersion, request.ArchiveGuardEnabled, request.Actor, request.CorrelationId, cancellationToken);
}
