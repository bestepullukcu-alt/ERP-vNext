using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — controlled sharing of an individual document or template (REFERENCE vs COPY_ON_ADOPT). The
/// target company is validated fail-closed via MOD-0220 (<see cref="ILegalEntityReferenceValidator"/>).
/// COPY_ON_ADOPT is feature-flag gated until copy lineage is verified. Reused by the folder/branch share flow.
/// </summary>
public sealed class TemplateSharingService
{
    private readonly IControlledDocumentRepository _documents;
    private readonly IControlledDocumentVersionRepository _documentVersions;
    private readonly ITemplateDocumentRepository _templates;
    private readonly ITemplateVersionRepository _templateVersions;
    private readonly IDocumentShareRecordRepository _shares;
    private readonly ILegalEntityReferenceValidator _legalEntityValidator;
    private readonly DocumentAccessEvaluator _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly ControlledDocumentsFeatureFlagOptions _flags;

    public TemplateSharingService(
        IControlledDocumentRepository documents,
        IControlledDocumentVersionRepository documentVersions,
        ITemplateDocumentRepository templates,
        ITemplateVersionRepository templateVersions,
        IDocumentShareRecordRepository shares,
        ILegalEntityReferenceValidator legalEntityValidator,
        DocumentAccessEvaluator access,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        IOptions<ControlledDocumentsFeatureFlagOptions> flags)
    {
        _documents = documents;
        _documentVersions = documentVersions;
        _templates = templates;
        _templateVersions = templateVersions;
        _shares = shares;
        _legalEntityValidator = legalEntityValidator;
        _access = access;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _flags = flags.Value;
    }

    public async Task<Response<ShareResultModel>> ShareDocumentAsync(
        Guid documentId, Guid targetCompanyId, DocumentShareMode shareMode, string correlationId, CancellationToken ct)
    {
        if (!_flags.TemplateSharingEnabled)
        {
            return Fail("Sharing is not enabled.", 403, ControlledDocumentReasonCodes.FeatureDisabled, correlationId);
        }

        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null || !await _access.CanReachDocumentAsync(document, ct))
        {
            return NotFound(correlationId);
        }

        if (!await _access.HasControlledDocumentActionOrOwnerDefaultAsync(
                document,
                DocumentAccessMatrixAction.Share,
                DocumentAccessAction.Share,
                ct))
        {
            return PermDenied(correlationId);
        }

        var modeResult = ResolveMode(shareMode, copyAllowed: true, correlationId);
        if (!modeResult.IsSuccessful)
        {
            return modeResult;
        }

        var targetValidation = await ValidateTargetAsync(targetCompanyId, document.OwnerCompanyId, correlationId, ct);
        if (targetValidation is not null)
        {
            return targetValidation;
        }

        Guid? copiedId = null;
        if (shareMode == DocumentShareMode.CopyOnAdopt)
        {
            copiedId = await CopyDocumentAsync(document, targetCompanyId, ct);
        }

