using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class ChangeInitiativeStrategyLinkStatusCommand : IRequest<Response<InitiativeStrategyLinkViewDto>>
{
    public string InitiativeId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
    public string Actor { get; set; } = "anonymous";
    public string CorrelationId { get; set; } = string.Empty;
}
