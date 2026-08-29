using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementLifecycle.Queries;

// MOD-0029-FU08 — lifecycle read queries (tenant-scoped; no side effects).

public sealed record GetLifecycleStateQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<LifecycleStateModel>>;

public sealed record GetLifecycleTransitionsQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<LifecycleTransitionRecordModel>>>;
