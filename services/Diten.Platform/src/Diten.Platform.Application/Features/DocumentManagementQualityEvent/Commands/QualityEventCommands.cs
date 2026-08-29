using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQualityEvent.Commands;

// MOD-0029-FU22 — quality event / deviation / CAPA commands. Auditable via the central AuditBehavior.
// No command deletes anything; cancellation and closure are status changes.

internal static class QualityEventAudit
{
    public const string Module = "MOD-0029-FU22";
    public static Guid? Correlation(string? c) => Guid.TryParse(c, out var g) ? g : null;
    public static AuditRequestMetadata Meta(AuditOperation op, string entityType, Guid entityId, string correlationId) =>
        new(AuditCategory.DocumentManagement, op, entityType, EntityId: entityId, SourceModule: Module, CorrelationId: Correlation(correlationId));
}

// ── quality events ───────────────────────────────────────────────────────────

public sealed record CreateDocumentQualityEventCommand(CreateQualityEventInput Input, string CorrelationId)
    : IRequest<Response<QualityEventModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Create, "DocumentQualityEvent", Guid.Empty, CorrelationId);
}

public sealed record OpenDocumentQualityEventCommand(Guid Id, string CorrelationId)
    : IRequest<Response<QualityEventModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentQualityEvent", Id, CorrelationId);
}

public sealed record CloseDocumentQualityEventCommand(Guid Id, CloseQualityEventInput Input, string CorrelationId)
    : IRequest<Response<QualityEventModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentQualityEvent", Id, CorrelationId);
}

public sealed record CancelDocumentQualityEventCommand(Guid Id, CancelQualityEventInput Input, string CorrelationId)
    : IRequest<Response<QualityEventModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentQualityEvent", Id, CorrelationId);
}

public sealed record LinkQualityEventSourceCommand(Guid Id, LinkQualityEventSourceInput Input, string CorrelationId)
    : IRequest<Response<QualityEventSourceLinkModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Assign, "DocumentQualityEventSourceLink", Id, CorrelationId);
}

// ── deviations ───────────────────────────────────────────────────────────────

public sealed record CreateDocumentDeviationCommand(CreateDeviationInput Input, string CorrelationId)
    : IRequest<Response<DeviationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Create, "DocumentDeviation", Input.QualityEventId, CorrelationId);
}

public sealed record OpenDocumentDeviationCommand(Guid Id, string CorrelationId)
    : IRequest<Response<DeviationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentDeviation", Id, CorrelationId);
}

public sealed record RecordDeviationInvestigationCommand(Guid Id, RecordDeviationInvestigationInput Input, string CorrelationId)
    : IRequest<Response<DeviationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentDeviation", Id, CorrelationId);
}

public sealed record RequireCAPAForDeviationCommand(Guid Id, string CorrelationId)
    : IRequest<Response<DeviationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentDeviation", Id, CorrelationId);
}

public sealed record CloseDocumentDeviationCommand(Guid Id, CloseDeviationInput Input, string CorrelationId)
    : IRequest<Response<DeviationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentDeviation", Id, CorrelationId);
}

public sealed record CancelDocumentDeviationCommand(Guid Id, CancelDeviationInput Input, string CorrelationId)
    : IRequest<Response<DeviationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentDeviation", Id, CorrelationId);
}

// ── CAPA actions ─────────────────────────────────────────────────────────────

public sealed record CreateDocumentCAPAActionCommand(CreateCapaActionInput Input, string CorrelationId)
    : IRequest<Response<CapaActionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Create, "DocumentCAPAAction", Guid.Empty, CorrelationId);
}

public sealed record StartCAPAActionCommand(Guid Id, string CorrelationId)
    : IRequest<Response<CapaActionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentCAPAAction", Id, CorrelationId);
}

public sealed record CompleteCAPAActionCommand(Guid Id, CompleteCapaActionInput Input, string CorrelationId)
    : IRequest<Response<CapaActionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentCAPAAction", Id, CorrelationId);
}

public sealed record RecordCAPAEffectivenessCommand(Guid Id, RecordCapaEffectivenessInput Input, string CorrelationId)
    : IRequest<Response<CapaActionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentCAPAAction", Id, CorrelationId);
}

public sealed record CloseCAPAActionCommand(Guid Id, CloseCapaActionInput Input, string CorrelationId)
    : IRequest<Response<CapaActionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentCAPAAction", Id, CorrelationId);
}

public sealed record CancelCAPAActionCommand(Guid Id, CancelCapaActionInput Input, string CorrelationId)
    : IRequest<Response<CapaActionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Update, "DocumentCAPAAction", Id, CorrelationId);
}

// ── bridge ───────────────────────────────────────────────────────────────────

public sealed record BridgeQualityEventFromSourceCommand(BridgeFromSourceInput Input, string CorrelationId)
    : IRequest<Response<QualityEventModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Create, "DocumentQualityEvent", Input.SourceId, CorrelationId);
}

public sealed record BridgeQualityEventFromGDocPCorrectionCommand(Guid CorrectionId, string? SeverityOverride, string CorrelationId)
    : IRequest<Response<QualityEventModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Create, "DocumentQualityEvent", CorrectionId, CorrelationId);
}

public sealed record BridgeQualityEventFromObsoleteCopyFindingCommand(Guid FindingId, string? SeverityOverride, string CorrelationId)
    : IRequest<Response<QualityEventModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Create, "DocumentQualityEvent", FindingId, CorrelationId);
}

public sealed record BridgeQualityEventFromTemporaryIssueCommand(Guid IssueId, string? SeverityOverride, string CorrelationId)
    : IRequest<Response<QualityEventModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Create, "DocumentQualityEvent", IssueId, CorrelationId);
}

public sealed record BridgeQualityEventFromExternalImpactCommand(Guid AssessmentId, string? SeverityOverride, string CorrelationId)
    : IRequest<Response<QualityEventModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => QualityEventAudit.Meta(AuditOperation.Create, "DocumentQualityEvent", AssessmentId, CorrelationId);
}
