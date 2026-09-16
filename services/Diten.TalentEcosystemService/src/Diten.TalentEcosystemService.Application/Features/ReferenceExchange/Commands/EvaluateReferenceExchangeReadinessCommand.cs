using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Commands;

public sealed record EvaluateReferenceExchangeReadinessCommand(Guid Id, EvaluateReferenceExchangeReadinessRequest Request)
    : IRequest<Response<ReferenceExchangeEvaluationDto>>;
