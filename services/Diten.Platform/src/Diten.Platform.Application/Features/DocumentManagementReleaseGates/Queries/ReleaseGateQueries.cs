using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementReleaseGates.Queries;

// MOD-0029-FU10 — release gate read queries (tenant-scoped; readiness/latest/history do not persist a new evaluation
// except where explicitly evaluated).

public sealed record GetLatestReleaseGateEvaluationQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<ReleaseGateEvaluationModel>>;

public sealed record GetReleaseGateHistoryQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<ReleaseGateEvaluationModel>>>;

public sealed record GetReleaseReadinessQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<ReleaseGateEvaluationModel>>;
