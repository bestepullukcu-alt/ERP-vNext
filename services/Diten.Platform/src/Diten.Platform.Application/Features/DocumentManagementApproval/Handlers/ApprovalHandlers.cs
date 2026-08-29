using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementApproval.Commands;
using Diten.Platform.Application.Features.DocumentManagementApproval.Queries;
using Diten.Platform.Application.Features.DocumentManagementApproval.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementApproval.Handlers;

// MOD-0029-FU09 — thin MediatR handlers delegating to DocumentApprovalService.

public sealed class ResolveApprovalRouteHandler(DocumentApprovalService service)
    : IRequestHandler<ResolveApprovalRouteCommand, Response<IReadOnlyList<ApprovalRequirementModel>>>
{
    public Task<Response<IReadOnlyList<ApprovalRequirementModel>>> Handle(ResolveApprovalRouteCommand request, CancellationToken ct) =>
        service.ResolveRouteAsync(request.RegisterEntryId, request.Input, request.CorrelationId, ct);
}

public sealed class RecordApprovalEvidenceHandler(DocumentApprovalService service)
    : IRequestHandler<RecordApprovalEvidenceCommand, Response<ApprovalReadinessModel>>
{
    public Task<Response<ApprovalReadinessModel>> Handle(RecordApprovalEvidenceCommand request, CancellationToken ct) =>
        service.RecordEvidenceAsync(request.RegisterEntryId, request.Input, request.CorrelationId, ct);
}

public sealed class RejectApprovalHandler(DocumentApprovalService service)
    : IRequestHandler<RejectApprovalCommand, Response<ApprovalReadinessModel>>
{
    public Task<Response<ApprovalReadinessModel>> Handle(RejectApprovalCommand request, CancellationToken ct) =>
        service.RejectAsync(request.RegisterEntryId, request.Input, request.CorrelationId, ct);
}

public sealed class GetApprovalRequirementsHandler(DocumentApprovalService service)
    : IRequestHandler<GetApprovalRequirementsQuery, Response<IReadOnlyList<ApprovalRequirementModel>>>
{
    public Task<Response<IReadOnlyList<ApprovalRequirementModel>>> Handle(GetApprovalRequirementsQuery request, CancellationToken ct) =>
        service.GetRequirementsAsync(request.RegisterEntryId, request.CorrelationId, ct);
}

public sealed class GetApprovalReadinessHandler(DocumentApprovalService service)
    : IRequestHandler<GetApprovalReadinessQuery, Response<ApprovalReadinessModel>>
{
    public Task<Response<ApprovalReadinessModel>> Handle(GetApprovalReadinessQuery request, CancellationToken ct) =>
        service.GetReadinessAsync(request.RegisterEntryId, request.CorrelationId, ct);
}
