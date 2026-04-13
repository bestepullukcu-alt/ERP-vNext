using Diten.Application.Common.Interfaces;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using Diten.Application.Queries.DemandIdeaQueries;
using Diten.Domain.Aggregates.DemandIdea;
using MediatR;

namespace Diten.Application.Handlers.DemandIdeaHandlers.QueryHandlers;

public sealed class CheckDemandIdeaDuplicatesQueryHandler : IRequestHandler<CheckDemandIdeaDuplicatesQuery, Response<IReadOnlyList<DuplicateIdeaItemDto>>>
{
    private readonly IRepository<DemandIdeaAggregate> _repository;

    public CheckDemandIdeaDuplicatesQueryHandler(IRepository<DemandIdeaAggregate> repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<DuplicateIdeaItemDto>>> Handle(CheckDemandIdeaDuplicatesQuery request, CancellationToken cancellationToken)
    {
        var all = (await _repository.GetAllAsync())
            .Where(x => !x.IsDeleted && x.Id != request.ExcludeId)
            .ToList();

        var list = new List<DuplicateIdeaItemDto>();
        foreach (var existing in all)
        {
            var score = DemandIdeaHandlerSupport.ScoreDuplicate(request, existing);
            if (score < 32) continue;

            list.Add(new DuplicateIdeaItemDto
            {
                Id = existing.Id,
                RecordNumber = existing.RecordNumber,
                Title = existing.Title,
                Score = score,
                Reason = DemandIdeaHandlerSupport.BuildDuplicateReason(request, existing)
            });
        }

        var top = list.OrderByDescending(x => x.Score).Take(5).ToList();
        return Response<IReadOnlyList<DuplicateIdeaItemDto>>.Ok(top);
    }
}
