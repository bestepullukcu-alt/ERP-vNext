using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Commands;

public sealed record DeleteSkillsGapHeatmapReadinessCommand(Guid Id) : IRequest<Response<bool>>;
