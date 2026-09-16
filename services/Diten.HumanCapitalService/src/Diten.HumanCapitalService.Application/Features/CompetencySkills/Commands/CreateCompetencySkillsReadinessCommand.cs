using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompetencySkills.Commands;

public sealed record CreateCompetencySkillsReadinessCommand(CompetencySkillsReadinessCreateRequest Request) : IRequest<Response<Guid>>;
