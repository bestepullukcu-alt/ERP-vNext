using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Queries;

public sealed record GetConsentVisibilityPolicyListQuery : IRequest<Response<IReadOnlyList<ConsentVisibilityPolicyListItemDto>>>;
