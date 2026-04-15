using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class UpsertProjectStrategyLinkCommand : IRequest<Response<ProjectStrategyLinkViewDto>>
{
    public string ProjectId { get; set; } = string.Empty;
    public ProjectStrategyLinkViewDto Link { get; set; } = new();
    public int ExpectedVersion { get; set; }
    public string Actor { get; set; } = "anonymous";
    public string CorrelationId { get; set; } = string.Empty;
}
