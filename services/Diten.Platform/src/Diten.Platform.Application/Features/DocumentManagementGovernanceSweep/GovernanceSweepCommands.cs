using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementGovernanceSweep;

// MOD-0029-FU32 — MediatR contracts for the governance sweep surface. Thin: every handler delegates to
// DocumentGovernanceSweepService. TenantId is NEVER carried on a request — it is resolved server-side from the
// tenant context.

public sealed record RunAllGovernanceSweepsCommand(GovernanceSweepRunInput Input, string CorrelationId)
    : IRequest<Response<GovernanceSweepRunModel>>;

public sealed record RunPeriodicReviewSweepCommand(GovernanceSweepRunInput Input, string CorrelationId)
    : IRequest<Response<GovernanceSweepRunModel>>;

public sealed record RunExternalDocumentSweepCommand(GovernanceSweepRunInput Input, string CorrelationId)
    : IRequest<Response<GovernanceSweepRunModel>>;

public sealed record RunTemporaryInstructionSweepCommand(GovernanceSweepRunInput Input, string CorrelationId)
    : IRequest<Response<GovernanceSweepRunModel>>;

public sealed record RunDowntimeTemporaryIssueSweepCommand(GovernanceSweepRunInput Input, string CorrelationId)
    : IRequest<Response<GovernanceSweepRunModel>>;

public sealed record RunQualityCapaSweepCommand(GovernanceSweepRunInput Input, string CorrelationId)
    : IRequest<Response<GovernanceSweepRunModel>>;

public sealed record RunSignatureRequestSweepCommand(GovernanceSweepRunInput Input, string CorrelationId)
    : IRequest<Response<GovernanceSweepRunModel>>;

public sealed record RunRetentionEligibilitySweepCommand(GovernanceSweepRunInput Input, string CorrelationId)
    : IRequest<Response<GovernanceSweepRunModel>>;

public sealed record RunLegalHoldScopeSweepCommand(GovernanceSweepRunInput Input, string CorrelationId)
    : IRequest<Response<GovernanceSweepRunModel>>;

/// <summary>Read-only: reports what a run-all would find. Writes nothing, not even a run-history row.</summary>
public sealed record PreviewGovernanceSweepsQuery(GovernanceSweepRunInput Input, string CorrelationId)
    : IRequest<Response<GovernanceSweepRunModel>>;

public sealed record GetGovernanceSweepRunsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<GovernanceSweepRunSummaryModel>>>;

public sealed record GetGovernanceSweepRunByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<GovernanceSweepRunDetailModel>>;

// ── handlers ───────────────────────────────────────────────────────────────────────────────────────────

public sealed class RunAllGovernanceSweepsHandler(DocumentGovernanceSweepService s)
    : IRequestHandler<RunAllGovernanceSweepsCommand, Response<GovernanceSweepRunModel>>
{
    public Task<Response<GovernanceSweepRunModel>> Handle(RunAllGovernanceSweepsCommand r, CancellationToken ct) =>
        s.RunAllAsync(r.Input, r.CorrelationId, ct);
}

public sealed class RunPeriodicReviewSweepHandler(DocumentGovernanceSweepService s)
    : IRequestHandler<RunPeriodicReviewSweepCommand, Response<GovernanceSweepRunModel>>
{
    public Task<Response<GovernanceSweepRunModel>> Handle(RunPeriodicReviewSweepCommand r, CancellationToken ct) =>
        s.RunPeriodicReviewsAsync(r.Input, r.CorrelationId, ct);
}

public sealed class RunExternalDocumentSweepHandler(DocumentGovernanceSweepService s)
    : IRequestHandler<RunExternalDocumentSweepCommand, Response<GovernanceSweepRunModel>>
{
    public Task<Response<GovernanceSweepRunModel>> Handle(RunExternalDocumentSweepCommand r, CancellationToken ct) =>
        s.RunExternalDocumentsAsync(r.Input, r.CorrelationId, ct);
}

