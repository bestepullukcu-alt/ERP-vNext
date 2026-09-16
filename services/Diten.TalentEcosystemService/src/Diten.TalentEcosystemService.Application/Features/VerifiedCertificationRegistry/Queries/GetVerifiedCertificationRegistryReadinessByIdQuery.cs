using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Queries;

public sealed record GetVerifiedCertificationRegistryReadinessByIdQuery(Guid Id) : IRequest<Response<VerifiedCertificationRegistryReadinessDto>>;
