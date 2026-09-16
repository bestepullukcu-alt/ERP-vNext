using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Commands;

public sealed record CreateIndustrySkillPassportReadinessCommand(IndustrySkillPassportReadinessCreateRequest Request) : IRequest<Response<Guid>>;
