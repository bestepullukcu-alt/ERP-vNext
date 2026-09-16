using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Commands;

public sealed record EvaluateSectorTalentTrendsReadinessCommand(Guid Id) : IRequest<Response<SectorTalentTrendsReadinessDto>>;
