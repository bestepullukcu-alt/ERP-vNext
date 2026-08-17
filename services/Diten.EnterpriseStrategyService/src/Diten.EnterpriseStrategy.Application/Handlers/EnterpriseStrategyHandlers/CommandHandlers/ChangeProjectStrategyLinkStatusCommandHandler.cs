using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class ChangeProjectStrategyLinkStatusCommandHandler : IRequestHandler<ChangeProjectStrategyLinkStatusCommand, Response<ProjectStrategyLinkViewDto>>
{
    private readonly IProjectOrchestrationService _service;

    public ChangeProjectStrategyLinkStatusCommandHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<ProjectStrategyLinkViewDto>> Handle(ChangeProjectStrategyLinkStatusCommand request, CancellationToken cancellationToken) =>
        _service.ChangeStrategyLinkStatusAsync(request.ProjectId, request.Status, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
