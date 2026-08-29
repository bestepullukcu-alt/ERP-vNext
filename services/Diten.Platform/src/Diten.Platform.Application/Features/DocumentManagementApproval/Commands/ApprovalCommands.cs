using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementApproval.Commands;

// MOD-0029-FU09 — approval route + evidence commands. Auditable via the central AuditBehavior. No hard delete.

internal static class ApprovalAudit
{
    public const string Module = "MOD-0029-FU09";
    public static Guid? Correlation(string? correlationId) => Guid.TryParse(correlationId, out var c) ? c : null;
}

public sealed record ResolveApprovalRouteCommand(Guid RegisterEntryId, ResolveApprovalRouteInput Input, string CorrelationId)
    : IRequest<Response<IReadOnlyList<ApprovalRequirementModel>>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "DocumentApprovalRoute",
        EntityId: RegisterEntryId, SourceModule: ApprovalAudit.Module, CorrelationId: ApprovalAudit.Correlation(CorrelationId));
}

public sealed record RecordApprovalEvidenceCommand(Guid RegisterEntryId, RecordApprovalEvidenceInput Input, string CorrelationId)
    : IRequest<Response<ApprovalReadinessModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Assign, "DocumentApprovalEvidence",
        EntityId: RegisterEntryId, SourceModule: ApprovalAudit.Module, CorrelationId: ApprovalAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["requirementId"] = Input.RequirementId, ["action"] = Input.Action });
}

public sealed record RejectApprovalCommand(Guid RegisterEntryId, RejectApprovalInput Input, string CorrelationId)
    : IRequest<Response<ApprovalReadinessModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "DocumentApprovalEvidence",
        EntityId: RegisterEntryId, SourceModule: ApprovalAudit.Module, CorrelationId: ApprovalAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["requirementId"] = Input.RequirementId, ["action"] = "Rejected" });
}
