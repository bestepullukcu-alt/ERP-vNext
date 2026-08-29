using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Queries;

// MOD-0029-FU12 — periodic review read queries (tenant-scoped; no side effects).

public sealed record GetPeriodicReviewScheduleQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<PeriodicReviewScheduleModel>>;

public sealed record GetPeriodicReviewEscalationsQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<PeriodicReviewEscalationModel>>>;
