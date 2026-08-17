using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class SyncProjectsCommand : IRequest<Response<SyncResultDto>>
{
    public string CorrelationId { get; set; } = string.Empty;
    public string Actor { get; set; } = "anonymous";
}
