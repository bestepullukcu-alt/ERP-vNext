using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Queries;

public sealed record GetConsentVisibilityPolicyAuditMetadataQuery(Guid Id) : IRequest<Response<ConsentVisibilityPolicyAuditMetadataDto>>;
