using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class ArchiveObjectiveCommandHandler : IRequestHandler<ArchiveObjectiveCommand, Response<ObjectiveDto>>
{
    private readonly IObjectiveService _service;

    public ArchiveObjectiveCommandHandler(IObjectiveService service) => _service = service;

    public Task<Response<ObjectiveDto>> Handle(ArchiveObjectiveCommand request, CancellationToken cancellationToken) =>
        _service.ArchiveAsync(request.ObjectiveId, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