public sealed class RunTemporaryInstructionSweepHandler(DocumentGovernanceSweepService s)
    : IRequestHandler<RunTemporaryInstructionSweepCommand, Response<GovernanceSweepRunModel>>
{
    public Task<Response<GovernanceSweepRunModel>> Handle(RunTemporaryInstructionSweepCommand r, CancellationToken ct) =>
        s.RunTemporaryInstructionsAsync(r.Input, r.CorrelationId, ct);
}

public sealed class RunDowntimeTemporaryIssueSweepHandler(DocumentGovernanceSweepService s)
    : IRequestHandler<RunDowntimeTemporaryIssueSweepCommand, Response<GovernanceSweepRunModel>>
{
    public Task<Response<GovernanceSweepRunModel>> Handle(RunDowntimeTemporaryIssueSweepCommand r, CancellationToken ct) =>
        s.RunDowntimeTemporaryIssuesAsync(r.Input, r.CorrelationId, ct);
}

public sealed class RunQualityCapaSweepHandler(DocumentGovernanceSweepService s)
    : IRequestHandler<RunQualityCapaSweepCommand, Response<GovernanceSweepRunModel>>
{
    public Task<Response<GovernanceSweepRunModel>> Handle(RunQualityCapaSweepCommand r, CancellationToken ct) =>
        s.RunCapaAsync(r.Input, r.CorrelationId, ct);
}

public sealed class RunSignatureRequestSweepHandler(DocumentGovernanceSweepService s)
    : IRequestHandler<RunSignatureRequestSweepCommand, Response<GovernanceSweepRunModel>>
{
    public Task<Response<GovernanceSweepRunModel>> Handle(RunSignatureRequestSweepCommand r, CancellationToken ct) =>
        s.RunSignatureRequestsAsync(r.Input, r.CorrelationId, ct);
}

public sealed class RunRetentionEligibilitySweepHandler(DocumentGovernanceSweepService s)
    : IRequestHandler<RunRetentionEligibilitySweepCommand, Response<GovernanceSweepRunModel>>
{
    public Task<Response<GovernanceSweepRunModel>> Handle(RunRetentionEligibilitySweepCommand r, CancellationToken ct) =>
        s.RunRetentionEligibilityAsync(r.Input, r.CorrelationId, ct);
}

public sealed class RunLegalHoldScopeSweepHandler(DocumentGovernanceSweepService s)
    : IRequestHandler<RunLegalHoldScopeSweepCommand, Response<GovernanceSweepRunModel>>
{
    public Task<Response<GovernanceSweepRunModel>> Handle(RunLegalHoldScopeSweepCommand r, CancellationToken ct) =>
        s.RunLegalHoldScopeAsync(r.Input, r.CorrelationId, ct);
}

public sealed class PreviewGovernanceSweepsHandler(DocumentGovernanceSweepService s)
    : IRequestHandler<PreviewGovernanceSweepsQuery, Response<GovernanceSweepRunModel>>
{
    public Task<Response<GovernanceSweepRunModel>> Handle(PreviewGovernanceSweepsQuery r, CancellationToken ct) =>
        s.PreviewAllAsync(r.Input, r.CorrelationId, ct);
}

public sealed class GetGovernanceSweepRunsHandler(DocumentGovernanceSweepService s)
    : IRequestHandler<GetGovernanceSweepRunsQuery, Response<IReadOnlyList<GovernanceSweepRunSummaryModel>>>
{
    public Task<Response<IReadOnlyList<GovernanceSweepRunSummaryModel>>> Handle(
        GetGovernanceSweepRunsQuery r, CancellationToken ct) => s.ListRunsAsync(r.CorrelationId, ct);
}

public sealed class GetGovernanceSweepRunByIdHandler(DocumentGovernanceSweepService s)
    : IRequestHandler<GetGovernanceSweepRunByIdQuery, Response<GovernanceSweepRunDetailModel>>
{
    public Task<Response<GovernanceSweepRunDetailModel>> Handle(
        GetGovernanceSweepRunByIdQuery r, CancellationToken ct) => s.GetRunAsync(r.Id, r.CorrelationId, ct);
}
