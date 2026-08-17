using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using MediatR;

namespace Diten.Application.Queries.DemandIdeaQueries;

public sealed class GetDemandIdeaByIdQuery : IRequest<Response<DemandIdeaResponseDto>>
{
    public string Id { get; set; } = string.Empty;
}
