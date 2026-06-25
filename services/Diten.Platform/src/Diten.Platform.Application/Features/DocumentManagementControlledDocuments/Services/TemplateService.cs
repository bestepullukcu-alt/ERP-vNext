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

            if (!await _access.HasFolderUploadAsync(instanceId, ct))
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

    public async Task<Response<IReadOnlyList<TemplateListItemModel>>> ListAsync(Guid? collectionInstanceId, string correlationId, CancellationToken ct)
    {
        IReadOnlyList<TemplateDocument> source = collectionInstanceId.HasValue
            ? await _templates.GetByCollectionInstanceAsync(collectionInstanceId.Value, ct)
            : await _templates.GetAllForTenantAsync(ct);

        var principal = _access.Principal;
        var shared = await SharedTemplateIdsAsync(ct);
        var visible = source
            .Where(t => principal.BelongsToCompany(t.OwnerCompanyId) || shared.Contains(t.Id))
            .OrderByDescending(t => t.CreatedAt)
            .Select(ControlledDocumentMapping.ToListItem)
            .ToList();

        return Response<IReadOnlyList<TemplateListItemModel>>.Success(visible, correlationId: correlationId);
    }

    public async Task<Response<TemplateDetailModel>> GetDetailAsync(Guid templateId, string correlationId, CancellationToken ct)
    {
        var template = await _templates.GetByIdAsync(templateId, ct);
        if (template is null || !await _access.CanReachItemAsync(SharedItemKind.Template, templateId, template.OwnerCompanyId, ct))
        {
            return NotFound<TemplateDetailModel>(correlationId);
        }

        return Response<TemplateDetailModel>.Success(ControlledDocumentMapping.ToDetail(template), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<DocumentVersionModel>>> GetVersionsAsync(Guid templateId, string correlationId, CancellationToken ct)
    {
        var template = await _templates.GetByIdAsync(templateId, ct);
        if (template is null || !await _access.CanReachItemAsync(SharedItemKind.Template, templateId, template.OwnerCompanyId, ct))
        {
            return NotFound<IReadOnlyList<DocumentVersionModel>>(correlationId);
        }

        var versions = await _versions.GetByTemplateAsync(templateId, ct);
        return Response<IReadOnlyList<DocumentVersionModel>>.Success(
            versions.OrderByDescending(v => v.VersionNumber).Select(ControlledDocumentMapping.ToVersionModel).ToList(),
            correlationId: correlationId);
    }

    public async Task<Response<DocumentVersionModel>> CreateVersionAsync(Guid templateId, FileUploadInput file, string? changeSummary, string correlationId, CancellationToken ct)
    {
        var template = await _templates.GetByIdAsync(templateId, ct);
        if (template is null || !await _access.CanReachItemAsync(SharedItemKind.Template, templateId, template.OwnerCompanyId, ct))
        {
            return NotFound<DocumentVersionModel>(correlationId);
        }

        if (template.CollectionInstanceId is { } instanceId
            && !await _access.HasDocumentActionAsync(template.AccessPolicy, instanceId, DocumentAccessAction.Version, ct)
            && !_access.Principal.BelongsToCompany(template.OwnerCompanyId))
        {
            return PermDenied<DocumentVersionModel>(correlationId);
        }

        if (template.CollectionInstanceId is null && !_access.Principal.BelongsToCompany(template.OwnerCompanyId))
        {
            return PermDenied<DocumentVersionModel>(correlationId);
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
        if (template is null || !await _access.CanReachItemAsync(SharedItemKind.Template, templateId, template.OwnerCompanyId, ct))
        {
            return NotFound<DocumentDownloadResult>(correlationId);
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
