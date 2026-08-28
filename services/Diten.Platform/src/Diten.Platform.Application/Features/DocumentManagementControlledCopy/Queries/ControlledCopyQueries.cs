using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledCopy.Queries;

// MOD-0029-FU17 — controlled copy read queries (tenant-scoped; no side effects).

public sealed record GetControlledCopiesQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<ControlledCopyModel>>>;

public sealed record GetWithdrawalPlansQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<WithdrawalPlanModel>>>;

public sealed record GetCopyWithdrawalReadinessQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<CopyWithdrawalReadinessModel>>;

public sealed record GetObsoleteCopyFindingsQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<ObsoleteCopyFindingModel>>>;
