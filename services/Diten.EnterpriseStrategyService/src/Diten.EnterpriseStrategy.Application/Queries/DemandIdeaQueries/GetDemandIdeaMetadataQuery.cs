using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using MediatR;

namespace Diten.Application.Queries.DemandIdeaQueries;

public sealed class GetDemandIdeaMetadataQuery : IRequest<Response<DemandIdeaMetadataDto>>
{
}
