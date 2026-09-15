using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Commands;

public sealed record EvaluateRestrictedIntegrityRegistryReadinessCommand(Guid Id) : IRequest<Response<RestrictedIntegrityRegistryReadinessDto>>;
