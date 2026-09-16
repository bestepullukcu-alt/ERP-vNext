using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Queries;

public sealed record GetRehireRecommendationReadinessByIdQuery(Guid Id) : IRequest<Response<RehireRecommendationReadinessDto>>;
