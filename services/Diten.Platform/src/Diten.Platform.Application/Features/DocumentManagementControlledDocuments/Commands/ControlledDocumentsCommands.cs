using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Commands;

// MOD-0029-FU01 — controlled-document / template / share commands (sealed records; handlers delegate to services).
// Mutations are auditable (central audit + Seq via AuditBehavior). Non-mutating previews (DryRunFolderShare) and the
// per-user favorite toggle are intentionally NOT audited to avoid governance-log noise.

internal static class ControlledDocumentsAudit
{
    public const string Module = "MOD-0029-FU01";
    public static Guid? Correlation(string? correlationId) => Guid.TryParse(correlationId, out var c) ? c : null;
}

public sealed record CreateControlledDocumentCommand(CreateControlledDocumentInput Input, string CorrelationId)
    : IRequest<Response<ControlledDocumentDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "ControlledDocument",
        SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId));
}

public sealed record EditControlledDocumentCommand(Guid DocumentId, EditControlledDocumentInput Input, string CorrelationId)
    : IRequest<Response<ControlledDocumentDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "ControlledDocument",
        EntityId: DocumentId, SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId));
}

public sealed record CreateControlledDocumentVersionCommand(Guid DocumentId, FileUploadInput File, string? ChangeSummary, bool AllowUnchanged, string CorrelationId)
    : IRequest<Response<DocumentVersionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "ControlledDocumentVersion",
        EntityId: DocumentId, SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId));
}

public sealed record ShareControlledDocumentCommand(Guid DocumentId, Guid TargetCompanyId, string? ShareMode, string CorrelationId)
    : IRequest<Response<ShareResultModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Assign, "ControlledDocument",
        EntityId: DocumentId, SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["targetCompanyId"] = TargetCompanyId, ["shareMode"] = ShareMode });
}

public sealed record DeleteControlledDocumentCommand(Guid DocumentId, string CorrelationId)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Delete, "ControlledDocument",
        EntityId: DocumentId, SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId));
}

public sealed record MoveControlledDocumentCommand(Guid DocumentId, Guid TargetCollectionInstanceId, string CorrelationId)
    : IRequest<Response<ControlledDocumentDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "ControlledDocument",
        EntityId: DocumentId, SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["targetCollectionInstanceId"] = TargetCollectionInstanceId });
}

// Per-user favorite toggle — UI preference, intentionally not audited.
public sealed record ToggleControlledDocumentFavoriteCommand(Guid DocumentId, string CorrelationId)
    : IRequest<Response<DocumentFavoriteResult>>;

public sealed record CopyControlledDocumentCommand(Guid DocumentId, Guid TargetCollectionInstanceId, string? TitleOverride, string CorrelationId)
    : IRequest<Response<ControlledDocumentDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "ControlledDocument",
        SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["copiedFromDocumentId"] = DocumentId, ["targetCollectionInstanceId"] = TargetCollectionInstanceId });
}

public sealed record CopyTemplateCommand(Guid TemplateId, Guid TargetCollectionInstanceId, string? TitleOverride, string CorrelationId)
    : IRequest<Response<TemplateDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "TemplateDocument",
        SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["copiedFromTemplateId"] = TemplateId, ["targetCollectionInstanceId"] = TargetCollectionInstanceId });
}

public sealed record CreateTemplateDocumentCommand(CreateTemplateInput Input, string CorrelationId)
    : IRequest<Response<TemplateDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "TemplateDocument",
        SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId));
}

public sealed record CreateTemplateVersionCommand(Guid TemplateId, FileUploadInput File, string? ChangeSummary, bool AllowUnchanged, string CorrelationId)
    : IRequest<Response<DocumentVersionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "TemplateDocumentVersion",
        EntityId: TemplateId, SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId));
}

public sealed record ShareTemplateCommand(Guid TemplateId, Guid TargetCompanyId, string? ShareMode, string CorrelationId)
    : IRequest<Response<ShareResultModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Assign, "TemplateDocument",
        EntityId: TemplateId, SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["targetCompanyId"] = TargetCompanyId, ["shareMode"] = ShareMode });
}

public sealed record UpsertFolderDocumentAccessCommand(UpsertFolderAccessInput Input, string CorrelationId)
    : IRequest<Response<FolderAccessPolicyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Assign, "FolderDocumentAccessPolicy",
        SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId));
}

// Non-mutating dry run — intentionally not audited.
public sealed record DryRunFolderShareCommand(FolderShareInput Input, string CorrelationId)
    : IRequest<Response<FolderShareResultModel>>;

public sealed record ExecuteFolderShareCommand(FolderShareInput Input, string CorrelationId)
    : IRequest<Response<FolderShareResultModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Execute, "FolderShare",
        SourceModule: ControlledDocumentsAudit.Module, CorrelationId: ControlledDocumentsAudit.Correlation(CorrelationId));
}
