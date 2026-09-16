using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Commands;

public sealed record EvaluateSectorMobilityIntelligenceReadinessCommand(Guid Id) : IRequest<Response<SectorMobilityIntelligenceReadinessDto>>;
