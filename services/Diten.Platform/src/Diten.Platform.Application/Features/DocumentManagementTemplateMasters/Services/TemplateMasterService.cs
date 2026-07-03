using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;

/// <summary>
/// MOD-0029-FU02 — corporate template master orchestration. Storage is written before metadata and compensated
/// on metadata failure; existing folder-attached TemplateDocument flows are not mutated here.
/// </summary>
public sealed class TemplateMasterService
{
    private readonly ITemplateMasterRepository _masters;
    private readonly ITemplateMasterVersionRepository _versions;
    private readonly ITemplateDocumentRepository _templateDocuments;
    private readonly DocumentVersioningService _versioning;
    private readonly DocumentAccessResolver? _resolver;
    private readonly DocumentAccessEvaluator? _access;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public TemplateMasterService(
        ITemplateMasterRepository masters,
        ITemplateMasterVersionRepository versions,
        ITemplateDocumentRepository templateDocuments,
        DocumentVersioningService versioning,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        DocumentAccessResolver? resolver = null,
        DocumentAccessEvaluator? access = null)
    {
        _masters = masters;
        _versions = versions;
        _templateDocuments = templateDocuments;
        _versioning = versioning;
        _resolver = resolver;
        _access = access;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<TemplateMasterDetailModel>> CreateAsync(CreateTemplateMasterInput input, string correlationId, CancellationToken ct)
    {
        var validation = await ValidateCreateAsync(input, correlationId, ct);
        if (!validation.IsSuccessful)
        {
            return Response<TemplateMasterDetailModel>.Fail(validation.Errors, validation.StatusCode, validation.ReasonCode, correlationId);
        }

        var masterCode = NormalizeMasterCode(input.MasterCode);
        if (await _masters.GetByMasterCodeAsync(masterCode, ct) is not null)
        {
            return Fail<TemplateMasterDetailModel>("A template master with the same code already exists.", 409, TemplateMasterReasonCodes.DuplicateMasterCode, correlationId);
        }

        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        if (!await CanCreateMasterAsync(input.OwnerCompanyId, tenantId, ct))
        {
            return PermissionDenied<TemplateMasterDetailModel>(correlationId);
        }

        var policy = TemplateMasterWire.ParseVariantPolicy(input.VariantPolicy)!.Value;
        var master = new TemplateMaster
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MasterCode = masterCode,
            TemplateName = input.TemplateName.Trim(),
            Description = TrimOrNull(input.Description),
            Classification = TemplateMasterClassifications.Normalize(input.Classification),
            CollectionDefinitionId = EmptyToNull(input.CollectionDefinitionId),
            CanonicalId = TrimOrNull(input.CanonicalId),
            VariantPolicy = policy,
            OwnerCompanyId = EmptyToNull(input.OwnerCompanyId),
            OwnerUserId = EmptyToNull(input.OwnerUserId),
            Status = TemplateMasterStatus.Draft,
            CurrentMasterVersion = 0,
            EffectiveDate = input.EffectiveDate,
            CreatedBy = _currentUser.ActorName
        };

        try
        {
            await _masters.CreateAsync(master, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return Fail<TemplateMasterDetailModel>("A template master with the same code already exists.", 409, TemplateMasterReasonCodes.DuplicateMasterCode, correlationId);
        }

        return Response<TemplateMasterDetailModel>.Success(TemplateMasterWire.ToDetail(master), 201, correlationId);
    }

    public async Task<Response<IReadOnlyList<TemplateMasterListItemModel>>> ListAsync(TemplateMasterListFilter filter, string correlationId, CancellationToken ct)
    {
        var rows = await _masters.ListAsync(filter.Status, filter.Classification, filter.CollectionDefinitionId, filter.CanonicalId, filter.VariantPolicy, ct);
        var visible = new List<TemplateMaster>();
        foreach (var master in rows)
        {
            if (await CanViewMasterAsync(master, ct))
            {
                visible.Add(master);
            }
        }

        var items = new List<TemplateMasterListItemModel>();
        foreach (var master in visible)
        {
            items.Add(TemplateMasterWire.ToListItem(
                master,
                usageCount: 0,
                variantCount: 0,
                canPublish: await HasMasterActionAsync(master, DocumentAccessMatrixAction.Publish, ct),
                canDeprecate: await HasMasterActionAsync(master, DocumentAccessMatrixAction.Archive, ct),
                canDelete: await HasMasterActionAsync(master, DocumentAccessMatrixAction.Archive, ct),
                canImpact: await CanViewMasterAsync(master, ct)));
        }

        return Response<IReadOnlyList<TemplateMasterListItemModel>>.Success(items, correlationId: correlationId);
    }

    public async Task<Response<TemplateMasterDetailModel>> GetDetailAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var master = await _masters.GetByIdAsync(id, ct);
        if (master is null)
        {
            return NotFound<TemplateMasterDetailModel>(correlationId);
        }

        if (!await CanViewMasterAsync(master, ct))
        {
            return NotFound<TemplateMasterDetailModel>(correlationId);
        }

        var usage = await CountFolderAttachedTemplatesAsync(id, ct);
        return Response<TemplateMasterDetailModel>.Success(TemplateMasterWire.ToDetail(master, usage, 0), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<TemplateMasterVersionModel>>> GetVersionsAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var master = await _masters.GetByIdAsync(id, ct);
        if (master is null)
        {
            return NotFound<IReadOnlyList<TemplateMasterVersionModel>>(correlationId);
        }

        if (!await CanViewMasterAsync(master, ct))
        {
            return NotFound<IReadOnlyList<TemplateMasterVersionModel>>(correlationId);
        }

        var versions = await _versions.GetByMasterAsync(id, ct);
        return Response<IReadOnlyList<TemplateMasterVersionModel>>.Success(
            versions.Select(TemplateMasterWire.ToVersionModel).ToList(),
            correlationId: correlationId);
    }

    // Returns the storage pointer for a version's file; the controller streams it through the content-storage
    // seam (no direct public file URL), mirroring the controlled-document / template download flow.
    public async Task<Response<DocumentDownloadResult>> DownloadVersionAsync(Guid templateMasterId, Guid versionId, string correlationId, CancellationToken ct)
    {
        var master = await _masters.GetByIdAsync(templateMasterId, ct);
        if (master is null)
        {
            return NotFound<DocumentDownloadResult>(correlationId);
        }

        if (!await HasMasterActionAsync(master, DocumentAccessMatrixAction.Download, ct))
        {
            return PermissionDenied<DocumentDownloadResult>(correlationId);
        }

        var version = await _versions.GetByIdAsync(versionId, ct);
        if (version is null || version.TemplateMasterId != templateMasterId)
        {
            return NotFound<DocumentDownloadResult>(correlationId);
        }

        return Response<DocumentDownloadResult>.Success(
            new DocumentDownloadResult(version.FileRef.StorageProvider, version.FileRef.ObjectKey, version.FileRef.FileName, version.FileRef.MediaType),
            correlationId: correlationId);
    }

    public async Task<Response<TemplateMasterVersionModel>> PublishVersionAsync(
        Guid id,
        FileUploadInput file,
        string? changeSummary,
        bool allowUnchanged,
        string correlationId,
        CancellationToken ct)
    {
        var master = await _masters.GetByIdAsync(id, ct);
        if (master is null)
        {
            return NotFound<TemplateMasterVersionModel>(correlationId);
        }

        if (!await HasMasterActionAsync(master, DocumentAccessMatrixAction.Publish, ct))
        {
            return PermissionDenied<TemplateMasterVersionModel>(correlationId);
        }

        if (master.Status is TemplateMasterStatus.Deprecated or TemplateMasterStatus.Archived)
        {
            return Fail<TemplateMasterVersionModel>("Deprecated or archived template masters cannot publish new versions.", 409, TemplateMasterReasonCodes.InvalidLifecycleTransition, correlationId);
        }

        if (!allowUnchanged && master.CurrentVersionId is { } currentVersionId)
        {
            var uploadChecksum = DocumentVersioningService.ComputeChecksum(file?.ContentBase64);
            var current = await _versions.GetByIdAsync(currentVersionId, ct);
            if (uploadChecksum is not null && current is not null && string.Equals(uploadChecksum, current.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                return Fail<TemplateMasterVersionModel>("Uploaded content is identical to the current master version.", 409, TemplateMasterReasonCodes.NoContentChange, correlationId);
            }
        }

        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var versionId = Guid.NewGuid();
        var nextNumber = await _versions.GetMaxVersionNumberAsync(id, ct) + 1;
        var storageCompanyId = master.OwnerCompanyId.GetValueOrDefault(Guid.Empty);

        var stored = await _versioning.StoreAsync(
            ContentStorageScope.Templates,
            storageCompanyId,
            id,
            versionId,
            file,
            _currentUser.ActorName,
            correlationId,
            ct);
        if (!stored.IsSuccessful)
        {
            return Response<TemplateMasterVersionModel>.Fail(
                stored.Errors,
                stored.StatusCode == 0 ? 503 : stored.StatusCode,
                stored.ReasonCode ?? TemplateMasterReasonCodes.StorageUnavailable,
                correlationId);
        }

        var version = new TemplateMasterVersion
        {
            Id = versionId,
            TenantId = tenantId,
            TemplateMasterId = id,
            VersionNumber = nextNumber,
            FileRef = DocumentVersioningService.ToContentRef(stored.Data!, versionId, _currentUser.ActorName),
            Checksum = stored.Data!.Checksum,
            Status = TemplateMasterVersionStatus.Published,
            PublishedAt = DateTimeOffset.UtcNow,
            PublishedBy = _currentUser.ActorName,
            ChangeSummary = TrimOrNull(changeSummary),
            CreatedBy = _currentUser.ActorName
        };

        try
        {
            if (await _versions.GetByMasterAndNumberAsync(id, nextNumber, ct) is not null)
            {
                await _versioning.TryDeleteAsync(stored.Data!, CancellationToken.None);
                return Fail<TemplateMasterVersionModel>("Duplicate template master version.", 409, TemplateMasterReasonCodes.DuplicateVersion, correlationId);
            }

            await _versions.CreateAsync(version, ct);
            await _versions.SupersedePublishedVersionsAsync(id, versionId, ct);

            master.Status = TemplateMasterStatus.Published;
            master.CurrentVersionId = versionId;
            master.CurrentMasterVersion = nextNumber;
            master.UpdatedAt = DateTimeOffset.UtcNow;
            master.UpdatedBy = _currentUser.ActorName;
            await _masters.UpdateAsync(master, ct);
        }
        catch
        {
            await _versioning.TryDeleteAsync(stored.Data!, CancellationToken.None);
            return Fail<TemplateMasterVersionModel>("Could not persist the template master version.", 409, TemplateMasterReasonCodes.Conflict, correlationId);
        }

        return Response<TemplateMasterVersionModel>.Success(TemplateMasterWire.ToVersionModel(version), 201, correlationId);
    }

    public async Task<Response<TemplateMasterDetailModel>> DeprecateAsync(Guid id, DeprecateTemplateMasterInput input, string correlationId, CancellationToken ct)
    {
        var master = await _masters.GetByIdAsync(id, ct);
        if (master is null)
        {
            return NotFound<TemplateMasterDetailModel>(correlationId);
        }

        if (!await HasMasterActionAsync(master, DocumentAccessMatrixAction.Archive, ct))
        {
            return PermissionDenied<TemplateMasterDetailModel>(correlationId);
        }

        if (master.Status != TemplateMasterStatus.Published)
        {
            return Fail<TemplateMasterDetailModel>("Only published template masters can be deprecated.", 409, TemplateMasterReasonCodes.InvalidLifecycleTransition, correlationId);
        }

        master.Status = TemplateMasterStatus.Deprecated;
        master.DeprecatedAt = DateTimeOffset.UtcNow;
        master.DeprecatedBy = _currentUser.ActorName;
        master.UpdatedAt = DateTimeOffset.UtcNow;
        master.UpdatedBy = _currentUser.ActorName;
        await _masters.UpdateAsync(master, ct);

        var usage = await CountFolderAttachedTemplatesAsync(id, ct);
        return Response<TemplateMasterDetailModel>.Success(TemplateMasterWire.ToDetail(master, usage, 0), correlationId: correlationId);
    }

    public async Task<Response<TemplateMasterAdoptionImpactModel>> GetAdoptionImpactAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var master = await _masters.GetByIdAsync(id, ct);
        if (master is null)
        {
            return NotFound<TemplateMasterAdoptionImpactModel>(correlationId);
        }

        if (!await CanViewMasterAsync(master, ct))
        {
            return NotFound<TemplateMasterAdoptionImpactModel>(correlationId);
        }

        var folderTemplateCount = await CountFolderAttachedTemplatesAsync(id, ct);
        var model = new TemplateMasterAdoptionImpactModel(
            id,
            folderTemplateCount,
            0,
            folderTemplateCount,
            "Basic usage summary only; full adoption impact analysis is deferred.");
        return Response<TemplateMasterAdoptionImpactModel>.Success(model, correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<TemplateMasterOptionModel>>> GetOptionsAsync(string correlationId, CancellationToken ct)
    {
        var masters = await _masters.ListAsync("Published", null, null, null, null, ct);
        var options = new List<TemplateMasterOptionModel>();
        foreach (var master in masters.Where(m => m.Status == TemplateMasterStatus.Published && m.CurrentVersionId.HasValue))
        {
            if (!await CanViewMasterAsync(master, ct))
            {
                continue;
            }

            options.Add(new TemplateMasterOptionModel(
                master.Id,
                master.MasterCode,
                master.TemplateName,
                master.CurrentMasterVersion,
                master.CurrentVersionId,
                master.Classification,
                master.VariantPolicy.ToWire()));
        }

        return Response<IReadOnlyList<TemplateMasterOptionModel>>.Success(options, correlationId: correlationId);
    }

    public async Task<Response<NoContent>> DeleteAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var master = await _masters.GetByIdAsync(id, ct);
        if (master is null)
        {
            return NotFound<NoContent>(correlationId);
        }

        if (!await HasMasterActionAsync(master, DocumentAccessMatrixAction.Archive, ct))
        {
            return PermissionDenied<NoContent>(correlationId);
        }

        await _masters.SoftDeleteAsync(id, ct);
        return Response<NoContent>.Success(correlationId: correlationId);
    }

    public async Task<Response<int>> BulkDeleteAsync(IReadOnlyList<Guid> ids, string correlationId, CancellationToken ct)
    {
        var distinctIds = (ids ?? [])
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctIds.Count == 0)
        {
            return Fail<int>("No identifiers provided for bulk deletion.", 400, TemplateMasterReasonCodes.ValidationFailed, correlationId);
        }

        var allowedIds = new List<Guid>();
        foreach (var id in distinctIds)
        {
            var master = await _masters.GetByIdAsync(id, ct);
            if (master is not null && await HasMasterActionAsync(master, DocumentAccessMatrixAction.Archive, ct))
            {
                allowedIds.Add(id);
            }
        }

        if (allowedIds.Count != distinctIds.Count)
        {
            return PermissionDenied<int>(correlationId);
        }

        var deletedCount = await _masters.BulkSoftDeleteAsync(allowedIds, ct);
        return Response<int>.Success(deletedCount, 200, correlationId);
    }

    private async Task<Response<NoContent>> ValidateCreateAsync(CreateTemplateMasterInput input, string correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.MasterCode) || string.IsNullOrWhiteSpace(input.TemplateName))
        {
            return Response<NoContent>.Fail("MasterCode and TemplateName are required.", 400, TemplateMasterReasonCodes.ValidationFailed, correlationId);
        }

