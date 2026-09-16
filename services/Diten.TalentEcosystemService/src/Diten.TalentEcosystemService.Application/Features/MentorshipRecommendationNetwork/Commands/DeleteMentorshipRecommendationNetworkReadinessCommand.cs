using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork.Commands;

public sealed record DeleteMentorshipRecommendationNetworkReadinessCommand(Guid Id) : IRequest<Response<bool>>;
