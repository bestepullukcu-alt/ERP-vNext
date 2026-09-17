using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Queries;

public sealed record GetEarlyWarningSignalsReadinessByIdQuery(Guid Id) : IRequest<Response<EarlyWarningSignalsReadinessDto>>;
