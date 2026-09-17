using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Commands;

public sealed record DeleteRestrictedIntegrityRegistryReadinessCommand(Guid Id) : IRequest<Response<bool>>;