        if (!TemplateMasterClassifications.IsAllowed(input.Classification))
        {
            return Response<NoContent>.Fail("Classification is not recognized.", 400, TemplateMasterReasonCodes.ValidationFailed, correlationId);
        }

        if (TemplateMasterWire.ParseVariantPolicy(input.VariantPolicy) is null)
        {
            return Response<NoContent>.Fail("VariantPolicy is not recognized.", 400, TemplateMasterReasonCodes.ValidationFailed, correlationId);
        }

        return Response<NoContent>.Success(correlationId: correlationId);
    }

    private async Task<int> CountFolderAttachedTemplatesAsync(Guid templateMasterId, CancellationToken ct)
    {
        var templates = await _templateDocuments.GetAllForTenantAsync(ct);
        return templates.Count(t => t.TemplateMasterId == templateMasterId);
    }

    private async Task<bool> CanCreateMasterAsync(Guid? ownerCompanyId, Guid tenantId, CancellationToken ct)
    {
        var targetType = ownerCompanyId is { } companyId && companyId != Guid.Empty
            ? DocumentAccessTargetType.Company
            : DocumentAccessTargetType.Tenant;
        var targetId = ownerCompanyId is { } owner && owner != Guid.Empty ? owner : tenantId;
        var decision = await ResolveMasterDecisionAsync(targetType, targetId, DocumentAccessMatrixAction.CreateTemplate, ct);
        if (decision != DocumentAccessDecision.NoDecision)
        {
            return decision == DocumentAccessDecision.Allow;
        }

        return _access is null
            || ownerCompanyId is null
            || ownerCompanyId == Guid.Empty
            || _access.Principal.BelongsToCompany(ownerCompanyId.Value);
    }

    private async Task<bool> CanViewMasterAsync(TemplateMaster master, CancellationToken ct)
    {
        var decision = await ResolveMasterDecisionAsync(
            DocumentAccessTargetType.TemplateMaster,
            master.Id,
            DocumentAccessMatrixAction.View,
            ct);
        if (decision != DocumentAccessDecision.NoDecision)
        {
            return decision == DocumentAccessDecision.Allow;
        }

        return master.OwnerCompanyId is null
            || master.OwnerCompanyId == Guid.Empty
            || _access is null
            || _access.Principal.BelongsToCompany(master.OwnerCompanyId.Value);
    }

    private async Task<bool> HasMasterActionAsync(TemplateMaster master, DocumentAccessMatrixAction action, CancellationToken ct)
    {
        var decision = await ResolveMasterDecisionAsync(DocumentAccessTargetType.TemplateMaster, master.Id, action, ct);
        if (decision != DocumentAccessDecision.NoDecision)
        {
            return decision == DocumentAccessDecision.Allow;
        }

        return master.OwnerCompanyId is null
            || master.OwnerCompanyId == Guid.Empty
            || _access is null
            || _access.Principal.BelongsToCompany(master.OwnerCompanyId.Value);
    }

    private Task<DocumentAccessDecision> ResolveMasterDecisionAsync(
        DocumentAccessTargetType targetType,
        Guid targetId,
        DocumentAccessMatrixAction action,
        CancellationToken ct) =>
        _resolver is null
            ? Task.FromResult(DocumentAccessDecision.NoDecision)
            : _resolver.ResolveCurrentDecisionAsync(targetType, targetId, action, ct);

    private static string NormalizeMasterCode(string value) => value.Trim().ToUpperInvariant();
    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Guid? EmptyToNull(Guid? value) => value is null || value == Guid.Empty ? null : value;

    private static Response<T> NotFound<T>(string correlationId) =>
        Response<T>.Fail("Not found.", 404, TemplateMasterReasonCodes.NotFoundNonLeakage, correlationId);

    private static Response<T> PermissionDenied<T>(string correlationId) =>
        Response<T>.Fail("Permission denied.", 403, TemplateMasterReasonCodes.PermissionDenied, correlationId);

    private static Response<T> Fail<T>(string error, int status, string reason, string correlationId) =>
        Response<T>.Fail(error, status, reason, correlationId);
}
