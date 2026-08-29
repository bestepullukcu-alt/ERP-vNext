using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementExternalDocuments.Commands;

// MOD-0029-FU14 — external document register commands. Auditable via the central AuditBehavior. No hard delete:
// supersession, archival and link closure are all status changes.

internal static class ExternalDocumentAudit
{
    public const string Module = "MOD-0029-FU14";
    public static Guid? Correlation(string? c) => Guid.TryParse(c, out var g) ? g : null;
    public static AuditRequestMetadata Meta(AuditOperation op, string entityType, Guid entityId, string correlationId) =>
        new(AuditCategory.DocumentManagement, op, entityType, EntityId: entityId, SourceModule: Module, CorrelationId: Correlation(correlationId));
}

public sealed record CreateExternalDocumentRegisterEntryCommand(ExternalDocumentFieldsInput Input, string CorrelationId)
    : IRequest<Response<ExternalDocumentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ExternalDocumentAudit.Meta(AuditOperation.Create, "ExternalDocumentRegisterEntry", Guid.Empty, CorrelationId);
}

public sealed record UpdateExternalDocumentRegisterEntryCommand(Guid Id, ExternalDocumentFieldsInput Input, string CorrelationId)
    : IRequest<Response<ExternalDocumentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ExternalDocumentAudit.Meta(AuditOperation.Update, "ExternalDocumentRegisterEntry", Id, CorrelationId);
}

public sealed record MarkExternalDocumentSupersededCommand(Guid Id, MarkExternalDocumentSupersededInput Input, string CorrelationId)
    : IRequest<Response<ExternalDocumentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ExternalDocumentAudit.Meta(AuditOperation.Update, "ExternalDocumentRegisterEntry", Id, CorrelationId);
}

public sealed record ArchiveExternalDocumentCommand(Guid Id, ArchiveExternalDocumentInput Input, string CorrelationId)
    : IRequest<Response<ExternalDocumentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ExternalDocumentAudit.Meta(AuditOperation.Update, "ExternalDocumentRegisterEntry", Id, CorrelationId);
}

public sealed record RecordExternalDocumentMonitoringCheckCommand(Guid Id, RecordMonitoringCheckInput Input, string CorrelationId)
    : IRequest<Response<ExternalDocumentMonitoringCheckModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ExternalDocumentAudit.Meta(AuditOperation.Create, "ExternalDocumentMonitoringCheck", Id, CorrelationId);
}

public sealed record CreateExternalDocumentImpactAssessmentCommand(Guid Id, CreateExternalImpactAssessmentInput Input, string CorrelationId)
    : IRequest<Response<ExternalDocumentImpactAssessmentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ExternalDocumentAudit.Meta(AuditOperation.Create, "ExternalDocumentImpactAssessment", Id, CorrelationId);
}

public sealed record CompleteExternalDocumentImpactAssessmentCommand(Guid Id, Guid AssessmentId, CompleteExternalImpactAssessmentInput Input, string CorrelationId)
    : IRequest<Response<ExternalDocumentImpactAssessmentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ExternalDocumentAudit.Meta(AuditOperation.Update, "ExternalDocumentImpactAssessment", AssessmentId, CorrelationId);
}

public sealed record LinkExternalDocumentToInternalRegisterEntryCommand(Guid Id, LinkExternalDocumentToInternalInput Input, string CorrelationId)
    : IRequest<Response<ExternalDocumentInternalLinkModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ExternalDocumentAudit.Meta(AuditOperation.Assign, "ExternalDocumentInternalLink", Id, CorrelationId);
}

/// <summary>Closes a link (status change). There is deliberately no unlink/delete endpoint.</summary>
public sealed record CloseExternalDocumentInternalLinkCommand(Guid Id, Guid LinkId, string CorrelationId)
    : IRequest<Response<ExternalDocumentInternalLinkModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ExternalDocumentAudit.Meta(AuditOperation.Update, "ExternalDocumentInternalLink", LinkId, CorrelationId);
}
