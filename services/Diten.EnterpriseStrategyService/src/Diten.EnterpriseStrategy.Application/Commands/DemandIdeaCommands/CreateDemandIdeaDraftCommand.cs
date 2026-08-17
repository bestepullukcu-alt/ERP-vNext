using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using MediatR;

namespace Diten.Application.Commands.DemandIdeaCommands;

public sealed class CreateDemandIdeaDraftCommand : IRequest<Response<DemandIdeaResponseDto>>
{
    public DemandIdeaUpsertRequest Request { get; set; } = new();
    public string? UserId { get; set; }
}
