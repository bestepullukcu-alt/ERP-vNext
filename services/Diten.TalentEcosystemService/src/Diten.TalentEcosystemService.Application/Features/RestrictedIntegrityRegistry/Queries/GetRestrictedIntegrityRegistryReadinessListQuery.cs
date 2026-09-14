using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Queries;

public sealed record GetRestrictedIntegrityRegistryReadinessListQuery : IRequest<Response<IReadOnlyList<RestrictedIntegrityRegistryReadinessListItemDto>>>;
