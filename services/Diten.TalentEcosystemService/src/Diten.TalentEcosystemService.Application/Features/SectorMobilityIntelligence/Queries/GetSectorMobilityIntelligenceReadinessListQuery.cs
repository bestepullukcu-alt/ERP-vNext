using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Queries;

public sealed record GetSectorMobilityIntelligenceReadinessListQuery : IRequest<Response<IReadOnlyList<SectorMobilityIntelligenceReadinessListItemDto>>>;
