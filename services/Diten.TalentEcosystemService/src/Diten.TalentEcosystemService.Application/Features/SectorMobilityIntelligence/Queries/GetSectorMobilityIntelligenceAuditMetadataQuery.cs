using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Queries;

public sealed record GetSectorMobilityIntelligenceAuditMetadataQuery(Guid Id) : IRequest<Response<SectorMobilityIntelligenceAuditMetadataDto>>;
