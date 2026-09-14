using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Queries;

public sealed record GetRehireRecommendationReadinessListQuery : IRequest<Response<IReadOnlyList<RehireRecommendationReadinessListItemDto>>>;
