using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Commands;

public sealed record EvaluateIndustryTalentPoolReadinessCommand(Guid Id) : IRequest<Response<IndustryTalentPoolReadinessDto>>;
