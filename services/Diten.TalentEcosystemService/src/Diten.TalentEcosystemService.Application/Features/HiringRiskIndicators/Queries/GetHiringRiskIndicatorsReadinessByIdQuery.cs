using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Queries;

public sealed record GetHiringRiskIndicatorsReadinessByIdQuery(Guid Id) : IRequest<Response<HiringRiskIndicatorsReadinessDto>>;
