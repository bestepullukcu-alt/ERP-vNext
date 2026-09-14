using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Commands;

public sealed record DeleteHiringRiskIndicatorsReadinessCommand(Guid Id) : IRequest<Response<bool>>;
