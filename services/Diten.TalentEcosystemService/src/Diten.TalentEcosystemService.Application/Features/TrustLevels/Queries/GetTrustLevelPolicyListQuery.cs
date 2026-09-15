using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels.Queries;

public sealed record GetTrustLevelPolicyListQuery : IRequest<Response<IReadOnlyList<TrustLevelPolicyListItemDto>>>;
