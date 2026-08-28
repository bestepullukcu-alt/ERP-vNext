using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementDowntime.Queries;

// MOD-0029-FU20 — downtime / temporary controlled issue read queries (tenant-scoped; no side effects).

public sealed record GetRepositoryDowntimeEventsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<DowntimeEventModel>>>;

public sealed record GetRepositoryDowntimeEventByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<DowntimeEventModel>>;

public sealed record GetDowntimeEscalationsQuery(Guid Id, string CorrelationId)
    : IRequest<Response<IReadOnlyList<DowntimeEscalationModel>>>;

public sealed record GetTemporaryControlledIssuesQuery(Guid DowntimeEventId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<TemporaryControlledIssueModel>>>;
