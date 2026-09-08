using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Queries;

public sealed record GetRestrictedIntegrityRegistryAuditMetadataQuery(Guid Id) : IRequest<Response<RestrictedIntegrityRegistryAuditMetadataDto>>;
