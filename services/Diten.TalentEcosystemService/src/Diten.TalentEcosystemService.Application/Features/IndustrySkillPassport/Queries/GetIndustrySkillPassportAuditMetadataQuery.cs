using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Queries;

public sealed record GetIndustrySkillPassportAuditMetadataQuery(Guid Id) : IRequest<Response<IndustrySkillPassportAuditMetadataDto>>;
