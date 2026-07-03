using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — Controlled Documents Explorer read model: lists a company's **active instantiated
/// Documentation Structures** (never raw published baselines) and runs **server-side, permission-filtered**
/// mixed search (folder / document / template) within a selected structure. Consumes the CollectionInstance
/// tree read-only via <see cref="ICollectionInstanceReferenceReader"/>; never mutates the folder tree.
/// </summary>
public sealed class ControlledDocumentExplorerService
{
    private readonly ICollectionInstanceReferenceReader _reader;
    private readonly IControlledDocumentRepository _documents;
    private readonly ITemplateDocumentRepository _templates;
    private readonly DocumentAccessEvaluator _access;

    public ControlledDocumentExplorerService(
        ICollectionInstanceReferenceReader reader,
        IControlledDocumentRepository documents,
        ITemplateDocumentRepository templates,
        DocumentAccessEvaluator access)
    {
        _reader = reader;
        _documents = documents;
        _templates = templates;
        _access = access;
    }

    public async Task<Response<IReadOnlyList<DocumentationStructureModel>>> GetActiveStructuresAsync(Guid companyId, string correlationId, CancellationToken ct)
    {
        if (companyId == Guid.Empty)
        {
            return Response<IReadOnlyList<DocumentationStructureModel>>.Fail("companyId is required.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        var instances = await _reader.GetCompanyInstancesAsync(companyId, ct);
        var active = instances.Where(x => x.IsUsable).ToList();

        var structures = new List<DocumentationStructureModel>();
        // One active Documentation Structure per instantiated baseline release for the company.
        foreach (var group in active.GroupBy(x => x.BaselineReleaseId))
        {
            // Pick the structure's representative root by the baseline's curated order (shallowest depth, then
            // DisplayOrder, then path) so the dropdown label matches the Baseline Definition Tree's first top-level
            // domain (e.g. "META & STANDARDS") instead of an arbitrary alphabetically-first one (e.g. "AUDIT...").
            var root = group
                .OrderBy(x => Depth(x.FullPath))
                .ThenBy(x => x.DisplayOrder)
                .ThenBy(x => x.FullPath, StringComparer.Ordinal)
                .First();
            // Only surface a structure the principal may view (owner-company OR a folder-view grant on its root).
            if (!await _access.CanViewFolderAsync(root.CollectionInstanceId, companyId, ct))
            {
                continue;
            }

            structures.Add(new DocumentationStructureModel(
                root.CollectionInstanceId,
                root.CollectionInstanceId,
                root.Name,
                companyId,
                root.BaselineReleaseId == Guid.Empty ? null : root.BaselineReleaseId,
                null,
                root.InstanceStatus,
                group.Count(),
                null));
        }

        return Response<IReadOnlyList<DocumentationStructureModel>>.Success(
            structures.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(), correlationId: correlationId);
    }

    public async Task<Response<ExplorerSearchResultModelList>> SearchAsync(ExplorerSearchInput input, string correlationId, CancellationToken ct)
    {
        if (input.CompanyId == Guid.Empty || input.ActiveStructureId == Guid.Empty)
        {
            return Response<ExplorerSearchResultModelList>.Fail("companyId and activeStructureId are required.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        var root = await _reader.ResolveByIdAsync(input.ActiveStructureId, ct);
        if (root is null || root.CompanyId != input.CompanyId)
        {
            return NotFound(correlationId);
        }

        // Resolve the folder set per scope (read-only; never mutated).
        IReadOnlyList<CollectionInstanceReferenceDto> folderSet = input.Scope switch
        {
            ExplorerSearchScope.CurrentFolder when input.CollectionInstanceId is { } id => await SingleFolderAsync(id, ct),
            ExplorerSearchScope.Subtree when input.CollectionInstanceId is { } id => await _reader.GetBranchAsync(id, ct),
            _ => await _reader.GetBranchAsync(input.ActiveStructureId, ct) // structure (default)
        };

        // Keep only folders the principal may view, and within the selected structure/company.
        var viewableFolders = new List<CollectionInstanceReferenceDto>();
        foreach (var folder in folderSet.Where(f => f.CompanyId == input.CompanyId))
        {
            if (await _access.CanViewFolderAsync(folder.CollectionInstanceId, input.CompanyId, ct))
            {
                viewableFolders.Add(folder);
            }
        }

        var folderIds = viewableFolders.Select(f => f.CollectionInstanceId).ToHashSet();
        var query = input.Query?.Trim();
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var documentType = ControlledDocumentWire.ParseDocumentType(input.DocumentType);
        var results = new List<ExplorerSearchResultModel>();

        // Folder results (only when actively searching by name).
        if (hasQuery)
        {
            foreach (var folder in viewableFolders.Where(f => Contains(f.Name, query!)))
            {
                results.Add(new ExplorerSearchResultModel(
                    "FOLDER", folder.CollectionInstanceId, folder.Name, folder.FullPath, folder.CollectionInstanceId,
                    null, null, null, null, folder.InstanceStatus, null,
                    new SearchResultPermissions(true, false, false, false, false,
                        await _access.HasFolderActionAsync(folder.CollectionInstanceId, DocumentAccessAction.ManageAccess, ct))));
            }
        }

        foreach (var folderId in folderIds)
        {
            foreach (var d in await _documents.GetByCollectionInstanceAsync(folderId, ct))
            {
                if (!await _access.CanReachDocumentAsync(d, ct)) continue;
                if (hasQuery && !Contains(d.Title, query!)) continue;
                if ((int)documentType >= 0 && d.DocumentType != documentType) continue;
                if (!string.IsNullOrWhiteSpace(input.Status) && !string.Equals(d.Status.ToWire(), input.Status, StringComparison.OrdinalIgnoreCase)) continue;

                results.Add(new ExplorerSearchResultModel(
                    "DOCUMENT", d.Id, d.Title, d.CollectionPath, d.CollectionInstanceId, d.Id, null,
                    d.DocumentType.ToWire(), d.CurrentVersionNumber, d.Status.ToWire(), d.UpdatedAt ?? d.CreatedAt,
                    await DocumentPermissionsAsync(d, ct)));
            }

            if (input.IncludeTemplates)
            {
                foreach (var t in await _templates.GetByCollectionInstanceAsync(folderId, ct))
                {
                    if (!await _access.CanReachTemplateAsync(t, ct)) continue;
                    if (hasQuery && !Contains(t.Title, query!)) continue;
                    if (!string.IsNullOrWhiteSpace(input.Status) && !string.Equals(t.Status.ToWire(), input.Status, StringComparison.OrdinalIgnoreCase)) continue;

                    results.Add(new ExplorerSearchResultModel(
                        "TEMPLATE", t.Id, t.Title, t.CollectionPath ?? string.Empty, t.CollectionInstanceId ?? folderId, null, t.Id,
                        "TEMPLATE", t.CurrentVersionNumber, t.Status.ToWire(), t.UpdatedAt ?? t.CreatedAt,
                        await TemplatePermissionsAsync(t, ct)));
                }
            }
        }

        var ordered = results
            .OrderBy(r => r.ResultType == "FOLDER" ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Response<ExplorerSearchResultModelList>.Success(
            new ExplorerSearchResultModelList(input.CompanyId, input.ActiveStructureId, input.Scope.ToString().ToUpperInvariant(), query, ordered),
            correlationId: correlationId);
    }

    private async Task<IReadOnlyList<CollectionInstanceReferenceDto>> SingleFolderAsync(Guid id, CancellationToken ct)
    {
        var folder = await _reader.ResolveByIdAsync(id, ct);
        return folder is null ? [] : [folder];
    }

    private async Task<SearchResultPermissions> DocumentPermissionsAsync(ControlledDocument document, CancellationToken ct) => new(
        await _access.HasControlledDocumentMatrixActionAsync(document, DocumentAccessMatrixAction.View, DocumentAccessAction.View, ct),
        await _access.HasControlledDocumentMatrixActionAsync(document, DocumentAccessMatrixAction.Download, DocumentAccessAction.Download, ct),
        await _access.HasControlledDocumentMatrixActionAsync(document, DocumentAccessMatrixAction.EditMetadata, DocumentAccessAction.Edit, ct),
        await _access.HasControlledDocumentMatrixActionAsync(document, DocumentAccessMatrixAction.UploadVersion, DocumentAccessAction.Version, ct),
        await _access.HasControlledDocumentMatrixActionAsync(document, DocumentAccessMatrixAction.Share, DocumentAccessAction.Share, ct),
        await _access.HasControlledDocumentMatrixActionAsync(document, DocumentAccessMatrixAction.ManageAccess, DocumentAccessAction.ManageAccess, ct));

    private async Task<SearchResultPermissions> TemplatePermissionsAsync(TemplateDocument template, CancellationToken ct) => new(
        await _access.HasTemplateDocumentMatrixActionAsync(template, DocumentAccessMatrixAction.View, DocumentAccessAction.View, ct),
        await _access.HasTemplateDocumentMatrixActionAsync(template, DocumentAccessMatrixAction.Download, DocumentAccessAction.Download, ct),
        await _access.HasTemplateDocumentMatrixActionAsync(template, DocumentAccessMatrixAction.EditMetadata, DocumentAccessAction.Edit, ct),
        await _access.HasTemplateDocumentMatrixActionAsync(template, DocumentAccessMatrixAction.UploadVersion, DocumentAccessAction.Version, ct),
        await _access.HasTemplateDocumentMatrixActionAsync(template, DocumentAccessMatrixAction.Share, DocumentAccessAction.Share, ct),
        await _access.HasTemplateDocumentMatrixActionAsync(template, DocumentAccessMatrixAction.ManageAccess, DocumentAccessAction.ManageAccess, ct));

    private static bool Contains(string? value, string query) =>
        !string.IsNullOrEmpty(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static int Depth(string fullPath) =>
        string.IsNullOrEmpty(fullPath) ? 0 : fullPath.Count(c => c == '/');

    private static Response<ExplorerSearchResultModelList> NotFound(string correlationId) =>
        Response<ExplorerSearchResultModelList>.Fail("Not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage, correlationId);
}

public enum ExplorerSearchScope
{
    CurrentFolder = 0,
    Subtree = 1,
    Structure = 2
}

public sealed record ExplorerSearchInput(
    Guid CompanyId,
    Guid ActiveStructureId,
    Guid? CollectionInstanceId,
    ExplorerSearchScope Scope,
    string? Query,
    string? DocumentType,
    bool IncludeTemplates,
    string? Status);
