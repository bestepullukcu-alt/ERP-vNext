using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting.Queries;

public sealed record GetTalentSupplyDemandForecastingAuditMetadataQuery(Guid Id) : IRequest<Response<TalentSupplyDemandForecastingAuditMetadataDto>>;
