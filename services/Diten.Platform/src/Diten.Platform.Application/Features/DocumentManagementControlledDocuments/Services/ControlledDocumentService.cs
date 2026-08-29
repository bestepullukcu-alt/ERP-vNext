using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — controlled document attach / version / read / download orchestration. Consumes the
/// MOD-0028-FU05 CollectionInstance ONLY through <see cref="ICollectionInstanceReferenceReader"/> (read-only).
/// Storage-first commit with best-effort compensation (no metadata orphan). Layer 1 is enforced by the
/// controller <c>[HasPermission]</c>; this service additionally enforces Layer 2 (folder/document AccessPolicy).
/// </summary>
public sealed class ControlledDocumentService
{
    private readonly ICollectionInstanceReferenceReader _reader;
    private readonly IControlledDocumentRepository _documents;
    private readonly IControlledDocumentVersionRepository _versions;
    private readonly IDocumentShareRecordRepository _shares;
    private readonly IDocumentFavoriteRepository _favorites;
    private readonly DocumentVersioningService _versioning;
    private readonly DocumentAccessEvaluator _access;
    private readonly DocumentKeyFactory _keyFactory;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly ControlledDocumentsFeatureFlagOptions _flags;

    public ControlledDocumentService(
        ICollectionInstanceReferenceReader reader,
        IControlledDocumentRepository documents,
        IControlledDocumentVersionRepository versions,
        IDocumentShareRecordRepository shares,
        IDocumentFavoriteRepository favorites,
        DocumentVersioningService versioning,
        DocumentAccessEvaluator access,
        DocumentKeyFactory keyFactory,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        IOptions<ControlledDocumentsFeatureFlagOptions> flags)
    {
        _reader = reader;
        _documents = documents;
        _versions = versions;
        _shares = shares;
        _favorites = favorites;
        _versioning = versioning;
        _access = access;
        _keyFactory = keyFactory;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _flags = flags.Value;
    }

