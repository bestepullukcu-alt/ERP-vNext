using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Commands;

public sealed record DeleteIndustryTalentPoolReadinessCommand(Guid Id) : IRequest<Response<bool>>;
