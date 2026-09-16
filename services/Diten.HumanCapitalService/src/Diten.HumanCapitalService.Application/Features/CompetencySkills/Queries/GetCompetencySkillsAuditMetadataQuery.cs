using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompetencySkills.Queries;

public sealed record GetCompetencySkillsAuditMetadataQuery(Guid Id) : IRequest<Response<CompetencySkillsAuditMetadataDto>>;
