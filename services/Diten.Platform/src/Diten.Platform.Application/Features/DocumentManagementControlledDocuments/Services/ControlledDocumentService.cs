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
        if (!await _access.HasFolderUploadAsync(input.CollectionInstanceId, ct))
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

    public async Task<Response<IReadOnlyList<ControlledDocumentListItemModel>>> ListAsync(Guid? collectionInstanceId, string correlationId, CancellationToken ct)
    {
        var visible = await GetVisibleDocumentsAsync(collectionInstanceId, ct);
        return Response<IReadOnlyList<ControlledDocumentListItemModel>>.Success(
            visible.Select(ControlledDocumentMapping.ToListItem).ToList(), correlationId: correlationId);
    }

    public async Task<Response<ControlledDocumentDetailModel>> GetDetailAsync(Guid documentId, string correlationId, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReachItemAsync(SharedItemKind.ControlledDocument, documentId, document.OwnerCompanyId, ct))
        {
            return NotFound<ControlledDocumentDetailModel>(correlationId);
        }

        if (!await CanViewAsync(document, ct))
        {
            return PermDenied<ControlledDocumentDetailModel>(correlationId);
        }

        return Response<ControlledDocumentDetailModel>.Success(ControlledDocumentMapping.ToDetail(document), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<DocumentVersionModel>>> GetVersionsAsync(Guid documentId, string correlationId, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReachItemAsync(SharedItemKind.ControlledDocument, documentId, document.OwnerCompanyId, ct))
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
        if (document is null || !await _access.CanReachItemAsync(SharedItemKind.ControlledDocument, documentId, document.OwnerCompanyId, ct))
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

    public async Task<Response<DocumentVersionModel>> CreateVersionAsync(Guid documentId, FileUploadInput file, string? changeSummary, string correlationId, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReachItemAsync(SharedItemKind.ControlledDocument, documentId, document.OwnerCompanyId, ct))
        {
            return NotFound<DocumentVersionModel>(correlationId);
        }

        // Layer 2 version-create (document-level or inherited folder-level canUploadNewVersion).
        if (!await _access.HasDocumentActionAsync(document.AccessPolicy, document.CollectionInstanceId, DocumentAccessAction.Version, ct))
        {
            return PermDenied<DocumentVersionModel>(correlationId);
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
        if (document is null || !await _access.CanReachItemAsync(SharedItemKind.ControlledDocument, documentId, document.OwnerCompanyId, ct))
        {
            return NotFound<DocumentDownloadResult>(correlationId);
        }

        // Backend-gated download: tenant → company → document/folder access → version → download permission.
        if (!await _access.HasDocumentActionAsync(document.AccessPolicy, document.CollectionInstanceId, DocumentAccessAction.Download, ct))
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
        if (document is null || !await _access.CanReachItemAsync(SharedItemKind.ControlledDocument, documentId, document.OwnerCompanyId, ct))
        {
            return NotFound<ControlledDocumentDetailModel>(correlationId);
        }

        if (!await _access.HasDocumentActionAsync(document.AccessPolicy, document.CollectionInstanceId, DocumentAccessAction.Edit, ct))
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

    private async Task<bool> CanViewAsync(ControlledDocument document, CancellationToken ct)
    {
        // Documented first-version reduction (§2 Access Control): list/detail visibility is company-scoped.
        // An owner-company principal (or an explicit-share target, already verified by CanReachItemAsync) may
        // view the library entry; per-document Layer 2 still gates edit / version / share / download.
        if (_access.Principal.BelongsToCompany(document.OwnerCompanyId))
        {
            return true;
        }

        return await _access.HasDocumentActionAsync(document.AccessPolicy, document.CollectionInstanceId, DocumentAccessAction.View, ct);
    }

    private async Task<IReadOnlyList<ControlledDocument>> GetVisibleDocumentsAsync(Guid? collectionInstanceId, CancellationToken ct)
    {
        IReadOnlyList<ControlledDocument> source = collectionInstanceId.HasValue
            ? await _documents.GetByCollectionInstanceAsync(collectionInstanceId.Value, ct)
            : await _documents.GetAllForTenantAsync(ct);

        var principal = _access.Principal;
        var sharedItemIds = await SharedDocumentIdsAsync(ct);

        return source
            .Where(d => principal.BelongsToCompany(d.OwnerCompanyId) || sharedItemIds.Contains(d.Id))
            .OrderByDescending(d => d.CreatedAt)
            .ToList();
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
