using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Commands;

// MOD-0029-FU21 — GDocP correction trail commands. These are ADDITIVE to the central AuditBehavior, not a
// replacement for it: the audit event still records that the command ran, while the correction record captures
// the regulated field change itself. No command here deletes anything.

internal static class GDocPCorrectionAudit
{
    public const string Module = "MOD-0029-FU21";
    public static Guid? Correlation(string? c) => Guid.TryParse(c, out var g) ? g : null;
    public static AuditRequestMetadata Meta(AuditOperation op, string entityType, Guid entityId, string correlationId) =>
        new(AuditCategory.DocumentManagement, op, entityType, EntityId: entityId, SourceModule: Module, CorrelationId: Correlation(correlationId));
}

// ── policies ─────────────────────────────────────────────────────────────────

public sealed record CreateGDocPCorrectionPolicyCommand(GDocPCorrectionPolicyInput Input, string CorrelationId)
    : IRequest<Response<GDocPCorrectionPolicyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        GDocPCorrectionAudit.Meta(AuditOperation.Create, "DocumentGDocPCorrectionPolicy", Guid.Empty, CorrelationId);
}

public sealed record ActivateGDocPCorrectionPolicyCommand(Guid Id, string CorrelationId)
    : IRequest<Response<GDocPCorrectionPolicyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        GDocPCorrectionAudit.Meta(AuditOperation.Update, "DocumentGDocPCorrectionPolicy", Id, CorrelationId);
}

public sealed record RetireGDocPCorrectionPolicyCommand(Guid Id, string CorrelationId)
    : IRequest<Response<GDocPCorrectionPolicyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        GDocPCorrectionAudit.Meta(AuditOperation.Update, "DocumentGDocPCorrectionPolicy", Id, CorrelationId);
}

// ── correction records ───────────────────────────────────────────────────────

/// <summary>Records a field correction. CorrectedAt is server-stamped and cannot be supplied by the caller.</summary>
public sealed record RecordGDocPCorrectionCommand(RecordGDocPCorrectionInput Input, string CorrelationId)
    : IRequest<Response<GDocPCorrectionRecordModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        GDocPCorrectionAudit.Meta(AuditOperation.Create, "DocumentGDocPCorrectionRecord", Input.SubjectId, CorrelationId);
}

public sealed record ReviewGDocPCorrectionCommand(Guid Id, ReviewGDocPCorrectionInput Input, string CorrelationId)
    : IRequest<Response<GDocPCorrectionRecordModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        GDocPCorrectionAudit.Meta(AuditOperation.Update, "DocumentGDocPCorrectionRecord", Id, CorrelationId);
}

public sealed record RejectGDocPCorrectionCommand(Guid Id, RejectGDocPCorrectionInput Input, string CorrelationId)
    : IRequest<Response<GDocPCorrectionRecordModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        GDocPCorrectionAudit.Meta(AuditOperation.Update, "DocumentGDocPCorrectionRecord", Id, CorrelationId);
}
