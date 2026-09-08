using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompetencySkills.Queries;

public sealed record GetCompetencySkillsReadinessListQuery : IRequest<Response<IReadOnlyList<CompetencySkillsReadinessListItemDto>>>;
