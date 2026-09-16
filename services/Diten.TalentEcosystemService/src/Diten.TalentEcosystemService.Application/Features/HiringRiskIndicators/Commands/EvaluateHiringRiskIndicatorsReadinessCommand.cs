using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Commands;

public sealed record EvaluateHiringRiskIndicatorsReadinessCommand(Guid Id) : IRequest<Response<HiringRiskIndicatorsReadinessDto>>;
