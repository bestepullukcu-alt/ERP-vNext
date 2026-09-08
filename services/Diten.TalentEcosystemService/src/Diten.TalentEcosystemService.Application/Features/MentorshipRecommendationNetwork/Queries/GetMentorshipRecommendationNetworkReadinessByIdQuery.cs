using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork.Queries;

public sealed record GetMentorshipRecommendationNetworkReadinessByIdQuery(Guid Id) : IRequest<Response<MentorshipRecommendationNetworkReadinessDto>>;
