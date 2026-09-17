using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Commands;

public sealed record CreateIndustryTalentPoolReadinessCommand(IndustryTalentPoolReadinessCreateRequest Request) : IRequest<Response<Guid>>;
