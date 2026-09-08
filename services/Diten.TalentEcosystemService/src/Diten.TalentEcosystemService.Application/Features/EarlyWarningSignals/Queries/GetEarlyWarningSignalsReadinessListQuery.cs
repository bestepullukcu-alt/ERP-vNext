using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Queries;

public sealed record GetEarlyWarningSignalsReadinessListQuery : IRequest<Response<IReadOnlyList<EarlyWarningSignalsReadinessListItemDto>>>;
