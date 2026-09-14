using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Queries;

public sealed record GetRestrictedIntegrityRegistryReadinessByIdQuery(Guid Id) : IRequest<Response<RestrictedIntegrityRegistryReadinessDto>>;
