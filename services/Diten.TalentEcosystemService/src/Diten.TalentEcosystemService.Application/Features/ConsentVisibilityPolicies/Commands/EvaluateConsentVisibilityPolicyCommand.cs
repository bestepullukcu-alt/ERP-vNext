using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Commands;

public sealed record EvaluateConsentVisibilityPolicyCommand(Guid Id, EvaluateConsentVisibilityPolicyRequest Request)
    : IRequest<Response<ConsentVisibilityPolicyEvaluationDto>>;
