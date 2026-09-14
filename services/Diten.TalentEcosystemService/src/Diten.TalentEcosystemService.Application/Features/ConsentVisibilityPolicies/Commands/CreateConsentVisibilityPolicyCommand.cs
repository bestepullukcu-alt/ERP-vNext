using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Commands;

public sealed record CreateConsentVisibilityPolicyCommand(ConsentVisibilityPolicyRequest Request) : IRequest<Response<Guid>>;
