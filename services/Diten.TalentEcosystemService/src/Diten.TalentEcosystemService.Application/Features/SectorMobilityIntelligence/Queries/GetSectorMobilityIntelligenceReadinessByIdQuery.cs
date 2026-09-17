using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Queries;

public sealed record GetSectorMobilityIntelligenceReadinessByIdQuery(Guid Id) : IRequest<Response<SectorMobilityIntelligenceReadinessDto>>;
