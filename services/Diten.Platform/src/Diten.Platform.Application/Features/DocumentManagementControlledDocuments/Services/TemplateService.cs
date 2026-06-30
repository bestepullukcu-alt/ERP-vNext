using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — reusable template attach / version / read / download orchestration. Mirrors
/// <see cref="ControlledDocumentService"/>; a template may be attached to a folder or stay company-scoped.
/// </summary>
public sealed class TemplateService
{
    private readonly ICollectionInstanceReferenceReader _reader;
    private readonly ITemplateDocumentRepository _templates;
    private readonly ITemplateVersionRepository _versions;
    private readonly IDocumentShareRecordRepository _shares;
    private readonly DocumentVersioningService _versioning;
    private readonly DocumentAccessEvaluator _access;
    private readonly DocumentKeyFactory _keyFactory;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly ControlledDocumentsFeatureFlagOptions _flags;

    public TemplateService(
        ICollectionInstanceReferenceReader reader,
        ITemplateDocumentRepository templates,
        ITemplateVersionRepository versions,
        IDocumentShareRecordRepository shares,
        DocumentVersioningService versioning,
        DocumentAccessEvaluator access,
        DocumentKeyFactory keyFactory,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        IOptions<ControlledDocumentsFeatureFlagOptions> flags)
    {
        _reader = reader;
        _templates = templates;
        _versions = versions;
        _shares = shares;
        _versioning = versioning;
        _access = access;
        _keyFactory = keyFactory;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _flags = flags.Value;
    }

