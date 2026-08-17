using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using Diten.Application.Queries.DemandIdeaQueries;
using MediatR;

namespace Diten.Application.Handlers.DemandIdeaHandlers.QueryHandlers;

public sealed class GetDemandIdeaMetadataQueryHandler : IRequestHandler<GetDemandIdeaMetadataQuery, Response<DemandIdeaMetadataDto>>
{
    public Task<Response<DemandIdeaMetadataDto>> Handle(GetDemandIdeaMetadataQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Response<DemandIdeaMetadataDto>.Ok(DemandIdeaHandlerSupport.Metadata()));
    }
}
