using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementGovernancePolicyPack;

// MOD-0029-FU31A — MediatR contracts for the governance policy pack surface. Thin: every handler delegates to
// DocumentGovernancePolicyPackApplicationService. TenantId is NEVER carried on a request — it is resolved
// server-side from the tenant context.

/// <summary>Read-only: computes what an apply would do. Writes nothing.</summary>
public sealed record PreviewGovernancePolicyPackQuery(string CorrelationId)
    : IRequest<Response<GovernancePolicyPackPreviewModel>>;

/// <summary>Creates the missing default policies and records an append-only history row.</summary>
public sealed record ApplyGovernancePolicyPackCommand(string CorrelationId)
    : IRequest<Response<GovernancePolicyPackApplyModel>>;

public sealed record GetGovernancePolicyPackApplicationsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<GovernancePolicyPackApplicationSummaryModel>>>;

public sealed record GetGovernancePolicyPackApplicationByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<GovernancePolicyPackApplicationDetailModel>>;

// ── handlers ───────────────────────────────────────────────────────────────────────────────────────────

public sealed class PreviewGovernancePolicyPackHandler(DocumentGovernancePolicyPackApplicationService s)
    : IRequestHandler<PreviewGovernancePolicyPackQuery, Response<GovernancePolicyPackPreviewModel>>
{
    public Task<Response<GovernancePolicyPackPreviewModel>> Handle(PreviewGovernancePolicyPackQuery r, CancellationToken ct) =>
        s.PreviewAsync(r.CorrelationId, ct);
}

public sealed class ApplyGovernancePolicyPackHandler(DocumentGovernancePolicyPackApplicationService s)
    : IRequestHandler<ApplyGovernancePolicyPackCommand, Response<GovernancePolicyPackApplyModel>>
{
    public Task<Response<GovernancePolicyPackApplyModel>> Handle(ApplyGovernancePolicyPackCommand r, CancellationToken ct) =>
        s.ApplyAsync(r.CorrelationId, ct);
}

public sealed class GetGovernancePolicyPackApplicationsHandler(DocumentGovernancePolicyPackApplicationService s)
    : IRequestHandler<GetGovernancePolicyPackApplicationsQuery, Response<IReadOnlyList<GovernancePolicyPackApplicationSummaryModel>>>
{
    public Task<Response<IReadOnlyList<GovernancePolicyPackApplicationSummaryModel>>> Handle(
        GetGovernancePolicyPackApplicationsQuery r, CancellationToken ct) => s.ListApplicationsAsync(r.CorrelationId, ct);
}

public sealed class GetGovernancePolicyPackApplicationByIdHandler(DocumentGovernancePolicyPackApplicationService s)
    : IRequestHandler<GetGovernancePolicyPackApplicationByIdQuery, Response<GovernancePolicyPackApplicationDetailModel>>
{
    public Task<Response<GovernancePolicyPackApplicationDetailModel>> Handle(
        GetGovernancePolicyPackApplicationByIdQuery r, CancellationToken ct) => s.GetApplicationAsync(r.Id, r.CorrelationId, ct);
}
