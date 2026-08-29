using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementApproval;
using Diten.Platform.Application.Features.DocumentManagementApproval.Services;
using Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Common.Tenancy;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;

/// <summary>
/// MOD-0029-FU06 — Document Master Register orchestration (GMG-QMS-SOP-0001 §18/§20). Foundation only: it creates and
/// maintains the regulated register PROJECTION and links it to an existing FU01 <see cref="ControlledDocument"/>. It
/// deliberately does NOT run the lifecycle transition engine (FU08), the approval route (FU09) or the non-waivable
/// release-gate engine (FU10); protected fields (UID, code, lifecycle status, effective date, version label, gate /
/// approval results) are never mutated by the metadata-update path. No hard delete.
/// </summary>
public sealed class DocumentMasterRegisterService
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IControlledDocumentRepository _controlledDocuments;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly DocumentLinkScopeCompatibilityValidator _linkCompatibility;
    private readonly CorporateCollectionFolderAccessEvaluator? _corporateAccess;
    private readonly DocumentApprovalService? _approval;

    public DocumentMasterRegisterService(
        IDocumentMasterRegisterRepository register,
        IControlledDocumentRepository controlledDocuments,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        DocumentLinkScopeCompatibilityValidator? linkCompatibility = null,
        CorporateCollectionFolderAccessEvaluator? corporateAccess = null,
        DocumentApprovalService? approval = null)
    {
        _register = register;
        _controlledDocuments = controlledDocuments;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _linkCompatibility = linkCompatibility ?? new DocumentLinkScopeCompatibilityValidator();
        _corporateAccess = corporateAccess;
        _approval = approval;
    }

    public async Task<Response<MasterRegisterDetailModel>> CreateAsync(CreateMasterRegisterEntryInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        var documentClass = MasterRegisterWire.ParseClass(input.DocumentClass);
        var criticality = MasterRegisterWire.ParseCriticality(input.Criticality);
        var documentType = string.IsNullOrWhiteSpace(input.DocumentType) ? DocumentType.Other : MasterRegisterWire.ParseDocumentType(input.DocumentType);

        var validation = ValidateShape(input.DocumentTitle, documentClass, criticality, documentType, input.DocumentType,
            input.ReviewCycleMonths, input.IsRecord, input.IsControlledDocument, input.IsVariant, input.ParentDocumentUid, input.ParentDocumentCode, correlationId);
        if (validation is not null)
        {
            return validation;
        }

        var authorUserId = EmptyToNull(input.AuthorUserId);
        if (authorUserId is null)
        {
            return Fail("A document author is required.", 400, MasterRegisterReasonCodes.ValidationFailed, correlationId);
        }

        var permanentUid = TrimOrNull(input.PermanentUid);
        var documentCode = TrimOrNull(input.DocumentCode);

        if (permanentUid is not null && await _register.GetByPermanentUidAsync(permanentUid, ct) is not null)
        {
            return Fail("A register entry with the same Permanent UID already exists.", 409, MasterRegisterReasonCodes.DuplicatePermanentUid, correlationId);
        }

        if (documentCode is not null && await _register.GetByDocumentCodeAsync(documentCode, ct) is not null)
        {
            return Fail("A register entry with the same Document Code already exists.", 409, MasterRegisterReasonCodes.DuplicateDocumentCode, correlationId);
        }

        var entry = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PermanentUid = permanentUid,
            DocumentCode = documentCode,
            // Manual entry in FU06: provenance is NOT system-allocated. The FU07 allocation engine flips this to true.
            IsSystemAllocated = false,
            LegacyCode = TrimOrNull(input.LegacyCode),
            DocumentTitle = input.DocumentTitle.Trim(),
            DocumentType = documentType!.Value,
            DocumentClass = documentClass!.Value,
            Criticality = criticality!.Value,
            ProcessOwnerRole = TrimOrNull(input.ProcessOwnerRole),
            ProcessOwnerUserId = EmptyToNull(input.ProcessOwnerUserId),
            // Author is an independent, explicit identity. It must never be inferred from Document/Process Owner.
            AuthorUserId = authorUserId,
            OwnerFunction = TrimOrNull(input.OwnerFunction),
            OwnerCompanyId = EmptyToNull(input.OwnerCompanyId),
            DocumentScope = DocumentScope.Company,
            ScopeOwnerId = EmptyToNull(input.OwnerCompanyId) ?? Guid.Empty,
            GoverningLanguage = TrimOrNull(input.GoverningLanguage),
            ReviewCycleMonths = input.ReviewCycleMonths,
            RetentionClass = TrimOrNull(input.RetentionClass),
            IsControlledDocument = input.IsControlledDocument,
            IsRecord = input.IsRecord,
            IsExternalDocument = input.IsExternalDocument,
            IsTemplate = input.IsTemplate,
            IsVariant = input.IsVariant,
            ParentDocumentUid = TrimOrNull(input.ParentDocumentUid),
            ParentDocumentCode = TrimOrNull(input.ParentDocumentCode),
            SourceSystem = TrimOrNull(input.SourceSystem),
            SourceLegacyId = TrimOrNull(input.SourceLegacyId),
            // Protected lifecycle/register defaults — no transition engine runs in FU06.
            LifecycleStatus = ControlledDocumentLifecycleStatus.Draft,
            RegisterStatus = DocumentRegisterStatus.Draft,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        await _register.CreateAsync(entry, ct);

        // The approval route is a deterministic projection of the entry's own class / criticality / impact flags, so it
        // is resolved automatically here — no separate operator "Resolve Route" action. Re-fetch so the returned detail
        // reflects the ApprovalEvidenceStatus the resolver just wrote back.
        var resolved = await ResolveApprovalRouteAsync(entry.Id, correlationId, ct) ?? entry;
        return Response<MasterRegisterDetailModel>.Success(MasterRegisterWire.ToDetail(resolved), 201, correlationId);
    }

    public async Task<Response<IReadOnlyList<MasterRegisterListItemModel>>> ListAsync(MasterRegisterListFilter filter, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _register.ListAsync(filter, ct);
        var items = rows.Select(MasterRegisterWire.ToListItem).ToList();
        return Response<IReadOnlyList<MasterRegisterListItemModel>>.Success(items, correlationId: correlationId);
    }

    public async Task<Response<MasterRegisterDetailModel>> GetDetailAsync(Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(id, ct);
        return entry is null
            ? Fail("Register entry not found.", 404, MasterRegisterReasonCodes.NotFoundNonLeakage, correlationId)
            : Response<MasterRegisterDetailModel>.Success(MasterRegisterWire.ToDetail(entry), correlationId: correlationId);
    }

    public async Task<Response<MasterRegisterDetailModel>> UpdateMetadataAsync(Guid id, UpdateMasterRegisterMetadataInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, MasterRegisterReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var documentClass = MasterRegisterWire.ParseClass(input.DocumentClass);
        var criticality = MasterRegisterWire.ParseCriticality(input.Criticality);
        var documentType = string.IsNullOrWhiteSpace(input.DocumentType) ? entry.DocumentType : MasterRegisterWire.ParseDocumentType(input.DocumentType);

        var validation = ValidateShape(input.DocumentTitle, documentClass, criticality, documentType, input.DocumentType,
            input.ReviewCycleMonths, entry.IsRecord, entry.IsControlledDocument, entry.IsVariant, input.ParentDocumentUid, input.ParentDocumentCode, correlationId);
        if (validation is not null)
        {
            return validation;
        }

        var requestedAuthorId = EmptyToNull(input.AuthorUserId);
        if (entry.AuthorUserId is { } recordedAuthorId
            && requestedAuthorId is { } replacementAuthorId
            && replacementAuthorId != recordedAuthorId)
        {
            return Fail("The document author is immutable once recorded.", 409, MasterRegisterReasonCodes.ProtectedFieldChange, correlationId);
        }

        // Only NON-PROTECTED metadata is mutated here. UID / DocumentCode / LifecycleStatus / EffectiveDate /
        // CurrentVersionLabel / release-gate / approval-evidence are intentionally untouched (set by FU07/FU08/FU10).
        // ApprovedRepositoryId/Name/Path are ALSO intentionally untouched here: they are owned by the FU16 Repository
        // Assessment process (ApprovedRepositoryId holds the assessment GUID that Release Gate 2 resolves), so a plain
        // metadata edit must never overwrite them — even though the input still carries the fields, they are ignored.
        entry.DocumentTitle = input.DocumentTitle.Trim();
        entry.DocumentClass = documentClass!.Value;
        entry.Criticality = criticality!.Value;
        entry.DocumentType = documentType!.Value;
        entry.LegacyCode = TrimOrNull(input.LegacyCode);
        entry.ProcessOwnerRole = TrimOrNull(input.ProcessOwnerRole);
        entry.ProcessOwnerUserId = EmptyToNull(input.ProcessOwnerUserId);
        if (entry.AuthorUserId is null)
        {
            // Legacy rows may have been created before AuthorUserId became mandatory. Permit a one-time explicit
            // assignment; after that the author is immutable so an editor cannot bypass segregation controls.
            entry.AuthorUserId = requestedAuthorId;
        }
        entry.OwnerFunction = TrimOrNull(input.OwnerFunction);
        entry.OwnerCompanyId = EmptyToNull(input.OwnerCompanyId);
        entry.GoverningLanguage = TrimOrNull(input.GoverningLanguage);
        entry.ReviewCycleMonths = input.ReviewCycleMonths;
        entry.RetentionClass = TrimOrNull(input.RetentionClass);
        entry.ParentDocumentUid = TrimOrNull(input.ParentDocumentUid);
        entry.ParentDocumentCode = TrimOrNull(input.ParentDocumentCode);
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedBy = _currentUser.ActorName;

        await _register.UpdateAsync(entry, ct);

        // Class / criticality drive the approval route, and either may change on this metadata edit — re-resolve so the
        // route stays in sync (adds newly-required approvals, retires no-longer-needed PENDING ones; completed evidence
        // is never touched). Re-fetch so the returned detail reflects the recomputed ApprovalEvidenceStatus.
        var resolved = await ResolveApprovalRouteAsync(entry.Id, correlationId, ct) ?? entry;
        return Response<MasterRegisterDetailModel>.Success(MasterRegisterWire.ToDetail(resolved), correlationId: correlationId);
    }

    /// <summary>
    /// Auto-resolves the FU09 approval route as a side-effect of create/update. Best-effort and non-fatal: the register
    /// entry is the primary write and already persisted, so a resolver hiccup must not fail the caller's operation.
    /// Returns the re-fetched entry (with the recomputed ApprovalEvidenceStatus) or null when resolution is unavailable.
    /// </summary>
    private async Task<DocumentMasterRegisterEntry?> ResolveApprovalRouteAsync(Guid entryId, string correlationId, CancellationToken ct)
    {
        if (_approval is null)
        {
            return null;
        }

        await _approval.ResolveRouteAsync(entryId, new ResolveApprovalRouteInput(), correlationId, ct);
        return await _register.GetByIdAsync(entryId, ct);
    }

    public async Task<Response<MasterRegisterDetailModel>> LinkControlledDocumentAsync(
        Guid id,
        Guid controlledDocumentId,
        string reconciliationReason,
        string correlationId,
        CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, MasterRegisterReasonCodes.NotFoundNonLeakage, correlationId);
        }

        // Tenant-scoped fetch: a cross-tenant document resolves to null → non-leaking 404.
        var document = await _controlledDocuments.GetByIdAsync(controlledDocumentId, ct);
        if (document is null)
        {
            return Fail("Controlled document not found.", 404, MasterRegisterReasonCodes.NotFoundNonLeakage, correlationId);
        }

        if (entry.ControlledDocumentId is { } existing && existing != controlledDocumentId)
        {
            return Fail("Register entry is already linked to a different controlled document.", 409, MasterRegisterReasonCodes.AlreadyLinked, correlationId);
        }

        if (string.IsNullOrWhiteSpace(reconciliationReason))
        {
            return Fail("A reconciliation reason is required.", 400, MasterRegisterReasonCodes.ValidationFailed, correlationId);
        }

        var compatibility = _linkCompatibility.Validate(entry, document);
        if (!compatibility.IsCompatible)
        {
            entry.LinkScopeCompatibilityStatus = DocumentLinkScopeCompatibilityStatus.Invalid;
            return Fail(
                compatibility.Message ?? "The controlled-document relation is incompatible.",
                409,
                compatibility.ReasonCode ?? MasterRegisterReasonCodes.ScopeMismatch,
                correlationId);
        }

        if (document.DocumentScope == DocumentScope.Corporate
            && (_corporateAccess is null
                || !await _corporateAccess.HasExplicitGrantAsync(
                    document.FolderId,
                    DocumentAccessMatrixAction.CreateDocument,
                    ct)))
        {
            return Fail(
                "Corporate reconciliation access is required.",
                403,
                MasterRegisterReasonCodes.CorporateAccessRequired,
                correlationId);
        }

        entry.ControlledDocumentId = controlledDocumentId;
        entry.LinkScopeCompatibilityStatus = DocumentLinkScopeCompatibilityStatus.Compatible;
        entry.ControlledDocumentLinkedAt = DateTimeOffset.UtcNow;
        entry.ControlledDocumentLinkedBy = _currentUser.ActorName;
        entry.ControlledDocumentLinkReason = reconciliationReason.Trim();
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedBy = _currentUser.ActorName;

        await _register.UpdateAsync(entry, ct);
        return Response<MasterRegisterDetailModel>.Success(MasterRegisterWire.ToDetail(entry), correlationId: correlationId);
    }

    public async Task<Response<MasterRegisterSummaryModel>> GetSummaryAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _register.GetAllForTenantAsync(ct);

        var summary = new MasterRegisterSummaryModel(
            Total: rows.Count,
            ByRegisterStatus: rows.GroupBy(x => x.RegisterStatus.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            ByLifecycleStatus: rows.GroupBy(x => x.LifecycleStatus.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            ByCriticality: rows.GroupBy(x => x.Criticality.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            ByClass: rows.GroupBy(x => x.DocumentClass.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            WithPermanentUid: rows.Count(x => !string.IsNullOrWhiteSpace(x.PermanentUid)),
            WithoutPermanentUid: rows.Count(x => string.IsNullOrWhiteSpace(x.PermanentUid)),
            LinkedToControlledDocument: rows.Count(x => x.ControlledDocumentId is not null));

        return Response<MasterRegisterSummaryModel>.Success(summary, correlationId: correlationId);
    }

    // ── validation ──────────────────────────────────────────────────────────────

    private static Response<MasterRegisterDetailModel>? ValidateShape(
        string? title, ControlledDocumentClass? documentClass, DocumentCriticality? criticality, DocumentType? documentType,
        string? rawDocumentType, int? reviewCycleMonths, bool isRecord, bool isControlledDocument, bool isVariant,
        string? parentDocumentUid, string? parentDocumentCode, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Fail("Document title is required.", 400, MasterRegisterReasonCodes.ValidationFailed, correlationId);
        }

        if (documentClass is null)
        {
            return Fail("A valid document class is required.", 400, MasterRegisterReasonCodes.ValidationFailed, correlationId);
        }

        if (criticality is null)
        {
            return Fail("A valid criticality is required.", 400, MasterRegisterReasonCodes.ValidationFailed, correlationId);
        }

        if (!string.IsNullOrWhiteSpace(rawDocumentType) && documentType is null)
        {
            return Fail("Document type is not recognised.", 400, MasterRegisterReasonCodes.ValidationFailed, correlationId);
        }

        if (reviewCycleMonths is { } months && months <= 0)
        {
            return Fail("Review cycle months must be positive when provided.", 400, MasterRegisterReasonCodes.ValidationFailed, correlationId);
        }

        // SOP §2 boundary: an object cannot be both a controlled document and a completed record.
        if (isRecord && isControlledDocument)
        {
            return Fail("An entry cannot be both a controlled document and a record (SOP §2 boundary).", 400, MasterRegisterReasonCodes.RecordControlledConflict, correlationId);
        }

        // SOP §13.2: a variant references its parent. FU06 decision: this is a hard validation error, not a warning.
        if (isVariant && string.IsNullOrWhiteSpace(parentDocumentUid) && string.IsNullOrWhiteSpace(parentDocumentCode))
        {
            return Fail("A variant entry requires a parent document UID or code (SOP §13.2).", 400, MasterRegisterReasonCodes.VariantParentMissing, correlationId);
        }

        return null;
    }

    private static Response<MasterRegisterDetailModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<MasterRegisterDetailModel>.Fail(error, status, reason, correlationId);

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? EmptyToNull(Guid? value) => value == Guid.Empty ? null : value;
}
