using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Queries;

public sealed record GetIndustrySkillPassportReadinessByIdQuery(Guid Id) : IRequest<Response<IndustrySkillPassportReadinessDto>>;
