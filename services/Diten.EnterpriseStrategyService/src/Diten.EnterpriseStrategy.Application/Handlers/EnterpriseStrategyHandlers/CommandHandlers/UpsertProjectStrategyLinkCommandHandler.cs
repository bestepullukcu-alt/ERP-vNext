using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class UpsertProjectStrategyLinkCommandHandler : IRequestHandler<UpsertProjectStrategyLinkCommand, Response<ProjectStrategyLinkViewDto>>
{
    private readonly IProjectOrchestrationService _service;

    public UpsertProjectStrategyLinkCommandHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<ProjectStrategyLinkViewDto>> Handle(UpsertProjectStrategyLinkCommand request, CancellationToken cancellationToken) =>
        _service.UpsertStrategyLinkAsync(request.ProjectId, request.Link, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
