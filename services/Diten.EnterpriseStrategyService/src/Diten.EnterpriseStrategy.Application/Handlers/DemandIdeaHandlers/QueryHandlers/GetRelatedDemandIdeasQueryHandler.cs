using Diten.Application.Common.Interfaces;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using Diten.Application.Queries.DemandIdeaQueries;
using Diten.Domain.Aggregates.DemandIdea;
using MediatR;

namespace Diten.Application.Handlers.DemandIdeaHandlers.QueryHandlers;

public sealed class GetRelatedDemandIdeasQueryHandler : IRequestHandler<GetRelatedDemandIdeasQuery, Response<IReadOnlyList<RelatedIdeaItemDto>>>
{
    private readonly IRepository<DemandIdeaAggregate> _repository;

    public GetRelatedDemandIdeasQueryHandler(IRepository<DemandIdeaAggregate> repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<RelatedIdeaItemDto>>> Handle(GetRelatedDemandIdeasQuery request, CancellationToken cancellationToken)
    {
        var all = (await _repository.GetAllAsync())
            .Where(x => !x.IsDeleted && x.Id != request.ExcludeId)
            .ToList();

        var scored = all
            .Select(item => new { Item = item, Score = DemandIdeaHandlerSupport.ScoreRelated(request, item) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(request.Take <= 0 ? 5 : request.Take)
            .Select(x => new RelatedIdeaItemDto
            {
                Id = x.Item.Id,
                RecordNumber = x.Item.RecordNumber,
                Title = x.Item.Title,
                MatchScore = Math.Min(100, x.Score)
            })
            .ToList();

        return Response<IReadOnlyList<RelatedIdeaItemDto>>.Ok(scored);
    }
}
