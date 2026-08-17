using Diten.Application.Common.Models;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class DeleteInitiativeStrategyLinkCommand : IRequest<Response<bool>>
{
    public string InitiativeId { get; set; } = string.Empty;
    public string Actor { get; set; } = "anonymous";
    public string CorrelationId { get; set; } = string.Empty;
}
