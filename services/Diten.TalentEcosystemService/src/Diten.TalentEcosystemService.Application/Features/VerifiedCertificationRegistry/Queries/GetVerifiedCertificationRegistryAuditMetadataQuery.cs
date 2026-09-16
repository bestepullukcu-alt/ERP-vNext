using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Queries;

public sealed record GetVerifiedCertificationRegistryAuditMetadataQuery(Guid Id) : IRequest<Response<VerifiedCertificationRegistryAuditMetadataDto>>;
