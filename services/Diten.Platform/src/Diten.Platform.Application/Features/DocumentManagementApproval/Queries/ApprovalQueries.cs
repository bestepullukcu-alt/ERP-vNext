using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementApproval.Queries;

// MOD-0029-FU09 — approval read queries (tenant-scoped; no side effects).

public sealed record GetApprovalRequirementsQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<ApprovalRequirementModel>>>;

public sealed record GetApprovalReadinessQuery(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<ApprovalReadinessModel>>;
