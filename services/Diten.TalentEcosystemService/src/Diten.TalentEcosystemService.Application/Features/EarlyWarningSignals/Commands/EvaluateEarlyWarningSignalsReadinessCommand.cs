using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Commands;

public sealed record EvaluateEarlyWarningSignalsReadinessCommand(Guid Id) : IRequest<Response<EarlyWarningSignalsReadinessDto>>;
