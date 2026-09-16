using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Commands;

public sealed record DeleteVerifiedCertificationRegistryReadinessCommand(Guid Id) : IRequest<Response<bool>>;
