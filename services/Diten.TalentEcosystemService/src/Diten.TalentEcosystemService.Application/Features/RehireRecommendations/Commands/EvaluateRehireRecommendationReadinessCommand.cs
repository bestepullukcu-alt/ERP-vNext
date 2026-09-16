using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Commands;

public sealed record EvaluateRehireRecommendationReadinessCommand(Guid Id, EvaluateRehireRecommendationReadinessRequest Request)
    : IRequest<Response<RehireRecommendationEvaluationDto>>;
