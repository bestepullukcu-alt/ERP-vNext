using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Commands;

public sealed record CreateSkillsGapHeatmapReadinessCommand(SkillsGapHeatmapReadinessCreateRequest Request) : IRequest<Response<Guid>>;
