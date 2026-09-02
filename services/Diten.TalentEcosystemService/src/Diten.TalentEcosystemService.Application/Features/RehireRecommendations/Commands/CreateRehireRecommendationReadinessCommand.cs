using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Commands;

public sealed record CreateRehireRecommendationReadinessCommand(RehireRecommendationReadinessRequest Request) : IRequest<Response<Guid>>;
