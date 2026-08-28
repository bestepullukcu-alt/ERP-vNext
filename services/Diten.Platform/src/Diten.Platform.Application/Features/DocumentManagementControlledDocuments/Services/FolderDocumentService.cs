using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — folder-detail attachments (documents + templates under a CollectionInstance node) and the
/// Layer 2 folder-level access policy (<see cref="FolderDocumentAccessPolicy"/>) management. The policy is the
/// FU01-owned sidecar; it never mutates the read-only MOD-0028 CollectionInstance.
/// </summary>
public sealed class FolderDocumentService
{
    private readonly ICollectionInstanceReferenceReader _reader;
    private readonly IControlledDocumentRepository _documents;
    private readonly ITemplateDocumentRepository _templates;
    private readonly IDocumentFavoriteRepository _favorites;
    private readonly IFolderDocumentAccessPolicyRepository _folderPolicies;
    private readonly DocumentAccessEvaluator _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public FolderDocumentService(
        ICollectionInstanceReferenceReader reader,
        IControlledDocumentRepository documents,
        ITemplateDocumentRepository templates,
        IDocumentFavoriteRepository favorites,
        IFolderDocumentAccessPolicyRepository folderPolicies,
        DocumentAccessEvaluator access,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _reader = reader;
        _documents = documents;
        _templates = templates;
        _favorites = favorites;
        _folderPolicies = folderPolicies;
        _access = access;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<FolderDocumentsModel>> GetFolderDocumentsAsync(
        Guid collectionInstanceId,
        bool includeNonEffective,
        string correlationId,
        CancellationToken ct)
    {
        var folder = await _reader.ResolveByIdAsync(collectionInstanceId, ct);
        if (folder is null)
        {
            return Response<FolderDocumentsModel>.Fail("Not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage, correlationId);
        }

        // Folder access selects the folder; each item still computes effective View so item-level deny wins.
        if (!await _access.CanViewFolderAsync(collectionInstanceId, folder.CompanyId, ct))
        {
            return Response<FolderDocumentsModel>.Fail("Permission denied.", 403, ControlledDocumentReasonCodes.PermissionDenied, correlationId);
        }

        var favoriteIds = _currentUser.UserId == Guid.Empty
            ? (IReadOnlySet<Guid>)new HashSet<Guid>()
            : await _favorites.GetFavoriteDocumentIdsAsync(_currentUser.UserId, ct);
        var documents = new List<ControlledDocumentListItemModel>();
        foreach (var document in await _documents.GetByCollectionInstanceAsync(collectionInstanceId, ct))
        {
            var lifecycle = await _access.GetControlledDocumentLifecycleVisibilityAsync(document, ct);
            if ((!includeNonEffective || !lifecycle.CanViewNonEffective) && !lifecycle.IsOfficiallyEffective)
            {
                continue;
            }

            if (await _access.CanReadControlledDocumentAsync(document, ct))
            {
                documents.Add(ControlledDocumentMapping.ToListItem(document) with
                {
                    IsFavorite = favoriteIds.Contains(document.Id),
                    MasterRegisterLifecycleStatus = lifecycle.MasterRegisterLifecycleStatus,
                    IsOfficiallyEffective = lifecycle.IsOfficiallyEffective
                });
            }
        }

        var templates = new List<TemplateListItemModel>();
        foreach (var template in await _templates.GetByCollectionInstanceAsync(collectionInstanceId, ct))
        {
            if (await _access.CanViewTemplateDocumentAsync(template, null, ct))
            {
                templates.Add(ControlledDocumentMapping.ToListItem(template));
            }
        }

        documents = documents.OrderByDescending(d => d.CreatedAt).ToList();
        templates = templates.OrderByDescending(t => t.CreatedAt).ToList();

        return Response<FolderDocumentsModel>.Success(
            new FolderDocumentsModel(collectionInstanceId, folder.FullPath, documents, templates), correlationId: correlationId);
    }

    public Task<Response<FolderDocumentsModel>> GetFolderDocumentsAsync(
        Guid collectionInstanceId,
        string correlationId,
        CancellationToken ct) =>
        GetFolderDocumentsAsync(collectionInstanceId, false, correlationId, ct);

    public async Task<Response<IReadOnlyList<FolderAccessPolicyModel>>> GetFolderAccessAsync(Guid collectionInstanceId, string correlationId, CancellationToken ct)
    {
        var folder = await _reader.ResolveByIdAsync(collectionInstanceId, ct);
        if (folder is null)
        {
            return Response<IReadOnlyList<FolderAccessPolicyModel>>.Fail("Not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var policies = await _folderPolicies.GetByCollectionInstanceAsync(collectionInstanceId, ct);
        return Response<IReadOnlyList<FolderAccessPolicyModel>>.Success(policies.Select(ToModel).ToList(), correlationId: correlationId);
    }

    public async Task<Response<FolderAccessPolicyModel>> UpsertFolderAccessAsync(UpsertFolderAccessInput input, string correlationId, CancellationToken ct)
    {
        var folder = await _reader.ResolveByIdAsync(input.CollectionInstanceId, ct);
        if (folder is null || !await _reader.ValidateScopeAsync(input.CollectionInstanceId, input.CompanyId, ct))
        {
            return Response<FolderAccessPolicyModel>.Fail("Not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var targetType = ControlledDocumentWire.ParseTargetType(input.TargetType);
        if (targetType is null || string.IsNullOrWhiteSpace(input.TargetId))
        {
            return Response<FolderAccessPolicyModel>.Fail("Invalid access target.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var policy = new FolderDocumentAccessPolicy
        {
            TenantId = tenantId,
            CollectionInstanceId = input.CollectionInstanceId,
            CompanyId = folder.CompanyId,
            TargetType = targetType.Value,
            TargetId = input.TargetId.Trim(),
            FolderPermissions = new FolderPermissionSet
            {
                CanViewFolderDocuments = input.Permissions.CanViewFolderDocuments,
                CanUploadDocument = input.Permissions.CanUploadDocument,
                CanEditFolderDocuments = input.Permissions.CanEditFolderDocuments,
                CanUploadNewVersion = input.Permissions.CanUploadNewVersion,
                CanShareFolderDocuments = input.Permissions.CanShareFolderDocuments,
                CanManageFolderDocumentAccess = input.Permissions.CanManageFolderDocumentAccess
            }
        };

        var saved = await _folderPolicies.UpsertAsync(policy, ct);
        return Response<FolderAccessPolicyModel>.Success(ToModel(saved), correlationId: correlationId);
    }

    private static FolderAccessPolicyModel ToModel(FolderDocumentAccessPolicy p) => new(
        p.CollectionInstanceId,
        p.CompanyId,
        p.TargetType.ToWire(),
        p.TargetId,
        new FolderPermissionsInput(
            p.FolderPermissions.CanViewFolderDocuments,
            p.FolderPermissions.CanUploadDocument,
            p.FolderPermissions.CanEditFolderDocuments,
            p.FolderPermissions.CanUploadNewVersion,
            p.FolderPermissions.CanShareFolderDocuments,
            p.FolderPermissions.CanManageFolderDocumentAccess));
}

public sealed record UpsertFolderAccessInput(
    Guid CollectionInstanceId,
    Guid CompanyId,
    string TargetType,
    string TargetId,
    FolderPermissionsInput Permissions);
