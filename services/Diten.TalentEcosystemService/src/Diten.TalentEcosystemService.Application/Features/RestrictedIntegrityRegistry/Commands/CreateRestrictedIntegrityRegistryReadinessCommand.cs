using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Commands;

public sealed record CreateRestrictedIntegrityRegistryReadinessCommand(RestrictedIntegrityRegistryReadinessCreateRequest Request) : IRequest<Response<Guid>>;
