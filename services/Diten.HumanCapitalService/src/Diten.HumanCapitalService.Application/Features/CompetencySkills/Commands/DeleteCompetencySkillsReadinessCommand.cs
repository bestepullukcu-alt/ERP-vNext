using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompetencySkills.Commands;

public sealed record DeleteCompetencySkillsReadinessCommand(Guid Id) : IRequest<Response<bool>>;
