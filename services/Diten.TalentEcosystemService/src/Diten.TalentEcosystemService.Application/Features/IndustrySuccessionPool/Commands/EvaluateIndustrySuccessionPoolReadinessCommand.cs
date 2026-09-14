using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Commands;

public sealed record EvaluateIndustrySuccessionPoolReadinessCommand(Guid Id) : IRequest<Response<IndustrySuccessionPoolReadinessDto>>;
