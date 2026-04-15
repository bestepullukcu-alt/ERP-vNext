using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Response<ProjectStrategyLinkViewDto>>
{
    private readonly IProjectOrchestrationService _service;

    public CreateProjectCommandHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<ProjectStrategyLinkViewDto>> Handle(CreateProjectCommand request, CancellationToken cancellationToken) =>
        _service.CreateAsync(request.Project, request.Actor, request.CorrelationId, cancellationToken);
}
