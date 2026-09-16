using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Commands;

public sealed record UpdateConsentVisibilityPolicyCommand(Guid Id, ConsentVisibilityPolicyRequest Request) : IRequest<Response<ConsentVisibilityPolicyDto>>;
