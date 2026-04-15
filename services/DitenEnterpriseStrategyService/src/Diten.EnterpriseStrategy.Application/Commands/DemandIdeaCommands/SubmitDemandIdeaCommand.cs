using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using MediatR;

namespace Diten.Application.Commands.DemandIdeaCommands;

public sealed class SubmitDemandIdeaCommand : IRequest<Response<DemandIdeaResponseDto>>
{
    public string Id { get; set; } = string.Empty;
    public string? UserId { get; set; }
}
