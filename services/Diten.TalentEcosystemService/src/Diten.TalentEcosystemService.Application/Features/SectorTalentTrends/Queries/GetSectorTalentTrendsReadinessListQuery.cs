using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Queries;

public sealed record GetSectorTalentTrendsReadinessListQuery : IRequest<Response<IReadOnlyList<SectorTalentTrendsReadinessListItemDto>>>;
