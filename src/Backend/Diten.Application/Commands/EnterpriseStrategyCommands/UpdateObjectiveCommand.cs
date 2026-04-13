using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class UpdateObjectiveCommand : IRequest<Response<ObjectiveDto>>
{
    public string ObjectiveId { get; set; } = string.Empty;
    public ObjectiveDto Objective { get; set; } = new();
    public int ExpectedVersion { get; set; }
    public string Actor { get; set; } = "anonymous";
    public string CorrelationId { get; set; } = string.Empty;
}
