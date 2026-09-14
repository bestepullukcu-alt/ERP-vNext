using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Queries;

public sealed record GetHiringRiskIndicatorsAuditMetadataQuery(Guid Id) : IRequest<Response<HiringRiskIndicatorsAuditMetadataDto>>;
