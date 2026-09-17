using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Commands;

public sealed record UpdateRehireRecommendationReadinessCommand(Guid Id, RehireRecommendationReadinessRequest Request) : IRequest<Response<NoContent>>;
