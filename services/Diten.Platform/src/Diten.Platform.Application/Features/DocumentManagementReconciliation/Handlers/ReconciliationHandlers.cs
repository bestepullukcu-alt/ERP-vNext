using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementReconciliation.Commands;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementReconciliation.Handlers;

public sealed class ReconciliationDryRunHandler(CollectionTreeReconciliationService service)
    : IRequestHandler<ReconciliationDryRunCommand, Response<ReconciliationResult>>
{
    public Task<Response<ReconciliationResult>> Handle(ReconciliationDryRunCommand request, CancellationToken ct) =>
        service.RunAsync(request.Request with { DryRun = true }, apply: false, request.CorrelationId, ct);
}

public sealed class ReconciliationApplyFindingsHandler(CollectionTreeReconciliationService service)
    : IRequestHandler<ReconciliationApplyFindingsCommand, Response<ReconciliationResult>>
{
    public Task<Response<ReconciliationResult>> Handle(ReconciliationApplyFindingsCommand request, CancellationToken ct) =>
        service.RunAsync(request.Request with { DryRun = false }, apply: true, request.CorrelationId, ct);
}

public sealed class UpsertProvisioningEvidenceHandler(ProvisioningEvidenceService service)
    : IRequestHandler<UpsertProvisioningEvidenceCommand, Response<ProvisioningEvidenceModel>>
{
    public Task<Response<ProvisioningEvidenceModel>> Handle(UpsertProvisioningEvidenceCommand request, CancellationToken ct) =>
        service.UpsertAsync(request.Input, request.CorrelationId, ct);
}

public sealed class MarkPermissionsAppliedHandler(ProvisioningEvidenceService service)
    : IRequestHandler<MarkPermissionsAppliedCommand, Response<ProvisioningEvidenceModel>>
{
    public Task<Response<ProvisioningEvidenceModel>> Handle(MarkPermissionsAppliedCommand request, CancellationToken ct) =>
        service.MarkPermissionsAppliedAsync(request.EvidenceId, request.CorrelationId, ct);
}

public sealed class MarkQaVerifiedHandler(ProvisioningEvidenceService service)
    : IRequestHandler<MarkQaVerifiedCommand, Response<ProvisioningEvidenceModel>>
{
    public Task<Response<ProvisioningEvidenceModel>> Handle(MarkQaVerifiedCommand request, CancellationToken ct) =>
        service.MarkQaVerifiedAsync(request.EvidenceId, request.CorrelationId, ct);
}

public sealed class GetDeviationsHandler(DeviationWorkflowService service)
    : IRequestHandler<GetDeviationsQuery, Response<IReadOnlyList<DeviationModel>>>
{
    public Task<Response<IReadOnlyList<DeviationModel>>> Handle(GetDeviationsQuery request, CancellationToken ct) =>
        service.ListByBaselineAsync(request.BaselineReleaseId, request.CorrelationId, ct);
}

public sealed class ResolveDeviationHandler(DeviationWorkflowService service)
    : IRequestHandler<ResolveDeviationCommand, Response<DeviationModel>>
{
    public Task<Response<DeviationModel>> Handle(ResolveDeviationCommand request, CancellationToken ct) =>
        service.ResolveAsync(request.DeviationId, request.Comment, request.CorrelationId, ct);
}

public sealed class AcceptDeviationHandler(DeviationWorkflowService service)
    : IRequestHandler<AcceptDeviationCommand, Response<DeviationModel>>
{
    public Task<Response<DeviationModel>> Handle(AcceptDeviationCommand request, CancellationToken ct) =>
        service.AcceptAsync(request.DeviationId, request.Comment, request.CorrelationId, ct);
}

public sealed class GetQualificationReadinessHandler(BaselineQualificationReadinessService service)
    : IRequestHandler<GetQualificationReadinessQuery, Response<QualificationReadinessModel>>
{
    public Task<Response<QualificationReadinessModel>> Handle(GetQualificationReadinessQuery request, CancellationToken ct) =>
        service.EvaluateAsync(request.BaselineReleaseId, request.CorrelationId, ct);
}

public sealed class GetProvisioningEvidenceHandler(ProvisioningEvidenceService service)
    : IRequestHandler<GetProvisioningEvidenceQuery, Response<IReadOnlyList<ProvisioningEvidenceModel>>>
{
    public Task<Response<IReadOnlyList<ProvisioningEvidenceModel>>> Handle(GetProvisioningEvidenceQuery request, CancellationToken ct) =>
        service.ListByBaselineAsync(request.BaselineReleaseId, request.CorrelationId, ct);
}
