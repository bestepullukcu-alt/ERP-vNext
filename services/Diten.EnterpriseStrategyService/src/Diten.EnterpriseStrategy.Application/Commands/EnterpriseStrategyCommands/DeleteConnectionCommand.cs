using Diten.Application.Common.Models;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class DeleteConnectionCommand : IRequest<Response<bool>>
{
    public string ConnectionId { get; set; } = string.Empty;
}
