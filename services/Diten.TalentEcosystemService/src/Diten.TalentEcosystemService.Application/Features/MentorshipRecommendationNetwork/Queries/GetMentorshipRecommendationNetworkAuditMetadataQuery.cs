using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork.Queries;

public sealed record GetMentorshipRecommendationNetworkAuditMetadataQuery(Guid Id) : IRequest<Response<MentorshipRecommendationNetworkAuditMetadataDto>>;
