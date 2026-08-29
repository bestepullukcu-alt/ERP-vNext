using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.DocumentManagementApproval;
using Diten.Platform.Application.Features.DocumentManagementApproval.Services;
using Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers.Services;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Services;

/// <summary>
/// MOD-0029-FU36 — durable, idempotent registration orchestration. No Mongo transaction is assumed.
/// The operation stores a metadata-only retry snapshot and storage pointer; file bytes are never persisted here.
/// </summary>
public sealed class ControlledDocumentRegistrationService
{
    private readonly IControlledDocumentRegistrationRepository _operations;
    private readonly IControlledDocumentRepository _documents;
    private readonly IControlledDocumentVersionRepository _versions;
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly ICollectionInstanceReferenceReader _folders;
    private readonly DocumentAccessEvaluator _access;
    private readonly DocumentVersioningService _versioning;
    private readonly DocumentKeyFactory _keyFactory;
    private readonly CorporateCollectionStoragePartitionBuilder _partitions;
    private readonly CorporateCollectionFolderAccessEvaluator _corporateAccess;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly DocumentApprovalService? _approval;
    private readonly DocumentIdentifierAllocationService? _identifiers;
    private readonly IDocumentVariantRepository? _documentVariants;

    public ControlledDocumentRegistrationService(
        IControlledDocumentRegistrationRepository operations,
        IControlledDocumentRepository documents,
        IControlledDocumentVersionRepository versions,
        IDocumentMasterRegisterRepository register,
        ICollectionInstanceReferenceReader folders,
        DocumentAccessEvaluator access,
        DocumentVersioningService versioning,
        DocumentKeyFactory keyFactory,
        CorporateCollectionStoragePartitionBuilder partitions,
        CorporateCollectionFolderAccessEvaluator corporateAccess,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        DocumentApprovalService? approval = null,
        DocumentIdentifierAllocationService? identifiers = null,
        IDocumentVariantRepository? documentVariants = null)
    {
        _operations = operations;
        _documents = documents;
        _versions = versions;
        _register = register;
        _folders = folders;
        _access = access;
        _versioning = versioning;
        _keyFactory = keyFactory;
        _partitions = partitions;
        _corporateAccess = corporateAccess;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _approval = approval;
        _identifiers = identifiers;
        _documentVariants = documentVariants;
    }

    public async Task<Response<ControlledDocumentRegistrationResultModel>> CreateAsync(
        CreateControlledDocumentRegistrationInput input,
        string correlationId,
        CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var authorUserId = EmptyToNull(input.AuthorUserId);
        if (authorUserId is null)
        {
            return Response<ControlledDocumentRegistrationResultModel>.Fail(
                "A document author is required.", 400,
                ControlledDocumentRegistrationReasonCodes.ValidationFailed, correlationId);
        }
        var key = input.IdempotencyKey.Trim();
        var folderId = input.FolderId == Guid.Empty ? input.CollectionInstanceId : input.FolderId;
        var folder = await _folders.ResolveByIdAsync(folderId, ct);
        if (folder is null || folder.CollectionInstanceId != input.CollectionInstanceId && input.FolderId != Guid.Empty)
        {
            return NotFound<ControlledDocumentRegistrationResultModel>(correlationId);
        }
        var scopeOwnerId = input.DocumentScope == DocumentScope.Company ? input.CompanyId : input.CorporateOwnerId;
        var storagePartition = input.DocumentScope == DocumentScope.Company
            ? _partitions.ForCompany(input.CompanyId, folderId)
            : _partitions.ForCorporate(input.CorporateOwnerId, folderId);
        var languageId = (input.GoverningLanguageId ?? input.GoverningLanguage).Trim();
        var retentionId = (input.RetentionClassId ?? input.RetentionClass ?? string.Empty).Trim();
        var fingerprint = ScopeFingerprint(input, folderId, storagePartition, languageId, retentionId);
        var operation = await _operations.GetByIdempotencyKeyAsync(key, ct);
        if (operation is null)
        {
            operation = new ControlledDocumentRegistrationOperation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                IdempotencyKey = key,
                CorrelationId = correlationId,
                CreatedBy = _currentUser.ActorName
            };
            operation.CaptureScopeSnapshot(
                input.DocumentScope,
                scopeOwnerId,
                input.DocumentScope == DocumentScope.Company ? input.CompanyId : Guid.Empty,
                input.DocumentScope == DocumentScope.Company ? input.OwnerCompanyId : Guid.Empty,
                input.DocumentScope == DocumentScope.Corporate ? input.CorporateOwnerId : Guid.Empty,
                input.CollectionInstanceId,
                folderId,
                storagePartition,
                input.DocumentScope == DocumentScope.Corporate ? folder.BaselineReleaseId : null,
                folder.ProvisioningOperationId?.ToString("D"),
                languageId,
                retentionId,
                TrimOrNull(input.OwnerFunction),
                TrimOrNull(input.ProcessOwnerRole),
                EmptyToNull(input.ProcessOwnerUserId),
                fingerprint,
                _currentUser.ActorName);
            operation.CaptureRegistrationMetadata(JsonSerializer.Serialize(ToSnapshot(input)), _currentUser.ActorName);
            await _operations.AddAsync(operation, ct);
        }
        else if (!operation.CaptureScopeSnapshot(
                     input.DocumentScope, scopeOwnerId,
                     input.DocumentScope == DocumentScope.Company ? input.CompanyId : Guid.Empty,
                     input.DocumentScope == DocumentScope.Company ? input.OwnerCompanyId : Guid.Empty,
                     input.DocumentScope == DocumentScope.Corporate ? input.CorporateOwnerId : Guid.Empty,
                     input.CollectionInstanceId, folderId, storagePartition,
                     input.DocumentScope == DocumentScope.Corporate ? folder.BaselineReleaseId : null,
                     folder.ProvisioningOperationId?.ToString("D"), languageId, retentionId,
                     TrimOrNull(input.OwnerFunction), TrimOrNull(input.ProcessOwnerRole),
                     EmptyToNull(input.ProcessOwnerUserId), fingerprint, _currentUser.ActorName))
        {
            return Response<ControlledDocumentRegistrationResultModel>.Fail(
                "The idempotency key is already bound to another immutable registration scope.", 409,
                ControlledDocumentRegistrationReasonCodes.ValidationFailed, correlationId);
        }
        else if (operation.Status == ControlledDocumentRegistrationStatus.Completed)
        {
            return Completed(operation, correlationId);
        }
        else
        {
            operation.StartAttempt(_currentUser.ActorName);
            await _operations.UpdateAsync(operation, ct);
        }

