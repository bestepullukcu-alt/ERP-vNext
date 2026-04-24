using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class CreateStrategyLinkedProjectCommandHandler : IRequestHandler<CreateStrategyLinkedProjectCommand, Response<ProjectStrategyLinkViewDto>>
{
    private readonly IProjectOrchestrationService _service;

    public CreateStrategyLinkedProjectCommandHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<ProjectStrategyLinkViewDto>> Handle(CreateStrategyLinkedProjectCommand request, CancellationToken cancellationToken) =>
        _service.CreateStrategyLinkedAsync(request.Project, request.StrategyContext, request.Actor, request.CorrelationId, cancellationToken);
}
