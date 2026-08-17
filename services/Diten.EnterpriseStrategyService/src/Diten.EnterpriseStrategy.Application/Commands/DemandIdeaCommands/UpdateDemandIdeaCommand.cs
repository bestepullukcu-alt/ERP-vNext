using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using MediatR;

namespace Diten.Application.Commands.DemandIdeaCommands;

public sealed class UpdateDemandIdeaCommand : IRequest<Response<DemandIdeaResponseDto>>
{
    public string Id { get; set; } = string.Empty;
    public DemandIdeaUpsertRequest Request { get; set; } = new();
    public string? UserId { get; set; }
}
