using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Queries;

public sealed record GetRehireRecommendationAuditMetadataQuery(Guid Id) : IRequest<Response<RehireRecommendationAuditMetadataDto>>;
