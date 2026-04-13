using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class ChangeObjectiveStatusCommand : IRequest<Response<ObjectiveDto>>
{
    public string ObjectiveId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
    public string Actor { get; set; } = "anonymous";
    public string CorrelationId { get; set; } = string.Empty;
}
