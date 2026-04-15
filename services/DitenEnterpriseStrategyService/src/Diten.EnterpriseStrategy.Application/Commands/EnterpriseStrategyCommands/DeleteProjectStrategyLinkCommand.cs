using Diten.Application.Common.Models;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class DeleteProjectStrategyLinkCommand : IRequest<Response<bool>>
{
    public string ProjectId { get; set; } = string.Empty;
    public string Actor { get; set; } = "anonymous";
    public string CorrelationId { get; set; } = string.Empty;
}
