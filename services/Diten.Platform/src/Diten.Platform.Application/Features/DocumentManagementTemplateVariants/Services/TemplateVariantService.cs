using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Services;

/// <summary>
/// MOD-0029-FU03 — template variant governance + drift orchestration. Drift is computed read-time and never
/// persisted. Rebase updates the last-rebased master lineage only — it never merges content, mutates binary/file
/// data, or overwrites the linked TemplateDocument / TemplateVersion. No approval workflow, queue, or MOD-0023
/// integration is performed here.
/// </summary>
public sealed class TemplateVariantService
{
    private readonly ITemplateVariantRepository _variants;
    private readonly ITemplateMasterRepository _masters;
    private readonly ITemplateMasterVersionRepository _masterVersions;
    private readonly ITemplateDocumentRepository _templateDocuments;
    private readonly ITemplateVersionRepository _templateVersions;
    private readonly ICollectionInstanceReferenceReader _collectionInstances;
    private readonly DocumentKeyFactory _keyFactory;
    private readonly DocumentVersioningService _versioning;
    private readonly DocumentAccessEvaluator? _access;
    private readonly DocumentAccessResolver? _resolver;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public TemplateVariantService(
        ITemplateVariantRepository variants,
        ITemplateMasterRepository masters,
        ITemplateMasterVersionRepository masterVersions,
        ITemplateDocumentRepository templateDocuments,
        ITemplateVersionRepository templateVersions,
        ICollectionInstanceReferenceReader collectionInstances,
        DocumentKeyFactory keyFactory,
        DocumentVersioningService versioning,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        DocumentAccessEvaluator? access = null,
        DocumentAccessResolver? resolver = null)
    {
        _variants = variants;
        _masters = masters;
        _masterVersions = masterVersions;
        _templateDocuments = templateDocuments;
        _templateVersions = templateVersions;
        _collectionInstances = collectionInstances;
        _keyFactory = keyFactory;
        _versioning = versioning;
        _access = access;
        _resolver = resolver;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Read-time drift computation. Priority is deterministic: Blocked &gt; Drifted &gt; RebaseRequired &gt; InSync.
    /// A missing/deprecated/archived master or a Blocked approval placeholder yields Blocked. When the variant has
    /// no last-rebased version number, RebaseRequired is the explicit chosen behavior (a published/active master is
    /// assumed to be ahead of an un-initialized variant).
    /// </summary>
    public static TemplateVariantDriftStatus ComputeDrift(TemplateVariant variant, TemplateMaster? master)
    {
        if (master is null || master.Status is TemplateMasterStatus.Deprecated or TemplateMasterStatus.Archived)
        {
            return TemplateVariantDriftStatus.Blocked;
        }

        if (variant.ApprovalStatus == TemplateVariantApprovalStatus.Blocked)
        {
            return TemplateVariantDriftStatus.Blocked;
        }

        if (variant.HasLocalChanges)
        {
            return TemplateVariantDriftStatus.Drifted;
        }

        if (variant.LastRebasedMasterVersionNumber is not { } rebased)
        {
            return TemplateVariantDriftStatus.RebaseRequired;
        }

        return master.CurrentMasterVersion > rebased
            ? TemplateVariantDriftStatus.RebaseRequired
            : TemplateVariantDriftStatus.InSync;
    }

    public async Task<Response<TemplateVariantDetailModel>> CreateAsync(CreateTemplateVariantInput input, string correlationId, CancellationToken ct)
    {
        if (input is null)
        {
            return Fail<TemplateVariantDetailModel>("Request body is required.", 400, TemplateVariantReasonCodes.ValidationFailed, correlationId);
        }

        var scopeType = TemplateVariantWire.ParseScopeType(input.ScopeType);
        if (scopeType is null)
        {
            return Fail<TemplateVariantDetailModel>("ScopeType is not recognized.", 400, TemplateVariantReasonCodes.InvalidScope, correlationId);
        }

        if (input.ScopeId == Guid.Empty)
        {
            return Fail<TemplateVariantDetailModel>("ScopeId is required.", 400, TemplateVariantReasonCodes.InvalidScope, correlationId);
        }

        if (input.TargetCollectionInstanceId == Guid.Empty)
        {
            return Fail<TemplateVariantDetailModel>("TargetCollectionInstanceId is required.", 400, TemplateVariantReasonCodes.InvalidTargetFolder, correlationId);
        }

        var contentSource = TemplateVariantWire.ParseContentSource(input.ContentSource);
        if (contentSource is null)
        {
            return Fail<TemplateVariantDetailModel>("ContentSource is not recognized.", 400, TemplateVariantReasonCodes.InvalidContentSource, correlationId);
        }

        if (contentSource == TemplateVariantContentSource.MasterVersion && input.LocalFile is not null)
        {
            return Fail<TemplateVariantDetailModel>("Local file upload is not allowed when ContentSource is MasterVersion.", 400, TemplateVariantReasonCodes.LocalFileNotAllowed, correlationId);
        }

        if (contentSource == TemplateVariantContentSource.LocalUpload && input.LocalFile is null)
        {
            return Fail<TemplateVariantDetailModel>("A local variant file is required when ContentSource is LocalUpload.", 400, TemplateVariantReasonCodes.LocalFileRequired, correlationId);
        }

        var status = TemplateVariantWire.ParseStatus(input.Status) ?? TemplateVariantStatus.Draft;

        // Tenant-scoped repositories: a missing or cross-tenant master returns null → 404 non-leakage.
        var master = await _masters.GetByIdAsync(input.TemplateMasterId, ct);
        if (master is null)
        {
            return NotFound<TemplateVariantDetailModel>(correlationId);
        }

        if (master.Status is TemplateMasterStatus.Deprecated or TemplateMasterStatus.Archived)
        {
            return Fail<TemplateVariantDetailModel>("Variants cannot be created from a deprecated or archived master.", 409, TemplateVariantReasonCodes.MasterInactive, correlationId);
        }

        var version = await _masterVersions.GetByIdAsync(input.TemplateMasterVersionId, ct);
        if (version is null || version.TemplateMasterId != master.Id)
        {
            return Fail<TemplateVariantDetailModel>("The selected master version does not belong to the selected master.", 400, TemplateVariantReasonCodes.InvalidMasterVersion, correlationId);
        }

        if (!HasValidContent(version.FileRef))
        {
            return Fail<TemplateVariantDetailModel>("The selected master version has no valid content reference.", 409, TemplateVariantReasonCodes.InvalidMasterContent, correlationId);
        }

        var variantCode = NormalizeCode(input.VariantCode);
        if (await _variants.GetByScopeAndCodeAsync(scopeType.Value, input.ScopeId, variantCode, ct) is not null)
        {
            return Fail<TemplateVariantDetailModel>("A variant with the same code already exists in this scope.", 409, TemplateVariantReasonCodes.DuplicateVariantCode, correlationId);
        }

        var folder = await _collectionInstances.ResolveByIdAsync(input.TargetCollectionInstanceId, ct);
        if (folder is null)
        {
            return Fail<TemplateVariantDetailModel>("Target folder was not found.", 404, TemplateVariantReasonCodes.NotFoundNonLeakage, correlationId);
        }

        if (!folder.IsUsable)
        {
            return Fail<TemplateVariantDetailModel>("The target folder is not active.", 409, TemplateVariantReasonCodes.InvalidTargetFolder, correlationId);
        }

        if (!IsFolderCompatible(scopeType.Value, input.ScopeId, folder))
        {
            return Fail<TemplateVariantDetailModel>("The target folder is not compatible with the selected scope.", 400, TemplateVariantReasonCodes.InvalidScope, correlationId);
        }

        if (_access is not null && !await _access.HasFolderCreateTemplateAsync(input.TargetCollectionInstanceId, ct))
        {
            return PermissionDenied<TemplateVariantDetailModel>(correlationId);
        }

        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var now = DateTimeOffset.UtcNow;
        var templateId = Guid.NewGuid();
        var templateVersionId = Guid.NewGuid();
        var title = input.VariantName.Trim();
        ContentStoreResult? storedLocalContent = null;
        var linkedTemplate = new TemplateDocument
        {
            Id = templateId,
            TenantId = tenantId,
            TemplateKey = _keyFactory.ForTemplate(tenantId, folder.CompanyId, folder.CollectionInstanceId, $"{variantCode}|{templateId:N}"),
            CompanyId = folder.CompanyId,
            OwnerCompanyId = folder.CompanyId,
            CollectionInstanceId = folder.CollectionInstanceId,
            CollectionPath = folder.FullPath,
            CanonicalId = folder.CanonicalId,
            Title = title,
            Description = TrimOrNull(input.Description),
            TemplateFlags = new TemplateFlags
            {
                Reusable = true,
                Shareable = false,
                CopyableOnAdopt = false,
                ReferenceOnly = false
            },
            CurrentVersionId = templateVersionId,
            CurrentVersionNumber = 1,
            Status = ControlledItemStatus.Active,
            TemplateMasterId = master.Id,
            TemplateMasterVersionId = version.Id,
            SourceTemplateDocumentId = null,
            SourceTemplateVersionId = null,
            CreatedBy = _currentUser.ActorName
        };

        ContentRef fileRef;
        string checksum;
        string changeSummary;
        if (contentSource == TemplateVariantContentSource.LocalUpload)
        {
            var stored = await _versioning.StoreAsync(
                ContentStorageScope.Templates,
                folder.CompanyId,
                templateId,
                templateVersionId,
                input.LocalFile!,
                _currentUser.ActorName,
                correlationId,
                ct);
            if (!stored.IsSuccessful)
            {
                return Response<TemplateVariantDetailModel>.Fail(
                    stored.Errors,
                    stored.StatusCode == 0 ? 503 : stored.StatusCode,
                    stored.ReasonCode ?? TemplateVariantReasonCodes.StorageUnavailable,
                    correlationId);
            }

            storedLocalContent = stored.Data!;
            fileRef = DocumentVersioningService.ToContentRef(storedLocalContent, templateVersionId, _currentUser.ActorName);
            checksum = storedLocalContent.Checksum;
            changeSummary = $"Created as local variant from template master {master.MasterCode} version {version.VersionNumber}";
        }
        else
        {
            fileRef = CloneContentRef(version.FileRef, templateVersionId);
            checksum = version.Checksum;
            changeSummary = $"Created from template master {master.MasterCode} version {version.VersionNumber}";
        }

        var linkedVersion = new TemplateVersion
        {
            Id = templateVersionId,
            TenantId = tenantId,
            TemplateId = templateId,
            VersionNumber = 1,
            FileRef = fileRef,
            Checksum = checksum,
            UploadedBy = _currentUser.ActorName,
            UploadedAt = now,
            ChangeSummary = changeSummary,
            VersionStatus = DocumentVersionStatus.Active,
            CreatedBy = _currentUser.ActorName
        };

        var variant = new TemplateVariant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateMasterId = master.Id,
            TemplateMasterVersionId = version.Id,
            VariantCode = variantCode,
            VariantName = input.VariantName.Trim(),
            Description = TrimOrNull(input.Description),
            ScopeType = scopeType.Value,
            ScopeId = input.ScopeId,
            OwnerCompanyId = EmptyToNull(input.OwnerCompanyId),
            OwnerUserId = EmptyToNull(input.OwnerUserId),
            Status = status,
            ContentSource = contentSource.Value,
            // Initialize the last-rebased lineage from the selected master version so first computed drift is
            // InSync when that version is the master's current version (RebaseRequired if an older version is chosen).
            LastRebasedMasterVersionId = version.Id,
            LastRebasedMasterVersionNumber = version.VersionNumber,
            LastRebasedAt = now,
            LinkedTemplateDocumentId = linkedTemplate.Id,
            HasLocalChanges = contentSource == TemplateVariantContentSource.LocalUpload,
            ApprovalStatus = TemplateVariantApprovalStatus.NotRequired,
            CreatedBy = _currentUser.ActorName
        };

        try
        {
            await _templateDocuments.CreateAsync(linkedTemplate, ct);
            await _templateVersions.CreateAsync(linkedVersion, ct);
            await _variants.CreateAsync(variant, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await CompensateLinkedTemplateAsync(linkedTemplate.Id, linkedVersion.Id);
            await TryDeleteStoredContentAsync(storedLocalContent);
            return Fail<TemplateVariantDetailModel>("A variant with the same code already exists in this scope.", 409, TemplateVariantReasonCodes.DuplicateVariantCode, correlationId);
        }
        catch
        {
            await CompensateLinkedTemplateAsync(linkedTemplate.Id, linkedVersion.Id);
            await TryDeleteStoredContentAsync(storedLocalContent);
            return Fail<TemplateVariantDetailModel>("Could not persist the linked folder template for the variant.", 409, TemplateVariantReasonCodes.LinkedTemplateCreateFailed, correlationId);
        }

        var drift = ComputeDrift(variant, master);
        return Response<TemplateVariantDetailModel>.Success(TemplateVariantWire.ToDetail(variant, master, drift, linkedTemplate), 201, correlationId);
    }

    public async Task<Response<IReadOnlyList<TemplateVariantListItemModel>>> ListAsync(TemplateVariantListFilter filter, string correlationId, CancellationToken ct)
    {
        var rows = await _variants.ListAsync(filter.TemplateMasterId, filter.ScopeType, filter.ScopeId, filter.Status, filter.ApprovalStatus, ct);
        var visible = new List<TemplateVariant>();
        foreach (var row in rows)
        {
            if (await CanViewVariantAsync(row, ct))
            {
                visible.Add(row);
            }
        }

        var items = await MapListAsync(visible, ct);
        return Response<IReadOnlyList<TemplateVariantListItemModel>>.Success(items, correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<TemplateVariantListItemModel>>> GetByMasterAsync(Guid templateMasterId, string correlationId, CancellationToken ct)
    {
        if (await _masters.GetByIdAsync(templateMasterId, ct) is null)
        {
            return NotFound<IReadOnlyList<TemplateVariantListItemModel>>(correlationId);
        }

        var rows = await _variants.GetByMasterAsync(templateMasterId, ct);
        var visible = new List<TemplateVariant>();
        foreach (var row in rows)
        {
            if (await CanViewVariantAsync(row, ct))
            {
                visible.Add(row);
            }
        }

        var items = await MapListAsync(visible, ct);
        return Response<IReadOnlyList<TemplateVariantListItemModel>>.Success(items, correlationId: correlationId);
    }

    public async Task<Response<TemplateVariantDetailModel>> GetDetailAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var variant = await _variants.GetByIdAsync(id, ct);
        if (variant is null)
        {
            return NotFound<TemplateVariantDetailModel>(correlationId);
        }

        if (!await CanViewVariantAsync(variant, ct))
        {
            return NotFound<TemplateVariantDetailModel>(correlationId);
        }

        var master = await _masters.GetByIdAsync(variant.TemplateMasterId, ct);
        var linkedTemplate = variant.LinkedTemplateDocumentId is { } templateId
            ? await _templateDocuments.GetByIdAsync(templateId, ct)
            : null;
        var drift = ComputeDrift(variant, master);
        return Response<TemplateVariantDetailModel>.Success(TemplateVariantWire.ToDetail(variant, master, drift, linkedTemplate), correlationId: correlationId);
    }

    public async Task<Response<TemplateVariantCompareModel>> CompareAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var variant = await _variants.GetByIdAsync(id, ct);
        if (variant is null)
        {
            return NotFound<TemplateVariantCompareModel>(correlationId);
        }

        if (!await CanViewVariantAsync(variant, ct))
        {
            return PermissionDenied<TemplateVariantCompareModel>(correlationId);
        }

        var master = await _masters.GetByIdAsync(variant.TemplateMasterId, ct);
        if (master is null)
        {
            return NotFound<TemplateVariantCompareModel>(correlationId);
        }

        var drift = ComputeDrift(variant, master);
        var linkedTemplate = variant.LinkedTemplateDocumentId is { } templateId
            ? await _templateDocuments.GetByIdAsync(templateId, ct)
            : null;
        var model = new TemplateVariantCompareModel(
            variant.Id,
            variant.VariantCode,
            variant.VariantName,
            variant.Status.ToWire(),
            master.Id,
            master.MasterCode,
            master.TemplateName,
            master.Status.ToWire(),
            master.CurrentMasterVersion,
            variant.LastRebasedMasterVersionNumber,
            variant.HasLocalChanges,
            variant.ApprovalStatus.ToWire(),
            drift.ToWire(),
            variant.ContentSource.ToWire(),
            variant.ContentSource == TemplateVariantContentSource.MasterVersion,
            // Checksum-based deep comparison is not in scope; no checksum is safely available at the variant level.
            ChecksumEqual: null,
            linkedTemplate?.Id,
            linkedTemplate?.Title,
            linkedTemplate?.CollectionInstanceId,
            linkedTemplate?.CollectionPath,
            linkedTemplate?.CurrentVersionNumber,
            variant.LinkedTemplateDocumentId.HasValue && linkedTemplate?.CurrentVersionId.HasValue == true,
            "Metadata-level comparison only; no binary diff, content diff, or merge plan is computed in this FU.");

        return Response<TemplateVariantCompareModel>.Success(model, correlationId: correlationId);
    }

    public async Task<Response<TemplateVariantDetailModel>> RebaseAsync(Guid id, RebaseTemplateVariantInput input, string correlationId, CancellationToken ct)
    {
        var variant = await _variants.GetByIdAsync(id, ct);
        if (variant is null)
        {
            return NotFound<TemplateVariantDetailModel>(correlationId);
        }

        if (!await CanRebaseVariantAsync(variant, ct))
        {
            return PermissionDenied<TemplateVariantDetailModel>(correlationId);
        }

        var master = await _masters.GetByIdAsync(variant.TemplateMasterId, ct);
        if (master is null)
        {
            return NotFound<TemplateVariantDetailModel>(correlationId);
        }

        // Deterministic blocked rules: a deprecated/archived master or a Blocked approval placeholder blocks rebase
        // with no metadata change.
        if (master.Status is TemplateMasterStatus.Deprecated or TemplateMasterStatus.Archived)
        {
            return Fail<TemplateVariantDetailModel>("The master is deprecated or archived; the variant cannot be rebased.", 409, TemplateVariantReasonCodes.RebaseBlocked, correlationId);
        }

        if (variant.ApprovalStatus == TemplateVariantApprovalStatus.Blocked)
        {
            return Fail<TemplateVariantDetailModel>("The variant is blocked; it cannot be rebased.", 409, TemplateVariantReasonCodes.RebaseBlocked, correlationId);
        }

        // Select the target master version: an explicit input version (validated against the master) or the master's
        // current published version.
        TemplateMasterVersion? target;
        if (input?.TargetMasterVersionId is { } requested && requested != Guid.Empty)
        {
            target = await _masterVersions.GetByIdAsync(requested, ct);
            if (target is null || target.TemplateMasterId != master.Id)
            {
                return Fail<TemplateVariantDetailModel>("The selected master version does not belong to the variant's master.", 400, TemplateVariantReasonCodes.InvalidMasterVersion, correlationId);
            }
        }
        else if (master.CurrentVersionId is { } currentVersionId)
        {
            target = await _masterVersions.GetByIdAsync(currentVersionId, ct);
            if (target is null)
            {
                return Fail<TemplateVariantDetailModel>("The master has no resolvable current version to rebase onto.", 409, TemplateVariantReasonCodes.RebaseBlocked, correlationId);
            }
        }
        else
        {
            return Fail<TemplateVariantDetailModel>("The master has no published version to rebase onto.", 409, TemplateVariantReasonCodes.RebaseBlocked, correlationId);
        }

        // Metadata-only rebase: update last-rebased lineage and clear local-change state. No content merge, no binary
        // mutation, no TemplateVariantVersion creation, no folder-attached template overwrite.
        variant.LastRebasedMasterVersionId = target.Id;
        variant.LastRebasedMasterVersionNumber = target.VersionNumber;
        variant.LastRebasedAt = DateTimeOffset.UtcNow;
        variant.HasLocalChanges = false;
        variant.UpdatedAt = DateTimeOffset.UtcNow;
        variant.UpdatedBy = _currentUser.ActorName;
        await _variants.UpdateAsync(variant, ct);

        var drift = ComputeDrift(variant, master);
        var linkedTemplate = variant.LinkedTemplateDocumentId is { } templateId
            ? await _templateDocuments.GetByIdAsync(templateId, ct)
            : null;
        return Response<TemplateVariantDetailModel>.Success(TemplateVariantWire.ToDetail(variant, master, drift, linkedTemplate), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<TemplateVariantOptionModel>>> GetOptionsAsync(string correlationId, CancellationToken ct)
    {
        var masters = await _masters.ListAsync("Published", null, null, null, null, ct);
        var options = masters
            .Where(m => m.Status == TemplateMasterStatus.Published && m.CurrentVersionId.HasValue)
            .Select(m => new TemplateVariantOptionModel(
                m.Id,
                m.MasterCode,
                m.TemplateName,
                m.CurrentMasterVersion,
                m.CurrentVersionId,
                m.Status.ToWire(),
                m.Classification))
            .ToList();

        return Response<IReadOnlyList<TemplateVariantOptionModel>>.Success(options, correlationId: correlationId);
    }

    private async Task<IReadOnlyList<TemplateVariantListItemModel>> MapListAsync(IReadOnlyList<TemplateVariant> rows, CancellationToken ct)
    {
        var masterCache = new Dictionary<Guid, TemplateMaster?>();
        var items = new List<TemplateVariantListItemModel>(rows.Count);
        foreach (var variant in rows)
        {
            if (!masterCache.TryGetValue(variant.TemplateMasterId, out var master))
            {
                master = await _masters.GetByIdAsync(variant.TemplateMasterId, ct);
                masterCache[variant.TemplateMasterId] = master;
            }

            var drift = ComputeDrift(variant, master);
            items.Add(TemplateVariantWire.ToListItem(
                variant,
                master,
                drift,
                canCompare: true,
                canRebase: await CanRebaseVariantAsync(variant, ct)));
        }

        return items;
    }

    private async Task<bool> CanViewVariantAsync(TemplateVariant variant, CancellationToken ct)
    {
        var decision = await ResolveVariantDecisionAsync(
            DocumentAccessTargetType.TemplateVariant,
            variant.Id,
            DocumentAccessMatrixAction.View,
            ct);
        if (decision != DocumentAccessDecision.NoDecision)
        {
            return decision == DocumentAccessDecision.Allow;
        }

        if (variant.LinkedTemplateDocumentId is { } templateId)
        {
            var linked = await _templateDocuments.GetByIdAsync(templateId, ct);
            return linked is not null && (_access is null || await _access.CanReachTemplateAsync(linked, ct));
        }

        return _access is null || (variant.OwnerCompanyId is { } companyId && _access.Principal.BelongsToCompany(companyId));
    }

    private async Task<bool> CanRebaseVariantAsync(TemplateVariant variant, CancellationToken ct)
    {
        var decision = await ResolveVariantDecisionAsync(
            DocumentAccessTargetType.TemplateVariant,
            variant.Id,
            DocumentAccessMatrixAction.EditMetadata,
            ct);
        if (decision != DocumentAccessDecision.NoDecision)
        {
            return decision == DocumentAccessDecision.Allow;
        }

        if (variant.LinkedTemplateDocumentId is { } templateId)
        {
            var linked = await _templateDocuments.GetByIdAsync(templateId, ct);
            return linked is not null
                && (_access is null || await _access.HasTemplateDocumentMatrixActionAsync(
                    linked,
                    DocumentAccessMatrixAction.EditMetadata,
                    DocumentAccessAction.Edit,
                    ct));
        }

        return _access is null || (variant.OwnerCompanyId is { } companyId && _access.Principal.BelongsToCompany(companyId));
    }

    private Task<DocumentAccessDecision> ResolveVariantDecisionAsync(
        DocumentAccessTargetType targetType,
        Guid targetId,
        DocumentAccessMatrixAction action,
        CancellationToken ct) =>
        _resolver is null
            ? Task.FromResult(DocumentAccessDecision.NoDecision)
            : _resolver.ResolveCurrentDecisionAsync(targetType, targetId, action, ct);

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Guid? EmptyToNull(Guid? value) => value is null || value == Guid.Empty ? null : value;

    private async Task CompensateLinkedTemplateAsync(Guid templateId, Guid versionId)
    {
        try { await _templateVersions.DeleteAsync(versionId, CancellationToken.None); } catch { }
        try { await _templateDocuments.SoftDeleteAsync(templateId, CancellationToken.None); } catch { }
    }

    private async Task TryDeleteStoredContentAsync(ContentStoreResult? stored)
    {
        if (stored is null)
        {
            return;
        }

        try { await _versioning.TryDeleteAsync(stored, CancellationToken.None); } catch { }
    }

    private static bool HasValidContent(ContentRef? fileRef) =>
        fileRef is not null
        && fileRef.ContentId != Guid.Empty
        && !string.IsNullOrWhiteSpace(fileRef.StorageProvider)
        && !string.IsNullOrWhiteSpace(fileRef.ObjectKey)
        && !string.IsNullOrWhiteSpace(fileRef.FileName)
        && !string.IsNullOrWhiteSpace(fileRef.MediaType)
        && !string.IsNullOrWhiteSpace(fileRef.Checksum);

    private ContentRef CloneContentRef(ContentRef source, Guid versionId) => new()
    {
        ContentId = source.ContentId,
        StorageProvider = source.StorageProvider,
        ObjectKey = source.ObjectKey,
        FileName = source.FileName,
        MediaType = source.MediaType,
        ByteSize = source.ByteSize,
        Checksum = source.Checksum,
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = _currentUser.ActorName,
        VersionId = versionId
    };

    private static bool IsFolderCompatible(TemplateVariantScopeType scopeType, Guid scopeId, CollectionInstanceReferenceDto folder)
    {
        if (scopeType == TemplateVariantScopeType.Company)
        {
            return folder.CompanyId == scopeId;
        }

        var expected = scopeType == TemplateVariantScopeType.BusinessUnit ? "BUSINESS_UNIT" : "SITE";
        return folder.ScopeBindings.Any(x =>
            x.ScopeId == scopeId
            && string.Equals(NormalizeScopeBindingType(x.ScopeType), expected, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(x.BindingStatus) || string.Equals(x.BindingStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase)));
    }

    private static string NormalizeScopeBindingType(string? value) =>
        (value ?? string.Empty).Trim().Replace("-", "_", StringComparison.Ordinal).ToUpperInvariant();

    private static Response<T> NotFound<T>(string correlationId) =>
        Response<T>.Fail("Not found.", 404, TemplateVariantReasonCodes.NotFoundNonLeakage, correlationId);

    private static Response<T> PermissionDenied<T>(string correlationId) =>
        Response<T>.Fail("Permission denied.", 403, TemplateVariantReasonCodes.PermissionDenied, correlationId);

    private static Response<T> Fail<T>(string error, int status, string reason, string correlationId) =>
        Response<T>.Fail(error, status, reason, correlationId);
}