    public async Task<Response<TemplateDetailModel>> CreateAsync(CreateTemplateInput input, string correlationId, CancellationToken ct)
    {
        if (!_flags.ControlledDocumentsEnabled)
        {
            return Fail<TemplateDetailModel>("Controlled documents are not enabled.", 403, ControlledDocumentReasonCodes.FeatureDisabled, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return Fail<TemplateDetailModel>("Title is required.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        Guid companyId = input.CompanyId;
        string? collectionPath = null;
        string? canonicalId = null;

        if (input.CollectionInstanceId is { } instanceId)
        {
            var folder = await _reader.ResolveByIdAsync(instanceId, ct);
            if (folder is null || !await _reader.ValidateScopeAsync(instanceId, input.CompanyId, ct))
            {
                return NotFound<TemplateDetailModel>(correlationId);
            }

            if (!folder.IsUsable)
            {
                return Fail<TemplateDetailModel>("The target folder is not active.", 409, ControlledDocumentReasonCodes.Conflict, correlationId);
            }

            if (!await _access.HasFolderCreateTemplateAsync(instanceId, ct))
            {
                return PermDenied<TemplateDetailModel>(correlationId);
            }

            companyId = folder.CompanyId;
            collectionPath = folder.FullPath;
            canonicalId = folder.CanonicalId;
        }

        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var stored = await _versioning.StoreAsync(
            ContentStorageScope.Templates, companyId, templateId, versionId, input.File, _currentUser.ActorName, correlationId, ct);
        if (!stored.IsSuccessful)
        {
            return Fail<TemplateDetailModel>(stored.Errors, stored.StatusCode, stored.ReasonCode, correlationId);
        }

        var flags = input.Flags is null
            ? new TemplateFlags()
            : new TemplateFlags
            {
                Reusable = input.Flags.Reusable,
                Shareable = input.Flags.Shareable,
                CopyableOnAdopt = input.Flags.CopyableOnAdopt,
                ReferenceOnly = input.Flags.ReferenceOnly
            };

        var template = new TemplateDocument
        {
            Id = templateId,
            TenantId = tenantId,
            TemplateKey = _keyFactory.ForTemplate(tenantId, companyId, input.CollectionInstanceId, input.Title),
            CompanyId = companyId,
            OwnerCompanyId = companyId,
            CollectionInstanceId = input.CollectionInstanceId,
            CollectionPath = collectionPath,
            CanonicalId = canonicalId,
            Title = input.Title.Trim(),
            Description = input.Description?.Trim(),
            Tags = NormalizeTags(input.Tags),
            TemplateFlags = flags,
            CurrentVersionId = versionId,
            CurrentVersionNumber = 1,
            Status = ControlledItemStatus.Active,
            CreatedBy = _currentUser.ActorName
        };

        var version = new TemplateVersion
        {
            Id = versionId,
            TenantId = tenantId,
            TemplateId = templateId,
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
            await _templates.CreateAsync(template, ct);
            await _versions.CreateAsync(version, ct);
        }
        catch
        {
            await _versioning.TryDeleteAsync(stored.Data!, CancellationToken.None);
            return Fail<TemplateDetailModel>("Could not persist the template.", 409, ControlledDocumentReasonCodes.Conflict, correlationId);
        }

        return Response<TemplateDetailModel>.Success(ControlledDocumentMapping.ToDetail(template), 201, correlationId);
    }

    public async Task<Response<TemplateDetailModel>> CopyAsync(Guid templateId, Guid targetCollectionInstanceId, string? titleOverride, string correlationId, CancellationToken ct)
    {
        var source = await _templates.GetByIdAsync(templateId, ct);
        if (source is null || !await _access.CanReachTemplateAsync(source, ct))
        {
            return Fail<TemplateDetailModel>("Not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var target = await _reader.ResolveByIdAsync(targetCollectionInstanceId, ct);
        if (target is null || target.CompanyId != source.OwnerCompanyId)
        {
            return Fail<TemplateDetailModel>("Not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage, correlationId);
        }

        if (!target.IsUsable)
        {
            return Fail<TemplateDetailModel>("The target folder is not active.", 409, ControlledDocumentReasonCodes.Conflict, correlationId);
        }

        if (!await _access.HasFolderCreateTemplateAsync(targetCollectionInstanceId, ct) && !_access.Principal.BelongsToCompany(target.CompanyId))
        {
            return PermDenied<TemplateDetailModel>(correlationId);
        }

        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var newId = Guid.NewGuid();
        var title = string.IsNullOrWhiteSpace(titleOverride) ? source.Title : titleOverride.Trim();
        var copy = new TemplateDocument
        {
            Id = newId,
            TenantId = tenantId,
            TemplateKey = _keyFactory.ForTemplate(tenantId, target.CompanyId, target.CollectionInstanceId, $"{title}|copy|{newId:N}"),
            CompanyId = target.CompanyId,
            OwnerCompanyId = target.CompanyId,
            CollectionInstanceId = target.CollectionInstanceId,
            CollectionPath = target.FullPath,
            CanonicalId = target.CanonicalId,
            Title = title,
            Description = source.Description,
            Tags = [.. source.Tags],
            TemplateFlags = new TemplateFlags
            {
                Reusable = source.TemplateFlags.Reusable,
                Shareable = source.TemplateFlags.Shareable,
                CopyableOnAdopt = source.TemplateFlags.CopyableOnAdopt,
                ReferenceOnly = source.TemplateFlags.ReferenceOnly
            },
            CurrentVersionNumber = 1,
            Status = ControlledItemStatus.Active,
            CopiedFromTemplateId = source.Id,
            CreatedBy = _currentUser.ActorName
        };

        var sourceVersion = source.CurrentVersionId is { } cv ? await _versions.GetByIdAsync(cv, ct) : null;
        if (sourceVersion is not null)
        {
            var versionId = Guid.NewGuid();
            copy.CurrentVersionId = versionId;
            await _templates.CreateAsync(copy, ct);
            await _versions.CreateAsync(CloneTemplateVersion(sourceVersion, newId, versionId, tenantId), ct);
        }
        else
        {
            await _templates.CreateAsync(copy, ct);
        }

        return Response<TemplateDetailModel>.Success(ControlledDocumentMapping.ToDetail(copy), 201, correlationId);
    }

    private TemplateVersion CloneTemplateVersion(TemplateVersion source, Guid templateId, Guid versionId, Guid tenantId) => new()
    {
        Id = versionId,
        TenantId = tenantId,
        TemplateId = templateId,
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

    public async Task<Response<IReadOnlyList<TemplateListItemModel>>> ListAsync(Guid? collectionInstanceId, string correlationId, CancellationToken ct)
    {
        IReadOnlyList<TemplateDocument> source = collectionInstanceId.HasValue
            ? await _templates.GetByCollectionInstanceAsync(collectionInstanceId.Value, ct)
            : await _templates.GetAllForTenantAsync(ct);

        var shared = await SharedTemplateIdsAsync(ct);

        var visible = new List<TemplateDocument>();
        foreach (var template in source)
        {
            if (await _access.CanViewTemplateDocumentAsync(template, shared, ct))
            {
                visible.Add(template);
            }
        }

        var items = visible
            .OrderByDescending(t => t.CreatedAt)
            .Select(ControlledDocumentMapping.ToListItem)
            .ToList();

        return Response<IReadOnlyList<TemplateListItemModel>>.Success(items, correlationId: correlationId);
    }

    public async Task<Response<TemplateDetailModel>> GetDetailAsync(Guid templateId, string correlationId, CancellationToken ct)
    {
        var template = await _templates.GetByIdAsync(templateId, ct);
        if (template is null || !await _access.CanReachTemplateAsync(template, ct))
        {
            return NotFound<TemplateDetailModel>(correlationId);
        }

        return Response<TemplateDetailModel>.Success(ControlledDocumentMapping.ToDetail(template), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<DocumentVersionModel>>> GetVersionsAsync(Guid templateId, string correlationId, CancellationToken ct)
    {
        var template = await _templates.GetByIdAsync(templateId, ct);
        if (template is null || !await _access.CanReachTemplateAsync(template, ct))
        {
            return NotFound<IReadOnlyList<DocumentVersionModel>>(correlationId);
        }

        var versions = await _versions.GetByTemplateAsync(templateId, ct);
        return Response<IReadOnlyList<DocumentVersionModel>>.Success(
            versions.OrderByDescending(v => v.VersionNumber).Select(ControlledDocumentMapping.ToVersionModel).ToList(),
            correlationId: correlationId);
    }

    public async Task<Response<DocumentVersionModel>> CreateVersionAsync(Guid templateId, FileUploadInput file, string? changeSummary, bool allowUnchanged, string correlationId, CancellationToken ct)
    {
        var template = await _templates.GetByIdAsync(templateId, ct);
        if (template is null || !await _access.CanReachTemplateAsync(template, ct))
        {
            return NotFound<DocumentVersionModel>(correlationId);
        }

        if (!await _access.HasTemplateDocumentActionOrOwnerDefaultAsync(
                template,
                DocumentAccessMatrixAction.UploadVersion,
                DocumentAccessAction.Version,
                ct))
        {
            return PermDenied<DocumentVersionModel>(correlationId);
        }

        // Content-change guard: reject a byte-identical re-upload of the current active version before any storage
        // write (no orphan) unless explicitly forced — same deterministic "did it actually change?" check as documents.
        if (!allowUnchanged && template.CurrentVersionId is { } activeId)
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
        var nextNumber = await _versions.GetMaxVersionNumberAsync(templateId, ct) + 1;
        var versionId = Guid.NewGuid();

        var stored = await _versioning.StoreAsync(
            ContentStorageScope.Templates, template.CompanyId, templateId, versionId, file, _currentUser.ActorName, correlationId, ct);
        if (!stored.IsSuccessful)
        {
            return Fail<DocumentVersionModel>(stored.Errors, stored.StatusCode, stored.ReasonCode, correlationId);
        }

        var version = new TemplateVersion
        {
            Id = versionId,
            TenantId = tenantId,
            TemplateId = templateId,
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
            await _versions.SupersedeActiveVersionsAsync(templateId, versionId, ct);
            template.CurrentVersionId = versionId;
            template.CurrentVersionNumber = nextNumber;
            template.UpdatedAt = DateTimeOffset.UtcNow;
            template.UpdatedBy = _currentUser.ActorName;
            await _templates.UpdateAsync(template, ct);
        }
        catch
        {
            await _versioning.TryDeleteAsync(stored.Data!, CancellationToken.None);
            return Fail<DocumentVersionModel>("Duplicate or conflicting version.", 409, ControlledDocumentReasonCodes.Conflict, correlationId);
        }

        return Response<DocumentVersionModel>.Success(ControlledDocumentMapping.ToVersionModel(version), 201, correlationId);
    }

    public async Task<Response<DocumentDownloadResult>> DownloadAsync(Guid templateId, Guid versionId, string correlationId, CancellationToken ct)
    {
        var template = await _templates.GetByIdAsync(templateId, ct);
        if (template is null || !await _access.CanReachTemplateAsync(template, ct))
        {
            return NotFound<DocumentDownloadResult>(correlationId);
        }

        if (!await _access.HasTemplateDocumentActionOrOwnerDefaultAsync(
                template,
                DocumentAccessMatrixAction.Download,
                DocumentAccessAction.Download,
                ct))
        {
            return PermDenied<DocumentDownloadResult>(correlationId);
        }

        var version = await _versions.GetByIdAsync(versionId, ct);
        if (version is null || version.TemplateId != templateId)
        {
            return NotFound<DocumentDownloadResult>(correlationId);
        }

        return Response<DocumentDownloadResult>.Success(
            new DocumentDownloadResult(version.FileRef.StorageProvider, version.FileRef.ObjectKey, version.FileRef.FileName, version.FileRef.MediaType),
            correlationId: correlationId);
    }

    private async Task<HashSet<Guid>> SharedTemplateIdsAsync(CancellationToken ct)
    {
        var ids = new HashSet<Guid>();
        foreach (var company in _access.Principal.CompanyIds)
        {
            foreach (var share in await _shares.GetSharesForTargetCompanyAsync(company, ct))
            {
                if (share.ItemKind == SharedItemKind.Template)
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

    private static Response<T> NotFound<T>(string correlationId) =>
        Response<T>.Fail("Not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage, correlationId);

    private static Response<T> PermDenied<T>(string correlationId) =>
        Response<T>.Fail("Permission denied.", 403, ControlledDocumentReasonCodes.PermissionDenied, correlationId);

    private static Response<T> Fail<T>(string error, int status, string? reason, string correlationId) =>
        Response<T>.Fail(error, status, reason, correlationId);

    private static Response<T> Fail<T>(IReadOnlyList<string> errors, int status, string? reason, string correlationId) =>
        Response<T>.Fail(errors, status, reason, correlationId);
}

public sealed record CreateTemplateInput(
    Guid CompanyId,
    Guid? CollectionInstanceId,
    string Title,
    string? Description,
    IReadOnlyList<string>? Tags,
    TemplateFlagsInput? Flags,
    FileUploadInput File,
    string? ChangeSummary);
