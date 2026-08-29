using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementRetention.Commands;

// MOD-0029-FU15 — retention / legal hold / disposition commands. Auditable via the central AuditBehavior.
// There is deliberately NO delete or purge command in this file: FU15 records retention decisions, it never
// destroys a record.

internal static class RetentionAudit
{
    public const string Module = "MOD-0029-FU15";
    public static Guid? Correlation(string? c) => Guid.TryParse(c, out var g) ? g : null;
    public static AuditRequestMetadata Meta(AuditOperation op, string entityType, Guid entityId, string correlationId) =>
        new(AuditCategory.DocumentManagement, op, entityType, EntityId: entityId, SourceModule: Module, CorrelationId: Correlation(correlationId));
}

// ── retention policies ───────────────────────────────────────────────────────

public sealed record CreateRetentionPolicyCommand(RetentionPolicyFieldsInput Input, string CorrelationId)
    : IRequest<Response<RetentionPolicyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Create, "DocumentRetentionPolicy", Guid.Empty, CorrelationId);
}

public sealed record UpdateRetentionPolicyCommand(Guid Id, RetentionPolicyFieldsInput Input, string CorrelationId)
    : IRequest<Response<RetentionPolicyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Update, "DocumentRetentionPolicy", Id, CorrelationId);
}

public sealed record ActivateRetentionPolicyCommand(Guid Id, string CorrelationId)
    : IRequest<Response<RetentionPolicyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Update, "DocumentRetentionPolicy", Id, CorrelationId);
}

public sealed record RetireRetentionPolicyCommand(Guid Id, string CorrelationId)
    : IRequest<Response<RetentionPolicyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Update, "DocumentRetentionPolicy", Id, CorrelationId);
}

// ── evaluation ───────────────────────────────────────────────────────────────

/// <summary>Opt-in evaluation. Nothing evaluates automatically — there is no scheduler in this FU.</summary>
public sealed record EvaluateRetentionSubjectCommand(EvaluateRetentionInput Input, string CorrelationId)
    : IRequest<Response<RetentionSubjectModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Execute, "DocumentRetentionSubject", Input.SubjectId, CorrelationId);
}

// ── legal holds ──────────────────────────────────────────────────────────────

public sealed record CreateLegalHoldCommand(LegalHoldFieldsInput Input, string CorrelationId)
    : IRequest<Response<LegalHoldModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Create, "DocumentLegalHold", Guid.Empty, CorrelationId);
}

public sealed record ActivateLegalHoldCommand(Guid Id, ActivateLegalHoldInput Input, string CorrelationId)
    : IRequest<Response<LegalHoldModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Update, "DocumentLegalHold", Id, CorrelationId);
}

/// <summary>SOP §22 — requires Legal release approval AND GQD concurrence. Audited as a distinct operation.</summary>
public sealed record ReleaseLegalHoldCommand(Guid Id, ReleaseLegalHoldInput Input, string CorrelationId)
    : IRequest<Response<LegalHoldModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Update, "DocumentLegalHold", Id, CorrelationId);
}

public sealed record AddLegalHoldSubjectCommand(Guid HoldId, string SubjectType, Guid SubjectId, Guid? RegisterEntryId, string CorrelationId)
    : IRequest<Response<LegalHoldSubjectModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Assign, "DocumentLegalHoldSubject", HoldId, CorrelationId);
}

// ── disposition ──────────────────────────────────────────────────────────────

public sealed record CreateDispositionRequestCommand(CreateDispositionRequestInput Input, string CorrelationId)
    : IRequest<Response<DispositionRequestModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Create, "DocumentDispositionRequest", Input.SubjectId, CorrelationId);
}

public sealed record SubmitDispositionRequestCommand(Guid Id, string CorrelationId)
    : IRequest<Response<DispositionRequestModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Update, "DocumentDispositionRequest", Id, CorrelationId);
}

public sealed record ApproveDispositionRequestCommand(Guid Id, ApproveDispositionInput Input, string CorrelationId)
    : IRequest<Response<DispositionRequestModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Update, "DocumentDispositionRequest", Id, CorrelationId);
}

public sealed record RejectDispositionRequestCommand(Guid Id, RejectDispositionInput Input, string CorrelationId)
    : IRequest<Response<DispositionRequestModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Update, "DocumentDispositionRequest", Id, CorrelationId);
}

/// <summary>
/// Writes a disposition EVIDENCE MARKER. Despite the name this deletes nothing — the audit operation is Update,
/// never Delete, because no record is removed.
/// </summary>
public sealed record ExecuteDispositionMarkerCommand(Guid Id, ExecuteDispositionMarkerInput Input, string CorrelationId)
    : IRequest<Response<DispositionRequestModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RetentionAudit.Meta(AuditOperation.Update, "DocumentDispositionRequest", Id, CorrelationId);
}
