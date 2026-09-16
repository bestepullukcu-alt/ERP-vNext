using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Queries;

public sealed record GetSkillsGapHeatmapAuditMetadataQuery(Guid Id) : IRequest<Response<SkillsGapHeatmapAuditMetadataDto>>;
