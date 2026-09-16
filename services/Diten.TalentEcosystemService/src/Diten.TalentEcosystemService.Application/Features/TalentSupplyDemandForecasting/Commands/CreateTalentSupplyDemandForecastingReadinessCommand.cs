using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting.Commands;

public sealed record CreateTalentSupplyDemandForecastingReadinessCommand(TalentSupplyDemandForecastingReadinessCreateRequest Request) : IRequest<Response<Guid>>;
