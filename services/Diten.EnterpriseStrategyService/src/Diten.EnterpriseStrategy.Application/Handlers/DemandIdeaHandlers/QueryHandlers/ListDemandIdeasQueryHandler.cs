using Diten.Application.Common.Interfaces;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using Diten.Application.Queries.DemandIdeaQueries;
using Diten.Domain.Aggregates.DemandIdea;
using MediatR;

namespace Diten.Application.Handlers.DemandIdeaHandlers.QueryHandlers;

public sealed class ListDemandIdeasQueryHandler : IRequestHandler<ListDemandIdeasQuery, Response<IReadOnlyList<DemandIdeaResponseDto>>>
{
    private readonly IRepository<DemandIdeaAggregate> _repository;

    public ListDemandIdeasQueryHandler(IRepository<DemandIdeaAggregate> repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<DemandIdeaResponseDto>>> Handle(ListDemandIdeasQuery request, CancellationToken cancellationToken)
    {
        var all = await _repository.GetAllAsync();
        var items = all
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedDate)
            .Select(DemandIdeaHandlerSupport.MapToDto)
            .ToList();

        return Response<IReadOnlyList<DemandIdeaResponseDto>>.Ok(items);
    }
}
