using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementReconciliation.Commands;

// MOD-0028-FU09 — reconciliation, evidence, and deviation commands/queries. Handlers stay thin (delegate to services).

public sealed record ReconciliationDryRunCommand(ReconciliationRequest Request, string CorrelationId)
    : IRequest<Response<ReconciliationResult>>;

public sealed record ReconciliationApplyFindingsCommand(ReconciliationRequest Request, string CorrelationId)
    : IRequest<Response<ReconciliationResult>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "CollectionReconciliation",
        EntityId: Request.BaselineReleaseId, SourceModule: "MOD-0028",
        CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}

public sealed record UpsertProvisioningEvidenceCommand(EvidenceUpsertInput Input, string CorrelationId)
    : IRequest<Response<ProvisioningEvidenceModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "ProvisioningEvidence",
        EntityId: Input.CollectionInstanceId, SourceModule: "MOD-0028",
        CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}

public sealed record MarkPermissionsAppliedCommand(Guid EvidenceId, string CorrelationId)
    : IRequest<Response<ProvisioningEvidenceModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "ProvisioningEvidence",
        EntityId: EvidenceId, SourceModule: "MOD-0028", CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}

public sealed record MarkQaVerifiedCommand(Guid EvidenceId, string CorrelationId)
    : IRequest<Response<ProvisioningEvidenceModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "ProvisioningEvidence",
        EntityId: EvidenceId, SourceModule: "MOD-0028", CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}

public sealed record GetDeviationsQuery(Guid BaselineReleaseId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<DeviationModel>>>;

public sealed record ResolveDeviationCommand(Guid DeviationId, string? Comment, string CorrelationId)
    : IRequest<Response<DeviationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "CollectionDeviation",
        EntityId: DeviationId, SourceModule: "MOD-0028", CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}

public sealed record AcceptDeviationCommand(Guid DeviationId, string? Comment, string CorrelationId)
    : IRequest<Response<DeviationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "CollectionDeviation",
        EntityId: DeviationId, SourceModule: "MOD-0028", CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}

public sealed record GetQualificationReadinessQuery(Guid BaselineReleaseId, string CorrelationId)
    : IRequest<Response<QualificationReadinessModel>>;

public sealed record GetProvisioningEvidenceQuery(Guid BaselineReleaseId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<ProvisioningEvidenceModel>>>;
