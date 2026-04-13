using Diten.Application.Common.Interfaces;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using Diten.Application.Queries.DemandIdeaQueries;
using Diten.Domain.Aggregates.DemandIdea;
using MediatR;

namespace Diten.Application.Handlers.DemandIdeaHandlers.QueryHandlers;

public sealed class GetDemandIdeaByIdQueryHandler : IRequestHandler<GetDemandIdeaByIdQuery, Response<DemandIdeaResponseDto>>
{
    private readonly IRepository<DemandIdeaAggregate> _repository;

    public GetDemandIdeaByIdQueryHandler(IRepository<DemandIdeaAggregate> repository)
    {
        _repository = repository;
    }

    public async Task<Response<DemandIdeaResponseDto>> Handle(GetDemandIdeaByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id);
        if (entity == null || entity.IsDeleted)
        {
            return Response<DemandIdeaResponseDto>.Fail(ResultErrorCodes.NotFound);
        }

        return Response<DemandIdeaResponseDto>.Ok(DemandIdeaHandlerSupport.MapToDto(entity));
    }
}
