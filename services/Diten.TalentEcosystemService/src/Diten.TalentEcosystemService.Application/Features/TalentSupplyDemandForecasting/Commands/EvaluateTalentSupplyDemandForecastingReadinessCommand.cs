using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting.Commands;

public sealed record EvaluateTalentSupplyDemandForecastingReadinessCommand(Guid Id) : IRequest<Response<TalentSupplyDemandForecastingReadinessDto>>;
