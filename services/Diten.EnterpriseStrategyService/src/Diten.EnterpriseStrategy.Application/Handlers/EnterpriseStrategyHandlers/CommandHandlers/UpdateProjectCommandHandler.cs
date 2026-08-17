using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, Response<ProjectStrategyLinkViewDto>>
{
    private readonly IProjectOrchestrationService _service;

    public UpdateProjectCommandHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<ProjectStrategyLinkViewDto>> Handle(UpdateProjectCommand request, CancellationToken cancellationToken) =>
        _service.UpdateAsync(request.ProjectId, request.Project, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
