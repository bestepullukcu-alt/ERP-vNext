using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class DeleteConnectionCommandHandler : IRequestHandler<DeleteConnectionCommand, Response<bool>>
{
    private readonly IConnectionService _service;

    public DeleteConnectionCommandHandler(IConnectionService service) => _service = service;

    public Task<Response<bool>> Handle(DeleteConnectionCommand request, CancellationToken cancellationToken) =>
        _service.DeleteAsync(request.ConnectionId, cancellationToken);
}
