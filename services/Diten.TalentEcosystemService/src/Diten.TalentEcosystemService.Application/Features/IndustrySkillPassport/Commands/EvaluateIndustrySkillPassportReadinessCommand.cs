using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Commands;

public sealed record EvaluateIndustrySkillPassportReadinessCommand(Guid Id) : IRequest<Response<IndustrySkillPassportReadinessDto>>;
