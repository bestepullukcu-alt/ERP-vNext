using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels.Commands;

public sealed record UpdateTrustLevelPolicyCommand(Guid Id, TrustLevelPolicyRequest Request) : IRequest<Response<TrustLevelPolicyDto>>;
