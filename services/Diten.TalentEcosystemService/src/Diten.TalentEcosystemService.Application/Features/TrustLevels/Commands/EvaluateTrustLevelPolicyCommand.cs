using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels.Commands;

public sealed record EvaluateTrustLevelPolicyCommand(Guid Id, EvaluateTrustLevelPolicyRequest Request)
    : IRequest<Response<TrustLevelEvaluationDto>>;
