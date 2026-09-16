using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Commands;

public sealed record CreateSectorMobilityIntelligenceReadinessCommand(SectorMobilityIntelligenceReadinessCreateRequest Request) : IRequest<Response<Guid>>;
