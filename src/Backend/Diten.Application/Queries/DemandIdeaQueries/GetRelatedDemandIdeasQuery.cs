using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using MediatR;

namespace Diten.Application.Queries.DemandIdeaQueries;

public sealed class GetRelatedDemandIdeasQuery : IRequest<Response<IReadOnlyList<RelatedIdeaItemDto>>>
{
    public string? Title { get; set; }
    public string? RequestType { get; set; }
    public string? BusinessUnit { get; set; }
    public string? StrategicAlignment { get; set; }
    public List<string>? Tags { get; set; }
    public string? ExcludeId { get; set; }
    public int Take { get; set; } = 5;
}