        return await ExecuteAsync(operation, input, correlationId, ct);
    }

    public async Task<Response<RetryControlledDocumentRegistrationResultModel>> RetryAsync(
        Guid operationId,
        string correlationId,
        CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var operation = await _operations.GetByIdAsync(operationId, ct);
        if (operation is null)
        {
            return Response<RetryControlledDocumentRegistrationResultModel>.Fail(
                "Registration operation not found.", 404,
                ControlledDocumentRegistrationReasonCodes.NotFoundNonLeakage, correlationId);
        }

        if (operation.Status == ControlledDocumentRegistrationStatus.Completed)
        {
            return Response<RetryControlledDocumentRegistrationResultModel>.Success(
                new RetryControlledDocumentRegistrationResultModel(
                    ControlledDocumentRegistrationMapping.ToModel(operation), false, true),
                correlationId: correlationId);
        }

        if (string.IsNullOrWhiteSpace(operation.RegistrationMetadataJson))
        {
            return Response<RetryControlledDocumentRegistrationResultModel>.Fail(
                "Registration metadata is unavailable for retry.", 409,
                ControlledDocumentRegistrationReasonCodes.RegistrationFailed, correlationId);
        }

        var snapshot = JsonSerializer.Deserialize<RegistrationSnapshot>(operation.RegistrationMetadataJson);
        if (snapshot is null)
        {
            return Response<RetryControlledDocumentRegistrationResultModel>.Fail(
                "Registration metadata is invalid.", 409,
                ControlledDocumentRegistrationReasonCodes.RegistrationFailed, correlationId);
        }

        if (operation.ContentRef is null)
        {
            return Response<RetryControlledDocumentRegistrationResultModel>.Fail(
                "The initial file must be resubmitted with the original idempotency key.", 409,
                ControlledDocumentRegistrationReasonCodes.StorageFailed, correlationId);
        }

        operation.StartAttempt(_currentUser.ActorName);
        await _operations.UpdateAsync(operation, ct);
        var result = await ExecuteAsync(operation, FromSnapshot(snapshot), correlationId, ct);
        return result.IsSuccessful
            ? Response<RetryControlledDocumentRegistrationResultModel>.Success(
                new RetryControlledDocumentRegistrationResultModel(
                    ControlledDocumentRegistrationMapping.ToModel(operation), true, true),
                correlationId: correlationId)
            : Response<RetryControlledDocumentRegistrationResultModel>.Fail(
                result.Errors, result.StatusCode, result.ReasonCode, correlationId);
    }

    public async Task<Response<ControlledDocumentRegistrationOperationModel>> GetOperationAsync(
        Guid operationId,
        string correlationId,
        CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var operation = await _operations.GetByIdAsync(operationId, ct);
        return operation is null
            ? Response<ControlledDocumentRegistrationOperationModel>.Fail(
                "Registration operation not found.", 404,
                ControlledDocumentRegistrationReasonCodes.NotFoundNonLeakage, correlationId)
            : Response<ControlledDocumentRegistrationOperationModel>.Success(
                ControlledDocumentRegistrationMapping.ToModel(operation), correlationId: correlationId);
    }

    public async Task<Response<MasterRegisterByControlledDocumentModel>> GetMasterRegisterAsync(
        Guid controlledDocumentId,
        string correlationId,
        CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var document = await _documents.GetByIdAsync(controlledDocumentId, ct);
        if (document is null || !await _access.CanReachDocumentAsync(document, ct))
        {
            return NotFound<MasterRegisterByControlledDocumentModel>(correlationId);
        }

        var entry = await _register.GetByControlledDocumentIdAsync(controlledDocumentId, ct);
        return entry is null
            ? NotFound<MasterRegisterByControlledDocumentModel>(correlationId)
            : Response<MasterRegisterByControlledDocumentModel>.Success(
                new MasterRegisterByControlledDocumentModel(
                    controlledDocumentId, entry.Id, entry.DocumentTitle,
                    entry.RegisterStatus.ToString(), entry.LifecycleStatus.ToString(),
                    entry.DocumentScope.ToString(), entry.ScopeOwnerId, entry.OwnerCompanyId,
                    entry.CorporateOwnerId, entry.DocumentClass.ToString(), entry.DocumentType.ToString(),
                    entry.LinkScopeCompatibilityStatus.ToString(), entry.ControlledDocumentLinkedAt,
                    entry.ControlledDocumentLinkedBy, entry.ControlledDocumentLinkReason),
                correlationId: correlationId);
    }

    private async Task<Response<ControlledDocumentRegistrationResultModel>> ExecuteAsync(
        ControlledDocumentRegistrationOperation operation,
        CreateControlledDocumentRegistrationInput input,
        string correlationId,
        CancellationToken ct)
    {
        ContentStoreResult? storedThisAttempt = null;
        try
        {
            var authorUserId = EmptyToNull(input.AuthorUserId);
            if (authorUserId is null)
            {
                return await FailAsync(operation, "A document author is required.", 400,
                    ControlledDocumentRegistrationReasonCodes.ValidationFailed, false, correlationId, ct);
            }

            // A Record stores a file + register entry but is NOT a controlled document: no lifecycle / approval /
            // release gate / identifier applies (SOP §2). The file-storage machinery below is reused unchanged.
            var isRecord = input.Kind == RegistrationKind.Record;
            // A Variant IS a controlled document (governance applies) but is derived from a parent and its content must
            // differ from that parent (checked after the file is stored).
            var isVariant = input.Kind == RegistrationKind.Variant;
            var documentType = ControlledDocumentWire.ParseDocumentType(input.DocumentType);
            if ((int)documentType < 0)
            {
                return await FailAsync(operation, "Unsupported document type.", 400,
                    ControlledDocumentRegistrationReasonCodes.ValidationFailed, false, correlationId, ct);
            }
            if (documentType == DocumentType.Template)
            {
                return await FailAsync(operation, "Templates must use the dedicated template flow.", 400,
                    ControlledDocumentRegistrationReasonCodes.TemplateFlowRequired, false, correlationId, ct);
            }

            var documentClass = MasterRegisterWire.ParseClass(input.DocumentClass);
            var criticality = MasterRegisterWire.ParseCriticality(input.Criticality);
            if (documentClass is null || criticality is null)
            {
                return await FailAsync(operation, "Document class or criticality is invalid.", 400,
                    ControlledDocumentRegistrationReasonCodes.ValidationFailed, false, correlationId, ct);
            }

            // Record code (controlled documents are engine-allocated, so this is ignored for them). A user-entered code
            // is honoured and duplicate-guarded; if left blank, a system default is generated in the same governed
            // Document-Code format (the user can still override it). Guard runs early — before any content is stored —
            // so a manual clash fails fast with nothing to compensate.
            var manualRecordCode = isRecord ? TrimOrNull(input.RecordCode) : null;
            if (manualRecordCode is not null && await _register.GetByDocumentCodeAsync(manualRecordCode, ct) is not null)
            {
                return await FailAsync(operation, "A register entry with the same code already exists.", 409,
                    ControlledDocumentRegistrationReasonCodes.DuplicateRecordCode, false, correlationId, ct);
            }
            var recordCode = manualRecordCode;
            if (isRecord && recordCode is null && _identifiers is not null)
            {
                recordCode = await _identifiers.GenerateRecordCodeAsync(documentClass.Value, documentType, ct);
            }

            // Variant: resolve the parent controlled document + its current-version checksum up front (fail fast).
            DocumentMasterRegisterEntry? variantParent = null;
            string? parentChecksum = null;
            if (isVariant)
            {
                if (input.ParentRegisterEntryId is not { } parentId || parentId == Guid.Empty)
                {
                    return await FailAsync(operation, "A parent register entry is required for a variant.", 400,
                        ControlledDocumentRegistrationReasonCodes.ValidationFailed, false, correlationId, ct);
                }
                variantParent = await _register.GetByIdAsync(parentId, ct);
                if (variantParent is null || !variantParent.IsControlledDocument)
                {
                    return await FailAsync(operation, "Parent controlled document not found.", 404,
                        ControlledDocumentRegistrationReasonCodes.VariantParentNotFound, false, correlationId, ct);
                }
                if (variantParent.ControlledDocumentId is { } parentDocId
                    && await _documents.GetByIdAsync(parentDocId, ct) is { CurrentVersionId: { } pv })
                {
                    parentChecksum = (await _versions.GetByIdAsync(pv, ct))?.Checksum;
                }
            }

            var folderId = input.FolderId == Guid.Empty ? input.CollectionInstanceId : input.FolderId;
            var folder = await _folders.ResolveByIdAsync(folderId, ct);
            var isCompany = input.DocumentScope == DocumentScope.Company;
            var expectedScope = isCompany ? "COMPANY" : "CORPORATE";
            var expectedOwner = isCompany ? input.CompanyId : input.CorporateOwnerId;
            // Pre-FU37 Company instances do not have ScopeOwnerId populated. CompanyId remains the
            // authoritative legacy owner in that case; Corporate instances must always carry the
            // explicit scope owner introduced by FU37.
            var ownerAligned = IsScopeOwnerAligned(folder, input.DocumentScope, expectedOwner, input.CompanyId);
            var aligned = folder is not null
                && string.Equals(folder.CollectionScopeType, expectedScope, StringComparison.OrdinalIgnoreCase)
                && ownerAligned
                && (isCompany ? folder.CompanyId == input.CompanyId : folder.CompanyId == Guid.Empty)
                && (input.FolderId == Guid.Empty || folder.CollectionInstanceId == input.CollectionInstanceId);
            if (!aligned)
            {
                return await FailAsync(operation, "Not found.", 404,
                    ControlledDocumentRegistrationReasonCodes.NotFoundNonLeakage, false, correlationId, ct);
            }
            var resolvedFolder = folder!;
            if (!resolvedFolder.IsUsable)
            {
                return await FailAsync(operation, "The target folder is not active.", 409,
                    ControlledDocumentRegistrationReasonCodes.ValidationFailed, false, correlationId, ct);
            }
            var hasAccess = isCompany
                ? await _access.HasFolderCreateDocumentAsync(folderId, ct)
                : await _corporateAccess.HasExplicitGrantAsync(
                    folderId, DocumentAccessMatrixAction.CreateDocument, ct);
            if (!hasAccess)
            {
                return await FailAsync(operation, "Permission denied.", 403,
                    ControlledDocumentRegistrationReasonCodes.PermissionDenied, false, correlationId, ct);
            }

            var tenantId = TenantGuard.RequireTenant(_tenantContext);
            ContentDescriptor descriptor;
            if (operation.ContentRef is null)
            {
                var documentId = operation.ControlledDocumentId ?? Guid.NewGuid();
                var versionId = operation.ControlledDocumentVersionId ?? Guid.NewGuid();
                var stored = await _versioning.StoreAsync(
                    ContentStorageScope.Documents,
                    isCompany ? resolvedFolder.CompanyId : Guid.Empty,
                    documentId,
                    versionId,
                    input.InitialFile,
                    _currentUser.ActorName,
                    correlationId,
                    ct,
                    operation.StoragePartition);
                if (!stored.IsSuccessful)
                {
                    return await FailAsync(operation, "Initial content storage failed.", stored.StatusCode,
                        stored.ReasonCode ?? ControlledDocumentRegistrationReasonCodes.StorageFailed, false, correlationId, ct);
                }

                storedThisAttempt = stored.Data!;
                descriptor = ContentDescriptor.From(stored.Data!, documentId, versionId);
                operation.MarkContentStored(
                    $"{stored.Data!.StorageProvider}:{stored.Data.ObjectKey}",
                    stored.Data.Checksum,
                    JsonSerializer.Serialize(descriptor),
                    _currentUser.ActorName);
                await _operations.UpdateAsync(operation, ct);
            }
            else
            {
                descriptor = JsonSerializer.Deserialize<ContentDescriptor>(operation.ContentDescriptorJson ?? string.Empty)
                    ?? throw new InvalidOperationException("Stored content descriptor is unavailable.");
            }

            // A variant's content MUST differ from its parent — an identical checksum is rejected (and the just-stored
            // blob cleaned up). Only enforced when the parent's checksum is known.
            if (isVariant && parentChecksum is not null
                && string.Equals(descriptor.Checksum, parentChecksum, StringComparison.OrdinalIgnoreCase))
            {
                await CleanupAfterFailureAsync(operation, storedThisAttempt, ct);
                return await FailAsync(operation, "A variant's content must differ from the parent document.", 409,
                    ControlledDocumentRegistrationReasonCodes.VariantContentUnchanged, false, correlationId, ct);
            }

            if (operation.ControlledDocumentId is null)
            {
                var document = new ControlledDocument
                {
                    Id = descriptor.DocumentId,
                    TenantId = tenantId,
                    DocumentKey = _keyFactory.ForDocument(
                        tenantId, expectedOwner, resolvedFolder.CollectionInstanceId, input.DocumentTitle),
                    DocumentScope = input.DocumentScope,
                    ScopeOwnerId = expectedOwner,
                    CorporateOwnerId = isCompany ? Guid.Empty : input.CorporateOwnerId,
                    CompanyId = isCompany ? resolvedFolder.CompanyId : Guid.Empty,
                    OwnerCompanyId = isCompany ? input.OwnerCompanyId : Guid.Empty,
                    CollectionInstanceId = resolvedFolder.CollectionInstanceId,
                    FolderId = folderId,
                    StoragePartition = operation.StoragePartition,
                    GovernanceOwnerFunction = TrimOrNull(input.OwnerFunction),
                    GovernanceOwnerRole = TrimOrNull(input.ProcessOwnerRole),
                    GovernanceOwnerUserId = EmptyToNull(input.ProcessOwnerUserId),
                    CollectionPath = resolvedFolder.FullPath,
                    CanonicalId = resolvedFolder.CanonicalId,
                    Title = input.DocumentTitle.Trim(),
                    DocumentType = documentType,
                    Description = TrimOrNull(input.Description),
                    Tags = NormalizeTags(input.Tags),
                    Controlled = !isRecord,
                    EffectiveDate = null,
                    ReviewDate = null,
                    ExpiryDate = null,
                    CurrentVersionId = descriptor.VersionId,
                    CurrentVersionNumber = 1,
                    Status = ControlledItemStatus.Active,
                    AccessPolicy = new DocumentAccessPolicy(),
                    CreatedBy = _currentUser.ActorName
                };
                var version = new ControlledDocumentVersion
                {
                    Id = descriptor.VersionId,
                    TenantId = tenantId,
                    DocumentId = descriptor.DocumentId,
                    VersionNumber = 1,
                    FileRef = descriptor.ToContentRef(_currentUser.ActorName),
                    Checksum = descriptor.Checksum,
                    UploadedBy = _currentUser.ActorName,
                    VersionStatus = DocumentVersionStatus.Active,
                    CreatedBy = _currentUser.ActorName
                };
                await _documents.CreateAsync(document, ct);
                await _versions.CreateAsync(version, ct);
                operation.MarkDocumentCreated(document.Id, version.Id, _currentUser.ActorName);
                await _operations.UpdateAsync(operation, ct);
            }

            if (operation.MasterRegisterEntryId is null)
            {
                var entry = new DocumentMasterRegisterEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DocumentTitle = input.DocumentTitle.Trim(),
                    DocumentType = documentType,
                    DocumentClass = documentClass.Value,
                    Criticality = criticality.Value,
                    ProcessOwnerRole = TrimOrNull(input.ProcessOwnerRole),
                    ProcessOwnerUserId = EmptyToNull(input.ProcessOwnerUserId),
                    // Author is supplied independently from the governance owner and is never inferred from it.
                    AuthorUserId = authorUserId,
                    OwnerFunction = TrimOrNull(input.OwnerFunction),
                    OwnerCompanyId = isCompany ? input.OwnerCompanyId : null,
                    DocumentScope = input.DocumentScope,
                    ScopeOwnerId = expectedOwner,
                    CorporateOwnerId = isCompany ? Guid.Empty : input.CorporateOwnerId,
                    CollectionInstanceId = resolvedFolder.CollectionInstanceId,
                    FolderId = folderId,
                    LinkScopeCompatibilityStatus = DocumentLinkScopeCompatibilityStatus.Compatible,
                    GoverningLanguage = (input.GoverningLanguageId ?? input.GoverningLanguage).Trim(),
                    ReviewCycleMonths = input.ReviewCycleMonths,
                    RetentionClass = TrimOrNull(input.RetentionClassId ?? input.RetentionClass),
                    IsControlledDocument = !isRecord,
                    IsRecord = isRecord,
                    IsExternalDocument = false,
                    IsTemplate = false,
                    IsVariant = isVariant,
                    ParentDocumentUid = isVariant ? variantParent!.PermanentUid : null,
                    ParentDocumentCode = isVariant ? variantParent!.DocumentCode : null,
                    PermanentUid = null,
                    DocumentCode = recordCode,
                    IsSystemAllocated = false,
                    LifecycleStatus = ControlledDocumentLifecycleStatus.Draft,
                    RegisterStatus = DocumentRegisterStatus.Draft,
                    CorrelationId = correlationId,
                    CreatedBy = _currentUser.ActorName
                };
                await _register.CreateAsync(entry, ct);
                operation.MarkRegisterCreated(entry.Id, _currentUser.ActorName);
                await _operations.UpdateAsync(operation, ct);
            }

            var linkedDocument = await _documents.GetByIdAsync(operation.ControlledDocumentId!.Value, ct);
            if (linkedDocument is null)
            {
                return await FailAsync(operation, "Controlled document not found.", 404,
                    ControlledDocumentRegistrationReasonCodes.NotFoundNonLeakage, true, correlationId, ct);
            }
            if (linkedDocument.Status == ControlledItemStatus.Archived)
            {
                linkedDocument.Status = ControlledItemStatus.Active;
                linkedDocument.UpdatedAt = DateTimeOffset.UtcNow;
                linkedDocument.UpdatedBy = _currentUser.ActorName;
                await _documents.UpdateAsync(linkedDocument, ct);
            }

            var register = await _register.GetByIdAsync(operation.MasterRegisterEntryId!.Value, ct);
            if (register is null)
            {
                return await FailAsync(operation, "Register entry not found.", 404,
                    ControlledDocumentRegistrationReasonCodes.NotFoundNonLeakage, true, correlationId, ct);
            }
            if (register.ControlledDocumentId is { } existing && existing != operation.ControlledDocumentId)
            {
                return await FailAsync(operation, "Register entry is already linked.", 409,
                    ControlledDocumentRegistrationReasonCodes.AlreadyLinked, true, correlationId, ct);
            }

            register.ControlledDocumentId = operation.ControlledDocumentId;
            register.LinkScopeCompatibilityStatus = DocumentLinkScopeCompatibilityStatus.Compatible;
            register.ControlledDocumentLinkedAt = DateTimeOffset.UtcNow;
            register.ControlledDocumentLinkedBy = _currentUser.ActorName;
            register.ControlledDocumentLinkReason = "UNIFIED_REGISTRATION";
            // A record has no approval/lifecycle workflow — it is a completed record, effective the moment it is filed.
            // A controlled document starts in Draft and is advanced by the FU08 lifecycle engine.
            register.RegisterStatus = isRecord ? DocumentRegisterStatus.Active : DocumentRegisterStatus.Draft;
            register.LifecycleStatus = isRecord ? ControlledDocumentLifecycleStatus.Effective : ControlledDocumentLifecycleStatus.Draft;
            if (isRecord && register.EffectiveDate is null)
            {
                register.EffectiveDate = DateTimeOffset.UtcNow;
            }
            register.UpdatedAt = DateTimeOffset.UtcNow;
            register.UpdatedBy = _currentUser.ActorName;
            await _register.UpdateAsync(register, ct);

            // Auto-resolve the FU09 approval route now that the register entry exists and is linked. The route is a
            // deterministic projection of the entry's class/criticality/impact flags, so no manual "Resolve Route"
            // action is needed. Best-effort: registration has already succeeded and must not fail if resolution hiccups.
            // Records are not controlled documents, so they have no approval route — skip resolution for them.
            if (!isRecord)
            {
                await TryResolveApprovalRouteAsync(register.Id, correlationId, ct);
            }

            // Persist the document-centric variant link (anchor for the Faz 2b localization governance).
            if (isVariant && variantParent is not null && _documentVariants is not null
                && await _documentVariants.GetByVariantRegisterEntryAsync(register.Id, ct) is null)
            {
                await _documentVariants.CreateAsync(new DocumentVariant
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    VariantRegisterEntryId = register.Id,
                    VariantControlledDocumentId = operation.ControlledDocumentId,
                    ParentRegisterEntryId = variantParent.Id,
                    ParentControlledDocumentId = variantParent.ControlledDocumentId,
                    VariantType = input.VariantType,
                    LanguageCode = TrimOrNull(input.LanguageCode),
                    CountryCode = TrimOrNull(input.CountryCode),
                    SiteCode = TrimOrNull(input.SiteCode),
                    ParentChecksum = parentChecksum,
                    VariantChecksum = descriptor.Checksum,
                    ContentChangeVerified = parentChecksum is not null,
                    CorrelationId = correlationId,
                    CreatedBy = _currentUser.ActorName
                }, ct);
            }

            operation.MarkLinked(_currentUser.ActorName);
            await _operations.UpdateAsync(operation, ct);
            operation.MarkCompleted(_currentUser.ActorName);
            await _operations.UpdateAsync(operation, ct);
            return Completed(operation, correlationId);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // A prior (or concurrent) registration already created a controlled document with the same title in the
            // same folder — the unique index ux_dm_controlled_documents_tenant_key_active rejects the second insert.
            // Surface a clear 409 the user can act on, not a generic 500. Same partial-metadata compensation runs.
            await CleanupAfterFailureAsync(operation, storedThisAttempt, ct);
            return await FailAsync(operation,
                "A controlled document with this title already exists in the selected folder.", 409,
                ControlledDocumentRegistrationReasonCodes.DuplicateDocumentTitle,
                operation.ControlledDocumentId is not null || operation.MasterRegisterEntryId is not null,
                correlationId, ct, ex.Message);
        }
        catch (Exception ex)
        {
            await CleanupAfterFailureAsync(operation, storedThisAttempt, ct);
            return await FailAsync(operation, "Registration could not be completed.", 500,
                ControlledDocumentRegistrationReasonCodes.RegistrationFailed,
                operation.ControlledDocumentId is not null || operation.MasterRegisterEntryId is not null,
                correlationId, ct, ex.Message);
        }
    }

    /// <summary>
    /// Best-effort auto-resolution of the approval route for a freshly registered entry. Never throws: registration is
    /// the primary, already-committed operation, so a resolver failure must not fail or roll it back. Resolution is
    /// idempotent, so a later manual/API resolve (or a metadata edit) still reconciles it.
    /// </summary>
    private async Task TryResolveApprovalRouteAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        if (_approval is null)
        {
            return;
        }

        try
        {
            await _approval.ResolveRouteAsync(registerEntryId, new ResolveApprovalRouteInput(), correlationId, ct);
        }
        catch
        {
            // Swallow: the registration itself has succeeded. The route can be reconciled later (idempotent).
        }
    }

    /// <summary>
    /// Shared failure compensation: drops content stored during THIS attempt (only when no document has adopted it yet)
    /// and soft-archives any partial metadata already written. Safe to call from any failure path.
    /// </summary>
    private async Task CleanupAfterFailureAsync(
        ControlledDocumentRegistrationOperation operation, ContentStoreResult? storedThisAttempt, CancellationToken ct)
    {
        if (storedThisAttempt is not null && operation.ControlledDocumentId is null)
        {
            if (await _versioning.TryDeleteAsync(storedThisAttempt, ct))
            {
                operation.ResetStoredContentAfterCleanup(_currentUser.ActorName);
            }
        }
        await SoftArchivePartialMetadataAsync(operation, ct);
    }

    internal static bool IsScopeOwnerAligned(
        CollectionInstanceReferenceDto? folder,
        DocumentScope documentScope,
        Guid expectedOwner,
        Guid companyId) =>
        folder is not null
        && (folder.ScopeOwnerId == expectedOwner
            || (documentScope == DocumentScope.Company
                && folder.ScopeOwnerId == Guid.Empty
                && folder.CompanyId == companyId));

    private async Task SoftArchivePartialMetadataAsync(ControlledDocumentRegistrationOperation operation, CancellationToken ct)
    {
        if (operation.ControlledDocumentId is { } documentId)
        {
            var document = await _documents.GetByIdAsync(documentId, ct);
            if (document is not null)
            {
                document.Status = ControlledItemStatus.Archived;
                document.UpdatedAt = DateTimeOffset.UtcNow;
                document.UpdatedBy = _currentUser.ActorName;
                await _documents.UpdateAsync(document, ct);
            }
        }

        if (operation.MasterRegisterEntryId is { } registerId)
        {
            var entry = await _register.GetByIdAsync(registerId, ct);
            if (entry is not null)
            {
                entry.RegisterStatus = DocumentRegisterStatus.Archived;
                entry.UpdatedAt = DateTimeOffset.UtcNow;
                entry.UpdatedBy = _currentUser.ActorName;
                await _register.UpdateAsync(entry, ct);
            }
        }
    }

    private async Task<Response<ControlledDocumentRegistrationResultModel>> FailAsync(
        ControlledDocumentRegistrationOperation operation,
        string message,
        int status,
        string reason,
        bool compensationPending,
        string correlationId,
        CancellationToken ct,
        string? detail = null)
    {
        operation.MarkFailure(reason, Sanitize(detail ?? message), compensationPending, _currentUser.ActorName);
        await _operations.UpdateAsync(operation, ct);
        return Response<ControlledDocumentRegistrationResultModel>.Fail(message, status, reason, correlationId);
    }

    private static Response<ControlledDocumentRegistrationResultModel> Completed(
        ControlledDocumentRegistrationOperation operation,
        string correlationId) =>
        Response<ControlledDocumentRegistrationResultModel>.Success(
            new(
                operation.Id,
                operation.ControlledDocumentId!.Value,
                operation.ControlledDocumentVersionId!.Value,
                operation.MasterRegisterEntryId!.Value,
                operation.Status.ToString(),
                correlationId),
            operation.Status == ControlledDocumentRegistrationStatus.Completed ? 200 : 201,
            correlationId);

    private static Response<T> NotFound<T>(string correlationId) =>
        Response<T>.Fail("Not found.", 404, ControlledDocumentRegistrationReasonCodes.NotFoundNonLeakage, correlationId);

    private static string Sanitize(string value)
    {
        var singleLine = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return singleLine.Length <= 1000 ? singleLine : singleLine[..1000];
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Guid? EmptyToNull(Guid? value) => value is null || value == Guid.Empty ? null : value;
    private static List<string> NormalizeTags(IReadOnlyList<string>? tags) =>
        tags?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList() ?? [];

    private static RegistrationSnapshot ToSnapshot(CreateControlledDocumentRegistrationInput input) => new(
        input.IdempotencyKey, input.DocumentTitle, input.DocumentClass, input.Criticality, input.DocumentType,
        input.Description, input.Tags, input.GoverningLanguage, input.OwnerFunction, input.OwnerCompanyId,
        input.ProcessOwnerRole, input.ProcessOwnerUserId, input.AuthorUserId, input.ReviewCycleMonths, input.RetentionClass,
        input.CompanyId, input.CollectionInstanceId,
        input.DocumentScope, input.CorporateOwnerId,
        input.FolderId == Guid.Empty ? input.CollectionInstanceId : input.FolderId,
        input.GoverningLanguageId, input.RetentionClassId, input.Kind, input.RecordCode,
        input.ParentRegisterEntryId, input.VariantType, input.LanguageCode, input.CountryCode, input.SiteCode);

    private static CreateControlledDocumentRegistrationInput FromSnapshot(RegistrationSnapshot snapshot) => new(
        snapshot.IdempotencyKey, snapshot.DocumentTitle, snapshot.DocumentClass, snapshot.Criticality,
        snapshot.DocumentType, snapshot.Description, snapshot.Tags, snapshot.GoverningLanguage,
        snapshot.OwnerFunction, snapshot.OwnerCompanyId, snapshot.ProcessOwnerRole, snapshot.ProcessOwnerUserId,
        snapshot.ReviewCycleMonths, snapshot.RetentionClass, snapshot.CompanyId, snapshot.CollectionInstanceId,
        new FileUploadInput("retry-pointer", null, string.Empty))
    {
        DocumentScope = snapshot.DocumentScope,
        CorporateOwnerId = snapshot.CorporateOwnerId,
        FolderId = snapshot.FolderId,
        AuthorUserId = snapshot.AuthorUserId,
        GoverningLanguageId = snapshot.GoverningLanguageId,
        RetentionClassId = snapshot.RetentionClassId,
        Kind = snapshot.Kind,
        RecordCode = snapshot.RecordCode,
        ParentRegisterEntryId = snapshot.ParentRegisterEntryId,
        VariantType = snapshot.VariantType,
        LanguageCode = snapshot.LanguageCode,
        CountryCode = snapshot.CountryCode,
        SiteCode = snapshot.SiteCode
    };

    private sealed record RegistrationSnapshot(
        string IdempotencyKey, string DocumentTitle, string DocumentClass, string Criticality, string DocumentType,
        string? Description, IReadOnlyList<string>? Tags, string GoverningLanguage, string? OwnerFunction,
        Guid OwnerCompanyId, string? ProcessOwnerRole, Guid? ProcessOwnerUserId, Guid? AuthorUserId, int? ReviewCycleMonths,
        string? RetentionClass, Guid CompanyId, Guid CollectionInstanceId,
        DocumentScope DocumentScope = DocumentScope.Company,
        Guid CorporateOwnerId = default,
        Guid FolderId = default,
        string? GoverningLanguageId = null,
        string? RetentionClassId = null,
        RegistrationKind Kind = RegistrationKind.ControlledDocument,
        string? RecordCode = null,
        Guid? ParentRegisterEntryId = null,
        DocumentVariantType VariantType = DocumentVariantType.Translation,
        string? LanguageCode = null,
        string? CountryCode = null,
        string? SiteCode = null);

    private static string ScopeFingerprint(
        CreateControlledDocumentRegistrationInput input,
        Guid folderId,
        string storagePartition,
        string languageId,
        string retentionId) =>
        string.Join('|',
            input.DocumentScope,
            input.DocumentScope == DocumentScope.Company ? input.CompanyId : input.CorporateOwnerId,
            input.DocumentScope == DocumentScope.Company ? input.OwnerCompanyId : Guid.Empty,
            input.CollectionInstanceId,
            folderId,
            storagePartition,
            languageId,
            retentionId);

    private sealed record ContentDescriptor(
        Guid ContentId, string StorageProvider, string ObjectKey, string FileName, string MediaType,
        long ByteSize, string Checksum, Guid DocumentId, Guid VersionId)
    {
        public static ContentDescriptor From(ContentStoreResult result, Guid documentId, Guid versionId) =>
            new(result.ContentId, result.StorageProvider, result.ObjectKey, result.FileName, result.MediaType,
                result.ByteSize, result.Checksum, documentId, versionId);

        public ContentRef ToContentRef(string actor) => new()
        {
            ContentId = ContentId,
            StorageProvider = StorageProvider,
            ObjectKey = ObjectKey,
            FileName = FileName,
            MediaType = MediaType,
            ByteSize = ByteSize,
            Checksum = Checksum,
            CreatedBy = actor,
            VersionId = VersionId
        };
    }
}
