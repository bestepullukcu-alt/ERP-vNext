using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork.Commands;

public sealed record EvaluateMentorshipRecommendationNetworkReadinessCommand(Guid Id) : IRequest<Response<MentorshipRecommendationNetworkReadinessDto>>;
