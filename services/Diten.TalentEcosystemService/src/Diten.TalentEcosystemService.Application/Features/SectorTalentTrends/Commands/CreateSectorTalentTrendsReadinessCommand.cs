using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Commands;

public sealed record CreateSectorTalentTrendsReadinessCommand(SectorTalentTrendsReadinessCreateRequest Request) : IRequest<Response<Guid>>;
