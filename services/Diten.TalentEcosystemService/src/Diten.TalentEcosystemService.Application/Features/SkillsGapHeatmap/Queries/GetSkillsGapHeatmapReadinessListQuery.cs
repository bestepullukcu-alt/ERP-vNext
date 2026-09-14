using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Queries;

public sealed record GetSkillsGapHeatmapReadinessListQuery : IRequest<Response<IReadOnlyList<SkillsGapHeatmapReadinessListItemDto>>>;