        var share = await PersistShareAsync(SharedItemKind.ControlledDocument, documentId, document.OwnerCompanyId, targetCompanyId, shareMode, copiedId, null, correlationId, ct);
        return Response<ShareResultModel>.Success(ToModel(share), 201, correlationId);
    }

    public async Task<Response<ShareResultModel>> ShareTemplateAsync(
        Guid templateId, Guid targetCompanyId, DocumentShareMode shareMode, string correlationId, CancellationToken ct)
    {
        if (!_flags.TemplateSharingEnabled)
        {
            return Fail("Sharing is not enabled.", 403, ControlledDocumentReasonCodes.FeatureDisabled, correlationId);
        }

        var template = await _templates.GetByIdAsync(templateId, ct);
        if (template is null || !await _access.CanReachTemplateAsync(template, ct))
        {
            return NotFound(correlationId);
        }

        if (!await _access.HasTemplateDocumentActionOrOwnerDefaultAsync(
                template,
                DocumentAccessMatrixAction.Share,
                DocumentAccessAction.Share,
                ct))
        {
            return PermDenied(correlationId);
        }

        // A non-shareable / reference-only template cannot be shared/copied.
        if (!template.TemplateFlags.Shareable || template.TemplateFlags.ReferenceOnly && shareMode == DocumentShareMode.CopyOnAdopt)
        {
            return Fail("This template is not shareable in the requested mode.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        var copyAllowed = template.TemplateFlags.CopyableOnAdopt;
        var modeResult = ResolveMode(shareMode, copyAllowed, correlationId);
        if (!modeResult.IsSuccessful)
        {
            return modeResult;
        }

        var targetValidation = await ValidateTargetAsync(targetCompanyId, template.OwnerCompanyId, correlationId, ct);
        if (targetValidation is not null)
        {
            return targetValidation;
        }

        Guid? copiedId = null;
        if (shareMode == DocumentShareMode.CopyOnAdopt)
        {
            copiedId = await CopyTemplateAsync(template, targetCompanyId, ct);
        }

        var share = await PersistShareAsync(SharedItemKind.Template, templateId, template.OwnerCompanyId, targetCompanyId, shareMode, copiedId, null, correlationId, ct);
        return Response<ShareResultModel>.Success(ToModel(share), 201, correlationId);
    }

    // ── reused by FolderShareService (no Layer-1/Layer-2 re-check; caller already authorized the operation) ──

    public async Task<(FolderShareOutcomeStatus Status, Guid? CopiedId)> ShareTemplateForFolderAsync(
        TemplateDocument template, Guid targetCompanyId, DocumentShareMode shareMode, Guid operationId, string correlationId, CancellationToken ct)
    {
        if (!template.TemplateFlags.Shareable)
        {
            return (FolderShareOutcomeStatus.Skipped, null);
        }

        if (shareMode == DocumentShareMode.CopyOnAdopt && (!template.TemplateFlags.CopyableOnAdopt || !_flags.FolderShareCopyOnAdoptEnabled))
        {
            // Falls back to a REFERENCE share when copy is not permitted/enabled for this template.
            shareMode = DocumentShareMode.Reference;
        }

        if (await _shares.ExistsAsync(SharedItemKind.Template, template.Id, targetCompanyId, ct))
        {
            return (FolderShareOutcomeStatus.Skipped, null);
        }

        Guid? copiedId = null;
        if (shareMode == DocumentShareMode.CopyOnAdopt)
        {
            copiedId = await CopyTemplateAsync(template, targetCompanyId, ct);
        }

        await PersistShareAsync(SharedItemKind.Template, template.Id, template.OwnerCompanyId, targetCompanyId, shareMode, copiedId, operationId, correlationId, ct);
        return (shareMode == DocumentShareMode.CopyOnAdopt ? FolderShareOutcomeStatus.Copied : FolderShareOutcomeStatus.Shared, copiedId);
    }

    private Response<ShareResultModel> ResolveMode(DocumentShareMode shareMode, bool copyAllowed, string correlationId)
    {
        if (shareMode != DocumentShareMode.CopyOnAdopt)
        {
            return Response<ShareResultModel>.Success(null!, correlationId: correlationId);
        }

        if (!_flags.FolderShareCopyOnAdoptEnabled)
        {
            return Fail("COPY_ON_ADOPT is not enabled.", 403, ControlledDocumentReasonCodes.FeatureDisabled, correlationId);
        }

        if (!copyAllowed)
        {
            return Fail("This item cannot be copied on adopt.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        return Response<ShareResultModel>.Success(null!, correlationId: correlationId);
    }

    private async Task<Response<ShareResultModel>?> ValidateTargetAsync(Guid targetCompanyId, Guid sourceCompanyId, string correlationId, CancellationToken ct)
    {
        if (targetCompanyId == Guid.Empty || targetCompanyId == sourceCompanyId)
        {
            return Fail("Invalid share target.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        var validation = await _legalEntityValidator.ValidateAsync(targetCompanyId, ct);
        if (!validation.IsSuccessful)
        {
            // MOD-0220 fail-closed: missing / inactive / non-referenceable target → 404 non-leakage, no orphaned writes.
            return NotFound(correlationId);
        }

        return null;
    }

    private async Task<Guid> CopyDocumentAsync(ControlledDocument source, Guid targetCompanyId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var newId = Guid.NewGuid();
        var copy = new ControlledDocument
        {
            Id = newId,
            TenantId = tenantId,
            DocumentKey = $"{source.DocumentKey}|copy|{newId:N}",
            CompanyId = targetCompanyId,
            OwnerCompanyId = targetCompanyId,
            CollectionInstanceId = source.CollectionInstanceId,
            CollectionPath = source.CollectionPath,
            CanonicalId = source.CanonicalId,
            Title = source.Title,
            DocumentType = source.DocumentType,
            Description = source.Description,
            Tags = [.. source.Tags],
            Controlled = source.Controlled,
            CurrentVersionNumber = 1,
            Status = ControlledItemStatus.Active,
            AccessPolicy = new DocumentAccessPolicy { Source = AccessPolicySource.Inherited },
            CopiedFromDocumentId = source.Id,
            CreatedBy = _currentUser.ActorName
        };

        var sourceVersion = source.CurrentVersionId is { } cv ? await _documentVersions.GetByIdAsync(cv, ct) : null;
        if (sourceVersion is not null)
        {
            var versionId = Guid.NewGuid();
            copy.CurrentVersionId = versionId;
            await _documents.CreateAsync(copy, ct);
            await _documentVersions.CreateAsync(CloneDocumentVersion(sourceVersion, newId, versionId, tenantId), ct);
        }
        else
        {
            await _documents.CreateAsync(copy, ct);
        }

        return newId;
    }

    private async Task<Guid> CopyTemplateAsync(TemplateDocument source, Guid targetCompanyId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var newId = Guid.NewGuid();
        var copy = new TemplateDocument
        {
            Id = newId,
            TenantId = tenantId,
            TemplateKey = $"{source.TemplateKey}|copy|{newId:N}",
            CompanyId = targetCompanyId,
            OwnerCompanyId = targetCompanyId,
            CollectionInstanceId = source.CollectionInstanceId,
            CollectionPath = source.CollectionPath,
            CanonicalId = source.CanonicalId,
            Title = source.Title,
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

        var sourceVersion = source.CurrentVersionId is { } cv ? await _templateVersions.GetByIdAsync(cv, ct) : null;
        if (sourceVersion is not null)
        {
            var versionId = Guid.NewGuid();
            copy.CurrentVersionId = versionId;
            await _templates.CreateAsync(copy, ct);
            await _templateVersions.CreateAsync(CloneTemplateVersion(sourceVersion, newId, versionId, tenantId), ct);
        }
        else
        {
            await _templates.CreateAsync(copy, ct);
        }

        return newId;
    }

    private async Task<DocumentShareRecord> PersistShareAsync(
        SharedItemKind kind, Guid itemId, Guid sourceCompanyId, Guid targetCompanyId,
        DocumentShareMode shareMode, Guid? copiedId, Guid? operationId, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var share = new DocumentShareRecord
        {
            TenantId = tenantId,
            ShareId = Guid.NewGuid(),
            ItemKind = kind,
            ItemId = itemId,
            SourceCompanyId = sourceCompanyId,
            TargetCompanyId = targetCompanyId,
            ShareMode = shareMode,
            VisibilityScope = ShareVisibilityScope.Company,
            CanUse = true,
            CanCopy = shareMode == DocumentShareMode.CopyOnAdopt,
            SourceVisibleOnUpdate = shareMode == DocumentShareMode.Reference,
            CopiedItemId = copiedId,
            FolderShareOperationId = operationId,
            CorrelationId = correlationId,
            SharedBy = _currentUser.ActorName,
            CreatedBy = _currentUser.ActorName
        };

        await _shares.CreateAsync(share, ct);
        return share;
    }

    private static ControlledDocumentVersion CloneDocumentVersion(ControlledDocumentVersion source, Guid documentId, Guid versionId, Guid tenantId) => new()
    {
        Id = versionId,
        TenantId = tenantId,
        DocumentId = documentId,
        VersionNumber = 1,
        FileRef = CloneContentRef(source.FileRef, versionId),
        Checksum = source.Checksum,
        UploadedBy = source.UploadedBy,
        UploadedAt = DateTimeOffset.UtcNow,
        ChangeSummary = source.ChangeSummary,
        VersionStatus = DocumentVersionStatus.Active,
        CreatedBy = source.UploadedBy
    };

    private static TemplateVersion CloneTemplateVersion(TemplateVersion source, Guid templateId, Guid versionId, Guid tenantId) => new()
    {
        Id = versionId,
        TenantId = tenantId,
        TemplateId = templateId,
        VersionNumber = 1,
        FileRef = CloneContentRef(source.FileRef, versionId),
        Checksum = source.Checksum,
        UploadedBy = source.UploadedBy,
        UploadedAt = DateTimeOffset.UtcNow,
        ChangeSummary = source.ChangeSummary,
        VersionStatus = DocumentVersionStatus.Active,
        CreatedBy = source.UploadedBy
    };

    // COPY_ON_ADOPT references the same immutable stored object (no byte duplication); lineage diverges on
    // the target's subsequent independent uploads, which produce new storage objects.
    private static ContentRef CloneContentRef(ContentRef source, Guid versionId) => new()
    {
        ContentId = source.ContentId,
        StorageProvider = source.StorageProvider,
        ObjectKey = source.ObjectKey,
        FileName = source.FileName,
        MediaType = source.MediaType,
        ByteSize = source.ByteSize,
        Checksum = source.Checksum,
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = source.CreatedBy,
        VersionId = versionId
    };

    private static ShareResultModel ToModel(DocumentShareRecord s) => new(
        s.ShareId, s.ItemKind.ToWire(), s.ItemId, s.SourceCompanyId, s.TargetCompanyId, s.ShareMode.ToWire(), s.CopiedItemId, s.CorrelationId);

    private static Response<ShareResultModel> NotFound(string correlationId) =>
        Response<ShareResultModel>.Fail("Not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage, correlationId);

    private static Response<ShareResultModel> PermDenied(string correlationId) =>
        Response<ShareResultModel>.Fail("Permission denied.", 403, ControlledDocumentReasonCodes.PermissionDenied, correlationId);

    private static Response<ShareResultModel> Fail(string error, int status, string? reason, string correlationId) =>
        Response<ShareResultModel>.Fail(error, status, reason, correlationId);
}
