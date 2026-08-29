using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementDowntime.Commands;
using Diten.Platform.Application.Features.DocumentManagementDowntime.Queries;
using Diten.Platform.Application.Features.DocumentManagementDowntime.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementDowntime.Handlers;

// MOD-0029-FU20 — thin MediatR handlers delegating to the downtime and temporary issue services.

public sealed class OpenRepositoryDowntimeEventHandler(DocumentRepositoryDowntimeService s)
    : IRequestHandler<OpenRepositoryDowntimeEventCommand, Response<DowntimeEventModel>>
{
    public Task<Response<DowntimeEventModel>> Handle(OpenRepositoryDowntimeEventCommand r, CancellationToken ct) =>
        s.OpenAsync(r.Input, r.CorrelationId, ct);
}

public sealed class MarkRepositoryRestoredHandler(DocumentRepositoryDowntimeService s)
    : IRequestHandler<MarkRepositoryRestoredCommand, Response<DowntimeEventModel>>
{
    public Task<Response<DowntimeEventModel>> Handle(MarkRepositoryRestoredCommand r, CancellationToken ct) =>
        s.MarkRestoredAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class EvaluateDowntimeEscalationHandler(DocumentRepositoryDowntimeService s)
    : IRequestHandler<EvaluateDowntimeEscalationCommand, Response<DowntimeEscalationEvaluationModel>>
{
    public Task<Response<DowntimeEscalationEvaluationModel>> Handle(EvaluateDowntimeEscalationCommand r, CancellationToken ct) =>
        s.EvaluateEscalationAsync(r.Id, r.CorrelationId, ct);
}

public sealed class CloseRepositoryDowntimeEventHandler(DocumentRepositoryDowntimeService s)
    : IRequestHandler<CloseRepositoryDowntimeEventCommand, Response<DowntimeEventModel>>
{
    public Task<Response<DowntimeEventModel>> Handle(CloseRepositoryDowntimeEventCommand r, CancellationToken ct) =>
        s.CloseAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class GetRepositoryDowntimeEventsHandler(DocumentRepositoryDowntimeService s)
    : IRequestHandler<GetRepositoryDowntimeEventsQuery, Response<IReadOnlyList<DowntimeEventModel>>>
{
    public Task<Response<IReadOnlyList<DowntimeEventModel>>> Handle(GetRepositoryDowntimeEventsQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetRepositoryDowntimeEventByIdHandler(DocumentRepositoryDowntimeService s)
    : IRequestHandler<GetRepositoryDowntimeEventByIdQuery, Response<DowntimeEventModel>>
{
    public Task<Response<DowntimeEventModel>> Handle(GetRepositoryDowntimeEventByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetDowntimeEscalationsHandler(DocumentRepositoryDowntimeService s)
    : IRequestHandler<GetDowntimeEscalationsQuery, Response<IReadOnlyList<DowntimeEscalationModel>>>
{
    public Task<Response<IReadOnlyList<DowntimeEscalationModel>>> Handle(GetDowntimeEscalationsQuery r, CancellationToken ct) =>
        s.GetEscalationsAsync(r.Id, r.CorrelationId, ct);
}

public sealed class RequestTemporaryControlledIssueHandler(DocumentTemporaryIssueService s)
    : IRequestHandler<RequestTemporaryControlledIssueCommand, Response<TemporaryControlledIssueModel>>
{
    public Task<Response<TemporaryControlledIssueModel>> Handle(RequestTemporaryControlledIssueCommand r, CancellationToken ct) =>
        s.RequestAsync(r.DowntimeEventId, r.Input, r.CorrelationId, ct);
}

public sealed class ApproveTemporaryControlledIssueHandler(DocumentTemporaryIssueService s)
    : IRequestHandler<ApproveTemporaryControlledIssueCommand, Response<TemporaryControlledIssueModel>>
{
    public Task<Response<TemporaryControlledIssueModel>> Handle(ApproveTemporaryControlledIssueCommand r, CancellationToken ct) =>
        s.ApproveAsync(r.DowntimeEventId, r.IssueId, r.Input, r.CorrelationId, ct);
}

public sealed class IssueTemporaryControlledCopyHandler(DocumentTemporaryIssueService s)
    : IRequestHandler<IssueTemporaryControlledCopyCommand, Response<TemporaryControlledIssueModel>>
{
    public Task<Response<TemporaryControlledIssueModel>> Handle(IssueTemporaryControlledCopyCommand r, CancellationToken ct) =>
        s.IssueCopiesAsync(r.DowntimeEventId, r.IssueId, r.Input, r.CorrelationId, ct);
}

public sealed class ReconcileTemporaryControlledIssueHandler(DocumentTemporaryIssueService s)
    : IRequestHandler<ReconcileTemporaryControlledIssueCommand, Response<TemporaryControlledIssueModel>>
{
    public Task<Response<TemporaryControlledIssueModel>> Handle(ReconcileTemporaryControlledIssueCommand r, CancellationToken ct) =>
        s.ReconcileAsync(r.DowntimeEventId, r.IssueId, r.Input, r.CorrelationId, ct);
}

public sealed class EvaluateTemporaryIssueOverdueHandler(DocumentTemporaryIssueService s)
    : IRequestHandler<EvaluateTemporaryIssueOverdueCommand, Response<TemporaryControlledIssueModel>>
{
    public Task<Response<TemporaryControlledIssueModel>> Handle(EvaluateTemporaryIssueOverdueCommand r, CancellationToken ct) =>
        s.EvaluateOverdueAsync(r.DowntimeEventId, r.IssueId, r.CorrelationId, ct);
}

public sealed class CancelTemporaryControlledIssueHandler(DocumentTemporaryIssueService s)
    : IRequestHandler<CancelTemporaryControlledIssueCommand, Response<TemporaryControlledIssueModel>>
{
    public Task<Response<TemporaryControlledIssueModel>> Handle(CancelTemporaryControlledIssueCommand r, CancellationToken ct) =>
        s.CancelAsync(r.DowntimeEventId, r.IssueId, r.Input, r.CorrelationId, ct);
}

public sealed class GetTemporaryControlledIssuesHandler(DocumentTemporaryIssueService s)
    : IRequestHandler<GetTemporaryControlledIssuesQuery, Response<IReadOnlyList<TemporaryControlledIssueModel>>>
{
    public Task<Response<IReadOnlyList<TemporaryControlledIssueModel>>> Handle(GetTemporaryControlledIssuesQuery r, CancellationToken ct) =>
        s.GetByDowntimeEventAsync(r.DowntimeEventId, r.CorrelationId, ct);
}