    public async Task<Response<ControlledDocumentDetailModel>> CreateAsync(CreateControlledDocumentInput input, string correlationId, CancellationToken ct)
    {
        if (!_flags.ControlledDocumentsEnabled)
        {
            return Fail<ControlledDocumentDetailModel>("Controlled documents are not enabled.", 403, ControlledDocumentReasonCodes.FeatureDisabled, correlationId);
        }

        var documentType = ControlledDocumentWire.ParseDocumentType(input.DocumentType);
        if ((int)documentType < 0)
        {
            return Fail<ControlledDocumentDetailModel>("Unsupported document type.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return Fail<ControlledDocumentDetailModel>("Title is required.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        if (input.ExpiryDate is { } expiry && input.EffectiveDate is { } effective && expiry < effective)
        {
            return Fail<ControlledDocumentDetailModel>("Expiry date cannot precede the effective date.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        // 1) Resolve folder (tenant-scoped) → 404 non-leakage.
        var folder = await _reader.ResolveByIdAsync(input.CollectionInstanceId, ct);
        if (folder is null)
        {
            return NotFound<ControlledDocumentDetailModel>(correlationId);
        }

        // 2) Validate company/legal-entity scope.
        if (!await _reader.ValidateScopeAsync(input.CollectionInstanceId, input.CompanyId, ct))
        {
            return NotFound<ControlledDocumentDetailModel>(correlationId);
        }

        // Archived/inactive folder cannot receive attachments.
        if (!folder.IsUsable)
        {
            return Fail<ControlledDocumentDetailModel>("The target folder is not active.", 409, ControlledDocumentReasonCodes.Conflict, correlationId);
        }

        // 3) Folder-level upload permission (Layer 2).
        if (!await _access.HasFolderCreateDocumentAsync(input.CollectionInstanceId, ct))
        {
            return PermDenied<ControlledDocumentDetailModel>(correlationId);
        }

        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        // 5/6) Validate file + write storage-first.
        var stored = await _versioning.StoreAsync(
            ContentStorageScope.Documents, folder.CompanyId, documentId, versionId, input.File, _currentUser.ActorName, correlationId, ct);
        if (!stored.IsSuccessful)
        {
            return Fail<ControlledDocumentDetailModel>(stored.Errors, stored.StatusCode, stored.ReasonCode, correlationId);
        }

        var document = new ControlledDocument
        {
            Id = documentId,
            TenantId = tenantId,
            DocumentKey = _keyFactory.ForDocument(tenantId, folder.CompanyId, folder.CollectionInstanceId, input.Title),
            CompanyId = folder.CompanyId,
            OwnerCompanyId = folder.CompanyId,
            CollectionInstanceId = folder.CollectionInstanceId,
            CollectionPath = folder.FullPath,
            CanonicalId = folder.CanonicalId,
            Title = input.Title.Trim(),
            DocumentType = documentType,
            Description = input.Description?.Trim(),
            Tags = NormalizeTags(input.Tags),
            Controlled = input.Controlled,
            EffectiveDate = input.EffectiveDate,
            ReviewDate = input.ReviewDate,
            ExpiryDate = input.ExpiryDate,
            CurrentVersionId = versionId,
            CurrentVersionNumber = 1,
            Status = ControlledItemStatus.Active,
            AccessPolicy = BuildAccessPolicy(input.AccessPolicy),
            CreatedBy = _currentUser.ActorName
        };

        var version = new ControlledDocumentVersion
        {
            Id = versionId,
            TenantId = tenantId,
            DocumentId = documentId,
            VersionNumber = 1,
            FileRef = DocumentVersioningService.ToContentRef(stored.Data!, versionId, _currentUser.ActorName),
            Checksum = stored.Data!.Checksum,
            UploadedBy = _currentUser.ActorName,
            UploadedAt = DateTimeOffset.UtcNow,
            ChangeSummary = input.ChangeSummary?.Trim(),
            VersionStatus = DocumentVersionStatus.Active,
            CreatedBy = _currentUser.ActorName
        };

        try
        {
            // 7) Commit metadata only after storage succeeded.
            await _documents.CreateAsync(document, ct);
            await _versions.CreateAsync(version, ct);
        }
        catch
        {
            // 9) Metadata commit failed after storage success → best-effort delete (no orphan).
            await _versioning.TryDeleteAsync(stored.Data!, CancellationToken.None);
            return Fail<ControlledDocumentDetailModel>("Could not persist the document.", 409, ControlledDocumentReasonCodes.Conflict, correlationId);
        }

        return Response<ControlledDocumentDetailModel>.Success(ControlledDocumentMapping.ToDetail(document), 201, correlationId);
    }

    public Task<Response<IReadOnlyList<ControlledDocumentListItemModel>>> ListAsync(
        Guid? collectionInstanceId,
        string correlationId,
        CancellationToken ct) =>
        ListAsync(collectionInstanceId, false, correlationId, ct);

    public async Task<Response<IReadOnlyList<ControlledDocumentListItemModel>>> ListAsync(
        Guid? collectionInstanceId,
        bool includeNonEffective,
        string correlationId,
        CancellationToken ct)
    {
        var visible = await GetVisibleDocumentsAsync(collectionInstanceId, ct);
        var favorites = _currentUser.UserId == Guid.Empty
            ? (IReadOnlySet<Guid>)new HashSet<Guid>()
            : await _favorites.GetFavoriteDocumentIdsAsync(_currentUser.UserId, ct);
        var items = new List<ControlledDocumentListItemModel>();
        foreach (var document in visible)
        {
            var lifecycle = await _access.GetControlledDocumentLifecycleVisibilityAsync(document, ct);
            if ((!includeNonEffective || !lifecycle.CanViewNonEffective) && !lifecycle.IsOfficiallyEffective)
            {
                continue;
            }

            items.Add(ControlledDocumentMapping.ToListItem(document) with
            {
                IsFavorite = favorites.Contains(document.Id),
                MasterRegisterLifecycleStatus = lifecycle.MasterRegisterLifecycleStatus,
                IsOfficiallyEffective = lifecycle.IsOfficiallyEffective
            });
        }
        return Response<IReadOnlyList<ControlledDocumentListItemModel>>.Success(items, correlationId: correlationId);
    }

    public async Task<Response<NoContent>> DeleteAsync(Guid documentId, string correlationId, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReachDocumentAsync(document, ct))
        {
            return NotFound<NoContent>(correlationId);
        }

        // Delete = soft delete (no hard delete per pack §4/§8). Requires the Layer 2 edit grant.
        if (!await _access.HasControlledDocumentMatrixActionAsync(
                document,
                DocumentAccessMatrixAction.Archive,
                DocumentAccessAction.Edit,
                ct))
        {
            return PermDenied<NoContent>(correlationId);
        }

        await _documents.SoftDeleteAsync(documentId, ct);
        return Response<NoContent>.Success(204, correlationId);
    }

    public async Task<Response<ControlledDocumentDetailModel>> MoveAsync(Guid documentId, Guid targetCollectionInstanceId, string correlationId, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReachDocumentAsync(document, ct))
        {
            return NotFound<ControlledDocumentDetailModel>(correlationId);
        }

        if (!await _access.HasControlledDocumentMatrixActionAsync(
                document,
                DocumentAccessMatrixAction.EditMetadata,
                DocumentAccessAction.Edit,
                ct)
            && !_access.Principal.BelongsToCompany(document.OwnerCompanyId))
        {
            return PermDenied<ControlledDocumentDetailModel>(correlationId);
        }

        var target = await _reader.ResolveByIdAsync(targetCollectionInstanceId, ct);
        if (target is null)
        {
            return NotFound<ControlledDocumentDetailModel>(correlationId);
        }

        // Move stays within the owning company; a cross-company transfer is a share, not a move.
        if (target.CompanyId != document.OwnerCompanyId)
        {
            return NotFound<ControlledDocumentDetailModel>(correlationId);
        }

        if (!target.IsUsable)
        {
            return Fail<ControlledDocumentDetailModel>("The target folder is not active.", 409, ControlledDocumentReasonCodes.Conflict, correlationId);
        }

        // Folder-level upload permission on the target folder (same gate as attaching a document there).
        if (!await _access.HasFolderCreateDocumentAsync(targetCollectionInstanceId, ct) && !_access.Principal.BelongsToCompany(target.CompanyId))
        {
            return PermDenied<ControlledDocumentDetailModel>(correlationId);
        }

        if (targetCollectionInstanceId == document.CollectionInstanceId)
        {
            return Fail<ControlledDocumentDetailModel>("The document is already in this folder.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        document.CollectionInstanceId = target.CollectionInstanceId;
        document.CollectionPath = target.FullPath;
        document.CanonicalId = target.CanonicalId;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        document.UpdatedBy = _currentUser.ActorName;
        await _documents.UpdateAsync(document, ct);

        return Response<ControlledDocumentDetailModel>.Success(ControlledDocumentMapping.ToDetail(document), correlationId: correlationId);
    }

    public async Task<Response<ControlledDocumentDetailModel>> CopyAsync(Guid documentId, Guid targetCollectionInstanceId, string? titleOverride, string correlationId, CancellationToken ct)
    {
        var source = await _documents.GetByIdAsync(documentId, ct);
        if (source is null || !await _access.CanReachDocumentAsync(source, ct))
        {
            return NotFound<ControlledDocumentDetailModel>(correlationId);
        }

        // Source view/download permission required to copy out of the source.
        if (!await _access.HasControlledDocumentMatrixActionAsync(
                source,
                DocumentAccessMatrixAction.View,
                DocumentAccessAction.View,
                ct)
            && !_access.Principal.BelongsToCompany(source.OwnerCompanyId))
        {
            return PermDenied<ControlledDocumentDetailModel>(correlationId);
        }

        var target = await _reader.ResolveByIdAsync(targetCollectionInstanceId, ct);
        if (target is null)
        {
            return NotFound<ControlledDocumentDetailModel>(correlationId);
        }

        // Same company only (cross-company copy is a share flow); target folder must be active.
        if (target.CompanyId != source.OwnerCompanyId)
        {
            return NotFound<ControlledDocumentDetailModel>(correlationId);
        }

        if (!target.IsUsable)
        {
            return Fail<ControlledDocumentDetailModel>("The target folder is not active.", 409, ControlledDocumentReasonCodes.Conflict, correlationId);
        }

        // Target upload/create permission required.
        if (!await _access.HasFolderCreateDocumentAsync(targetCollectionInstanceId, ct) && !_access.Principal.BelongsToCompany(target.CompanyId))
        {
            return PermDenied<ControlledDocumentDetailModel>(correlationId);
        }

        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var newId = Guid.NewGuid();
        var title = string.IsNullOrWhiteSpace(titleOverride) ? source.Title : titleOverride.Trim();
        var copy = new ControlledDocument
        {
            Id = newId,
            TenantId = tenantId,
            DocumentKey = _keyFactory.ForDocument(tenantId, target.CompanyId, target.CollectionInstanceId, $"{title}|copy|{newId:N}"),
            CompanyId = target.CompanyId,
            OwnerCompanyId = target.CompanyId,
            CollectionInstanceId = target.CollectionInstanceId,
            CollectionPath = target.FullPath,
            CanonicalId = target.CanonicalId,
            Title = title,
            DocumentType = source.DocumentType,
            Description = source.Description,
            Tags = [.. source.Tags],
            Controlled = source.Controlled,
            EffectiveDate = source.EffectiveDate,
            ReviewDate = source.ReviewDate,
            ExpiryDate = source.ExpiryDate,
            CurrentVersionNumber = 1,
            Status = ControlledItemStatus.Active,
            AccessPolicy = new DocumentAccessPolicy { Source = AccessPolicySource.Inherited },
            CopiedFromDocumentId = source.Id,
            CreatedBy = _currentUser.ActorName
        };

        // Default copy = current active version copied as a new independent initial version (same immutable
        // content object; lineage diverges on the target's subsequent uploads).
        var sourceVersion = source.CurrentVersionId is { } cv ? await _versions.GetByIdAsync(cv, ct) : null;
        if (sourceVersion is not null)
        {
            var versionId = Guid.NewGuid();
            copy.CurrentVersionId = versionId;
            await _documents.CreateAsync(copy, ct);
            await _versions.CreateAsync(CloneVersion(sourceVersion, newId, versionId, tenantId), ct);
        }
        else
        {
            await _documents.CreateAsync(copy, ct);
        }

        return Response<ControlledDocumentDetailModel>.Success(ControlledDocumentMapping.ToDetail(copy), 201, correlationId);
    }

    private ControlledDocumentVersion CloneVersion(ControlledDocumentVersion source, Guid documentId, Guid versionId, Guid tenantId) => new()
    {
        Id = versionId,
        TenantId = tenantId,
        DocumentId = documentId,
        VersionNumber = 1,
        FileRef = new ContentRef
        {
            ContentId = source.FileRef.ContentId,
            StorageProvider = source.FileRef.StorageProvider,
            ObjectKey = source.FileRef.ObjectKey,
            FileName = source.FileRef.FileName,
            MediaType = source.FileRef.MediaType,
            ByteSize = source.FileRef.ByteSize,
            Checksum = source.FileRef.Checksum,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUser.ActorName,
            VersionId = versionId
        },
        Checksum = source.Checksum,
        UploadedBy = _currentUser.ActorName,
        UploadedAt = DateTimeOffset.UtcNow,
        ChangeSummary = source.ChangeSummary,
        VersionStatus = DocumentVersionStatus.Active,
        CreatedBy = _currentUser.ActorName
    };

    public async Task<Response<DocumentFavoriteResult>> ToggleFavoriteAsync(Guid documentId, string correlationId, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReachDocumentAsync(document, ct))
        {
            return NotFound<DocumentFavoriteResult>(correlationId);
        }

        if (_currentUser.UserId == Guid.Empty)
        {
            return PermDenied<DocumentFavoriteResult>(correlationId);
        }

        var isFavorite = await _favorites.IsFavoriteAsync(_currentUser.UserId, documentId, ct);
        await _favorites.ToggleAsync(_currentUser.UserId, documentId, !isFavorite, ct);
        return Response<DocumentFavoriteResult>.Success(new DocumentFavoriteResult(documentId, !isFavorite), correlationId: correlationId);
    }

    public async Task<Response<ControlledDocumentDetailModel>> GetDetailAsync(Guid documentId, string correlationId, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReadControlledDocumentAsync(document, ct))
        {
            return NotFound<ControlledDocumentDetailModel>(correlationId);
        }

        if (!await CanViewAsync(document, ct))
        {
            return PermDenied<ControlledDocumentDetailModel>(correlationId);
        }

        var lifecycle = await _access.GetControlledDocumentLifecycleVisibilityAsync(document, ct);
        return Response<ControlledDocumentDetailModel>.Success(
            ControlledDocumentMapping.ToDetail(document) with
            {
                MasterRegisterLifecycleStatus = lifecycle.MasterRegisterLifecycleStatus,
                IsOfficiallyEffective = lifecycle.IsOfficiallyEffective,
                CanViewNonEffective = lifecycle.CanViewNonEffective
            },
            correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<DocumentVersionModel>>> GetVersionsAsync(Guid documentId, string correlationId, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReadControlledDocumentAsync(document, ct))
        {
            return NotFound<IReadOnlyList<DocumentVersionModel>>(correlationId);
        }

        if (!await CanViewAsync(document, ct))
        {
            return PermDenied<IReadOnlyList<DocumentVersionModel>>(correlationId);
        }

        var versions = await _versions.GetByDocumentAsync(documentId, ct);
        return Response<IReadOnlyList<DocumentVersionModel>>.Success(
            versions.OrderByDescending(v => v.VersionNumber).Select(ControlledDocumentMapping.ToVersionModel).ToList(),
            correlationId: correlationId);
    }

    public async Task<Response<DocumentVersionModel>> GetVersionAsync(Guid documentId, Guid versionId, string correlationId, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReadControlledDocumentAsync(document, ct))
        {
            return NotFound<DocumentVersionModel>(correlationId);
        }

        if (!await CanViewAsync(document, ct))
        {
            return PermDenied<DocumentVersionModel>(correlationId);
        }

        var version = await _versions.GetByIdAsync(versionId, ct);
        if (version is null || version.DocumentId != documentId)
        {
            return NotFound<DocumentVersionModel>(correlationId);
        }

        return Response<DocumentVersionModel>.Success(ControlledDocumentMapping.ToVersionModel(version), correlationId: correlationId);
    }

    public async Task<Response<DocumentVersionModel>> CreateVersionAsync(Guid documentId, FileUploadInput file, string? changeSummary, bool allowUnchanged, string correlationId, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReachDocumentAsync(document, ct))
        {
            return NotFound<DocumentVersionModel>(correlationId);
        }

        // Layer 2 version-create (document-level or inherited folder-level canUploadNewVersion).
        if (!await _access.HasControlledDocumentMatrixActionAsync(
                document,
                DocumentAccessMatrixAction.UploadVersion,
                DocumentAccessAction.Version,
                ct))
        {
            return PermDenied<DocumentVersionModel>(correlationId);
        }

        // Content-change guard: a "new version" whose SHA-256 is byte-identical to the current ACTIVE version is
        // not a real change. Reject it before any storage write (no orphan) unless the uploader explicitly forces
        // it. This is the deterministic answer to "did the document actually change?".
        if (!allowUnchanged && document.CurrentVersionId is { } activeId)
        {
            var uploadChecksum = DocumentVersioningService.ComputeChecksum(file?.ContentBase64);
            var activeVersion = await _versions.GetByIdAsync(activeId, ct);
            if (uploadChecksum is not null && activeVersion is not null
                && string.Equals(uploadChecksum, activeVersion.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                return Fail<DocumentVersionModel>(
                    "Uploaded content is identical to the current active version; no change detected.",
                    409, ControlledDocumentReasonCodes.NoContentChange, correlationId);
            }
        }

        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var nextNumber = await _versions.GetMaxVersionNumberAsync(documentId, ct) + 1;
        var versionId = Guid.NewGuid();

        var stored = await _versioning.StoreAsync(
            ContentStorageScope.Documents, document.CompanyId, documentId, versionId, file, _currentUser.ActorName, correlationId, ct);
        if (!stored.IsSuccessful)
        {
            return Fail<DocumentVersionModel>(stored.Errors, stored.StatusCode, stored.ReasonCode, correlationId);
        }

        var version = new ControlledDocumentVersion
        {
            Id = versionId,
            TenantId = tenantId,
            DocumentId = documentId,
            VersionNumber = nextNumber,
            FileRef = DocumentVersioningService.ToContentRef(stored.Data!, versionId, _currentUser.ActorName),
            Checksum = stored.Data!.Checksum,
            UploadedBy = _currentUser.ActorName,
            UploadedAt = DateTimeOffset.UtcNow,
            ChangeSummary = changeSummary?.Trim(),
            VersionStatus = DocumentVersionStatus.Active,
            CreatedBy = _currentUser.ActorName
        };

        try
        {
            await _versions.CreateAsync(version, ct);
            await _versions.SupersedeActiveVersionsAsync(documentId, versionId, ct);
            document.CurrentVersionId = versionId;
            document.CurrentVersionNumber = nextNumber;
            document.UpdatedAt = DateTimeOffset.UtcNow;
            document.UpdatedBy = _currentUser.ActorName;
            await _documents.UpdateAsync(document, ct);
        }
        catch
        {
            await _versioning.TryDeleteAsync(stored.Data!, CancellationToken.None);
            return Fail<DocumentVersionModel>("Duplicate or conflicting version.", 409, ControlledDocumentReasonCodes.Conflict, correlationId);
        }

        return Response<DocumentVersionModel>.Success(ControlledDocumentMapping.ToVersionModel(version), 201, correlationId);
    }

    public async Task<Response<DocumentDownloadResult>> DownloadAsync(Guid documentId, Guid versionId, string correlationId, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReadControlledDocumentAsync(document, ct))
        {
            return NotFound<DocumentDownloadResult>(correlationId);
        }

        // Backend-gated download: tenant → company → document/folder access → version → download permission.
        if (!await _access.HasControlledDocumentMatrixActionAsync(
                document,
                DocumentAccessMatrixAction.Download,
                DocumentAccessAction.Download,
                ct))
        {
            return PermDenied<DocumentDownloadResult>(correlationId);
        }

        var version = await _versions.GetByIdAsync(versionId, ct);
        if (version is null || version.DocumentId != documentId)
        {
            return NotFound<DocumentDownloadResult>(correlationId);
        }

        return Response<DocumentDownloadResult>.Success(
            new DocumentDownloadResult(version.FileRef.StorageProvider, version.FileRef.ObjectKey, version.FileRef.FileName, version.FileRef.MediaType),
            correlationId: correlationId);
    }

    public async Task<Response<ControlledDocumentDetailModel>> EditMetadataAsync(Guid documentId, EditControlledDocumentInput input, string correlationId, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReachDocumentAsync(document, ct))
        {
            return NotFound<ControlledDocumentDetailModel>(correlationId);
        }

        if (!await _access.HasControlledDocumentMatrixActionAsync(
                document,
                DocumentAccessMatrixAction.EditMetadata,
                DocumentAccessAction.Edit,
                ct))
        {
            return PermDenied<ControlledDocumentDetailModel>(correlationId);
        }

        if (input.ExpiryDate is { } expiry && input.EffectiveDate is { } effective && expiry < effective)
        {
            return Fail<ControlledDocumentDetailModel>("Expiry date cannot precede the effective date.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        if (!string.IsNullOrWhiteSpace(input.Title))
        {
            document.Title = input.Title.Trim();
        }

        document.Description = input.Description?.Trim();
        document.Tags = NormalizeTags(input.Tags);
        document.EffectiveDate = input.EffectiveDate;
        document.ReviewDate = input.ReviewDate;
        document.ExpiryDate = input.ExpiryDate;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        document.UpdatedBy = _currentUser.ActorName;
        await _documents.UpdateAsync(document, ct);

        return Response<ControlledDocumentDetailModel>.Success(ControlledDocumentMapping.ToDetail(document), correlationId: correlationId);
    }

    private Task<bool> CanViewAsync(ControlledDocument document, CancellationToken ct) =>
        _access.CanViewControlledDocumentAsync(document, null, ct);

    private async Task<IReadOnlyList<ControlledDocument>> GetVisibleDocumentsAsync(Guid? collectionInstanceId, CancellationToken ct)
    {
        IReadOnlyList<ControlledDocument> source = collectionInstanceId.HasValue
            ? await _documents.GetByCollectionInstanceAsync(collectionInstanceId.Value, ct)
            : await _documents.GetAllForTenantAsync(ct);

        var sharedItemIds = await SharedDocumentIdsAsync(ct);

        var visible = new List<ControlledDocument>();
        foreach (var document in source)
        {
            if (await _access.CanViewControlledDocumentAsync(document, sharedItemIds, ct))
            {
                visible.Add(document);
            }
        }

        return visible.OrderByDescending(d => d.CreatedAt).ToList();
    }

    private async Task<HashSet<Guid>> SharedDocumentIdsAsync(CancellationToken ct)
    {
        var ids = new HashSet<Guid>();
        foreach (var company in _access.Principal.CompanyIds)
        {
            foreach (var share in await _shares.GetSharesForTargetCompanyAsync(company, ct))
            {
                if (share.ItemKind == SharedItemKind.ControlledDocument)
                {
                    ids.Add(share.CopiedItemId ?? share.ItemId);
                }
            }
        }

        return ids;
    }

    private static List<string> NormalizeTags(IReadOnlyList<string>? tags) =>
        (tags ?? [])
            .Select(t => t?.Trim() ?? string.Empty)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static DocumentAccessPolicy BuildAccessPolicy(DocumentAccessPolicyInput? input)
    {
        if (input is null)
        {
            return new DocumentAccessPolicy { Source = AccessPolicySource.Inherited };
        }

        var source = string.Equals(input.Source, "EXPLICIT", StringComparison.OrdinalIgnoreCase)
            ? AccessPolicySource.Explicit
            : AccessPolicySource.Inherited;
        var grants = new List<DocumentAccessGrant>();
        foreach (var g in input.Grants ?? [])
        {
            var action = ControlledDocumentWire.ParseAccessAction(g.Action);
            var targetType = ControlledDocumentWire.ParseTargetType(g.TargetType);
            if (action is null || targetType is null || string.IsNullOrWhiteSpace(g.TargetId))
            {
                continue;
            }

            grants.Add(new DocumentAccessGrant { Action = action.Value, TargetType = targetType.Value, TargetId = g.TargetId.Trim() });
        }

        return new DocumentAccessPolicy { Source = source, Grants = grants };
    }

    private static Response<T> NotFound<T>(string correlationId) =>
        Response<T>.Fail("Not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage, correlationId);

    private static Response<T> PermDenied<T>(string correlationId) =>
        Response<T>.Fail("Permission denied.", 403, ControlledDocumentReasonCodes.PermissionDenied, correlationId);

    private static Response<T> Fail<T>(string error, int status, string? reason, string correlationId) =>
        Response<T>.Fail(error, status, reason, correlationId);

    private static Response<T> Fail<T>(IReadOnlyList<string> errors, int status, string? reason, string correlationId) =>
        Response<T>.Fail(errors, status, reason, correlationId);
}

public sealed record CreateControlledDocumentInput(
    Guid CollectionInstanceId,
    Guid CompanyId,
    string Title,
    string DocumentType,
    string? Description,
    IReadOnlyList<string>? Tags,
    bool Controlled,
    DateTimeOffset? EffectiveDate,
    DateTimeOffset? ReviewDate,
    DateTimeOffset? ExpiryDate,
    FileUploadInput File,
    string? ChangeSummary,
    DocumentAccessPolicyInput? AccessPolicy);

public sealed record EditControlledDocumentInput(
    string? Title,
    string? Description,
    IReadOnlyList<string>? Tags,
    DateTimeOffset? EffectiveDate,
    DateTimeOffset? ReviewDate,
    DateTimeOffset? ExpiryDate);

public sealed record DocumentDownloadResult(string StorageProvider, string ObjectKey, string FileName, string MediaType);

public sealed record DocumentFavoriteResult(Guid DocumentId, bool IsFavorite);
