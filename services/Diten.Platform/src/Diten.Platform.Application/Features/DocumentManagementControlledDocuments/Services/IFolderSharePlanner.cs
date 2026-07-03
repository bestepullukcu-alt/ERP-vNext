using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — builds a folder/branch share plan from a CollectionInstance branch (read-only via
/// <see cref="ICollectionInstanceReferenceReader"/>). Discovers associated templates under the included nodes;
/// never exposes an unselected branch or an unrelated item.
/// </summary>
public interface IFolderSharePlanner
{
    Task<Response<FolderSharePlan>> PlanAsync(
        Guid sourceBranchCollectionInstanceId,
        Guid targetCompanyId,
        bool includeTemplates,
        DocumentShareMode shareMode,
        string correlationId,
        CancellationToken ct);
}

public sealed record FolderSharePlan(
    Guid OperationId,
    Guid TenantId,
    Guid SourceCompanyId,
    Guid TargetCompanyId,
    Guid SourceBranchCollectionInstanceId,
    bool IncludeTemplates,
    DocumentShareMode ShareMode,
    bool Blocked,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<FolderShareNode> Folders,
    IReadOnlyList<TemplateDocument> IncludedTemplates,
    IReadOnlyList<FolderShareSkippedTemplate> SkippedTemplates);

public sealed record FolderShareNode(Guid CollectionInstanceId, string CanonicalId, string FullPath);

public sealed record FolderShareSkippedTemplate(Guid TemplateId, string TemplateKey, string ReasonCode, string Message);
