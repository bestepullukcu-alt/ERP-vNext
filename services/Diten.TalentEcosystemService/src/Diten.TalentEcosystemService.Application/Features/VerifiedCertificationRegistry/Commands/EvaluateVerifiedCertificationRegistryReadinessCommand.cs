using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Commands;

public sealed record EvaluateVerifiedCertificationRegistryReadinessCommand(Guid Id) : IRequest<Response<VerifiedCertificationRegistryReadinessDto>>;
