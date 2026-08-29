using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementIdentifiers.Services;

/// <summary>
/// MOD-0029-FU07 — central Permanent UID / Document Code allocation engine (GMG-QMS-SOP-0001 §6.3, §9.2, §9.3, §12.3).
/// Numbers come from a monotonic, concurrency-safe sequence counter and every allocation is written to an append-only
/// ledger, so values are NEVER reused — including cancelled/abandoned/soft-deleted ones. System allocation flips the
/// register entry's <c>IsSystemAllocated</c> provenance; manually reserved values keep it false. Protected fields are
/// only ever set through this engine — never through the FU06 metadata-update path. No hard delete (cancel = status
/// change). This FU implements NO lifecycle/approval/release-gate/training behaviour.
/// </summary>
public sealed class DocumentIdentifierAllocationService
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IDocumentIdentifierAllocationRepository _ledger;
    private readonly IDocumentIdentifierSequenceCounterRepository _counter;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly DocumentCodingOptions _coding;

    private const int MaxSequenceProbes = 100;

    public DocumentIdentifierAllocationService(
        IDocumentMasterRegisterRepository register,
        IDocumentIdentifierAllocationRepository ledger,
        IDocumentIdentifierSequenceCounterRepository counter,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        IOptions<DocumentCodingOptions> coding)
    {
        _register = register;
        _ledger = ledger;
        _counter = counter;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _coding = coding.Value;
    }

    // ── public: allocate ──────────────────────────────────────────────────────

    /// <summary>
    /// Produces a default code in the governed Document-Code standard for a RECORD. Records are not eligible for the
    /// full governed allocation flow, but may carry a system default in the SAME format the user can override. Draws a
    /// unique number from the same monotonic counter and formats it identically; it is NOT written to the allocation
    /// ledger (records are not governed allocations). Returns null if a unique value could not be produced.
    /// </summary>
    public Task<string?> GenerateRecordCodeAsync(ControlledDocumentClass documentClass, DocumentType documentType, CancellationToken ct)
    {
        var typeCode = DocumentTypeCodeResolver.Resolve(documentClass, documentType);
        return GenerateRecordCodeCoreAsync(typeCode, ct);
    }

    private async Task<string?> GenerateRecordCodeCoreAsync(string? typeCode, CancellationToken ct)
    {
        var (value, _) = await GenerateUniqueAsync(
            DocumentIdentifierType.DocumentCode, _coding.OrgPrefix, _coding.DomainCode, typeCode, _coding.CodePadding, ct);
        return value;
    }

    public async Task<Response<IdentifierAllocationResultModel>> AllocateUidAsync(Guid registerEntryId, string? reason, string correlationId, CancellationToken ct)
    {
        var (fail, entry, model) = await AllocateUidInternalAsync(registerEntryId, IdentifierWire.ParseReason(reason), correlationId, ct);
        return fail ?? Ok(entry!, model, null, correlationId);
    }

    public async Task<Response<IdentifierAllocationResultModel>> AllocateCodeAsync(Guid registerEntryId, string? reason, string correlationId, CancellationToken ct)
    {
        var (fail, entry, model) = await AllocateCodeInternalAsync(registerEntryId, IdentifierWire.ParseReason(reason), correlationId, ct);
        return fail ?? Ok(entry!, null, model, correlationId);
    }

    public async Task<Response<IdentifierAllocationResultModel>> AllocateIdentifiersAsync(Guid registerEntryId, string? reason, string correlationId, CancellationToken ct)
    {
        var parsedReason = IdentifierWire.ParseReason(reason);

        var uid = await AllocateUidInternalAsync(registerEntryId, parsedReason, correlationId, ct);
        if (uid.Fail is not null)
        {
            return uid.Fail;
        }

        var code = await AllocateCodeInternalAsync(registerEntryId, parsedReason, correlationId, ct);
        if (code.Fail is not null)
        {
            // No rollback of the already-recorded UID allocation: it is a legitimate, non-reusable allocation
            // (SOP §6.3). The partial result surfaces the UID that was assigned so the caller can retry the code.
            return Response<IdentifierAllocationResultModel>.Fail(code.Fail.Errors, code.Fail.StatusCode, code.Fail.ReasonCode, correlationId);
        }

        return Ok(code.Entry!, uid.Model, code.Model, correlationId);
    }

    // ── public: reserve (manual / migration) ───────────────────────────────────

    public async Task<Response<IdentifierAllocationModel>> ReserveAsync(ReserveIdentifierInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var type = IdentifierWire.ParseType(input.IdentifierType);
        var value = input.IdentifierValue?.Trim();
        if (type is null || string.IsNullOrWhiteSpace(value))
        {
            return FailModel("A valid identifier type and value are required.", 400, IdentifierAllocationReasonCodes.ValidationFailed, correlationId);
        }

        if (await _ledger.ExistsValueIncludingDeletedAsync(type.Value, value, ct))
        {
            return FailModel("This identifier value has already been allocated and cannot be reused.", 409, IdentifierAllocationReasonCodes.DuplicateIdentifier, correlationId);
        }

        DocumentMasterRegisterEntry? entry = null;
        if (input.RegisterEntryId is { } entryId && entryId != Guid.Empty)
        {
            entry = await _register.GetByIdAsync(entryId, ct);
            if (entry is null)
            {
                return FailModel("Register entry not found.", 404, IdentifierAllocationReasonCodes.NotFoundNonLeakage, correlationId);
            }

            var current = type == DocumentIdentifierType.PermanentUid ? entry.PermanentUid : entry.DocumentCode;
            if (!string.IsNullOrWhiteSpace(current))
            {
                return FailModel("The register entry already has this identifier.", 409, IdentifierAllocationReasonCodes.ManualIdentifierExists, correlationId);
            }
        }

        var allocation = NewAllocation(type.Value, value, sequenceNumber: null, prefix: null, domainCode: null, typeCode: null,
            entry, isSystemAllocated: false,
            reason: input.AllocationReason is null ? DocumentIdentifierAllocationReason.ManualImport : IdentifierWire.ParseReason(input.AllocationReason),
            status: DocumentIdentifierAllocationStatus.Reserved, correlationId);
        allocation.LegacyCode = TrimOrNull(input.LegacyCode);
        allocation.SourceSystem = TrimOrNull(input.SourceSystem);
        allocation.SourceLegacyId = TrimOrNull(input.SourceLegacyId);

        await _ledger.CreateAsync(allocation, ct);

        if (entry is not null)
        {
            if (type == DocumentIdentifierType.PermanentUid) entry.PermanentUid = value; else entry.DocumentCode = value;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            entry.UpdatedBy = _currentUser.ActorName;
            await _register.UpdateAsync(entry, ct);
        }

        return Response<IdentifierAllocationModel>.Success(IdentifierWire.ToModel(allocation), 201, correlationId);
    }

    // ── public: cancel (never-reuse preserving) ────────────────────────────────

    public async Task<Response<IdentifierAllocationModel>> CancelAsync(Guid allocationId, CancelIdentifierInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var allocation = await _ledger.GetByIdAsync(allocationId, ct);
        if (allocation is null)
        {
            return FailModel("Allocation not found.", 404, IdentifierAllocationReasonCodes.NotFoundNonLeakage, correlationId);
        }

        // Status change only — the value stays in the ledger and is never reused (SOP §6.3).
        allocation.AllocationStatus = DocumentIdentifierAllocationStatus.Cancelled;
        allocation.CancelledAt = DateTimeOffset.UtcNow;
        allocation.CancelledBy = _currentUser.ActorName;
        allocation.CancellationReason = TrimOrNull(input.CancellationReason);
        allocation.UpdatedAt = DateTimeOffset.UtcNow;
        allocation.UpdatedBy = _currentUser.ActorName;
        await _ledger.UpdateAsync(allocation, ct);

        if (allocation.RegisterEntryId is { } registerEntryId && registerEntryId != Guid.Empty)
        {
            var entry = await _register.GetByIdAsync(registerEntryId, ct);
            if (entry is not null)
            {
                var clearedCurrentIdentifier = false;
                if (allocation.IdentifierType == DocumentIdentifierType.PermanentUid
                    && string.Equals(entry.PermanentUid, allocation.IdentifierValue, StringComparison.Ordinal))
                {
                    entry.PermanentUid = null;
                    clearedCurrentIdentifier = true;
                }
                else if (allocation.IdentifierType == DocumentIdentifierType.DocumentCode
                    && string.Equals(entry.DocumentCode, allocation.IdentifierValue, StringComparison.Ordinal))
                {
                    entry.DocumentCode = null;
                    clearedCurrentIdentifier = true;
                }

                if (clearedCurrentIdentifier)
                {
                    var remaining = await _ledger.ListAsync(
                        new IdentifierAllocationListFilter(null, null, registerEntryId), ct);
                    entry.IsSystemAllocated = remaining.Any(x =>
                        x.IsSystemAllocated
                        && x.AllocationStatus == DocumentIdentifierAllocationStatus.Assigned
                        && ((x.IdentifierType == DocumentIdentifierType.PermanentUid
                                && x.IdentifierValue == entry.PermanentUid)
                            || (x.IdentifierType == DocumentIdentifierType.DocumentCode
                                && x.IdentifierValue == entry.DocumentCode)));
                    entry.UpdatedAt = DateTimeOffset.UtcNow;
                    entry.UpdatedBy = _currentUser.ActorName;
                    await _register.UpdateAsync(entry, ct);
                }
            }
        }

        return Response<IdentifierAllocationModel>.Success(IdentifierWire.ToModel(allocation), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<IdentifierAllocationModel>>> ListAsync(IdentifierAllocationListFilter filter, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _ledger.ListAsync(filter, ct);
        return Response<IReadOnlyList<IdentifierAllocationModel>>.Success(rows.Select(IdentifierWire.ToModel).ToList(), correlationId: correlationId);
    }

    // ── internal core ──────────────────────────────────────────────────────────

    private async Task<(Response<IdentifierAllocationResultModel>? Fail, DocumentMasterRegisterEntry? Entry, IdentifierAllocationModel? Model)>
        AllocateUidInternalAsync(Guid registerEntryId, DocumentIdentifierAllocationReason reason, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return (FailResult("Register entry not found.", 404, IdentifierAllocationReasonCodes.NotFoundNonLeakage, correlationId), null, null);
        }

        if (Ineligible(entry) is { } reasonCode)
        {
            return (FailResult(IneligibleMessage(reasonCode), 409, reasonCode, correlationId), null, null);
        }

        // Idempotent / manual-conflict resolution via the ledger (provenance source of truth).
        if (!string.IsNullOrWhiteSpace(entry.PermanentUid))
        {
            var existing = (await _ledger.ListAsync(new IdentifierAllocationListFilter(DocumentIdentifierType.PermanentUid, null, entry.Id), ct))
                .FirstOrDefault(x => x.IsSystemAllocated && x.IdentifierValue == entry.PermanentUid);
            return existing is not null
                ? (null, entry, IdentifierWire.ToModel(existing))
                : (FailResult("A manual Permanent UID already exists on this entry.", 409, IdentifierAllocationReasonCodes.ManualIdentifierExists, correlationId), null, null);
        }

        var (value, seq) = await GenerateUniqueAsync(DocumentIdentifierType.PermanentUid, _coding.UidPrefix, null, null, _coding.UidPadding, ct);
        if (value is null)
        {
            return (FailResult("Unable to allocate a unique Permanent UID.", 409, IdentifierAllocationReasonCodes.DuplicateIdentifier, correlationId), null, null);
        }

        var allocation = NewAllocation(DocumentIdentifierType.PermanentUid, value, seq, _coding.UidPrefix, null, null, entry, isSystemAllocated: true, reason,
            DocumentIdentifierAllocationStatus.Assigned, correlationId);
        await _ledger.CreateAsync(allocation, ct);

        entry.PermanentUid = value;
        entry.IsSystemAllocated = true;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedBy = _currentUser.ActorName;
        await _register.UpdateAsync(entry, ct);

        return (null, entry, IdentifierWire.ToModel(allocation));
    }

    private async Task<(Response<IdentifierAllocationResultModel>? Fail, DocumentMasterRegisterEntry? Entry, IdentifierAllocationModel? Model)>
        AllocateCodeInternalAsync(Guid registerEntryId, DocumentIdentifierAllocationReason reason, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return (FailResult("Register entry not found.", 404, IdentifierAllocationReasonCodes.NotFoundNonLeakage, correlationId), null, null);
        }

        if (Ineligible(entry) is { } reasonCode)
        {
            return (FailResult(IneligibleMessage(reasonCode), 409, reasonCode, correlationId), null, null);
        }

        var typeCode = DocumentTypeCodeResolver.Resolve(entry.DocumentClass, entry.DocumentType);
        if (typeCode is null)
        {
            return (FailResult("No deterministic type-code mapping for this document class/type; a Document Code cannot be allocated.", 409, IdentifierAllocationReasonCodes.TypeMappingMissing, correlationId), null, null);
        }

        if (!string.IsNullOrWhiteSpace(entry.DocumentCode))
        {
            var existing = (await _ledger.ListAsync(new IdentifierAllocationListFilter(DocumentIdentifierType.DocumentCode, null, entry.Id), ct))
                .FirstOrDefault(x => x.IsSystemAllocated && x.IdentifierValue == entry.DocumentCode);
            return existing is not null
                ? (null, entry, IdentifierWire.ToModel(existing))
                : (FailResult("A manual Document Code already exists on this entry.", 409, IdentifierAllocationReasonCodes.ManualIdentifierExists, correlationId), null, null);
        }

        var (value, seq) = await GenerateUniqueAsync(DocumentIdentifierType.DocumentCode, _coding.OrgPrefix, _coding.DomainCode, typeCode, _coding.CodePadding, ct);
        if (value is null)
        {
            return (FailResult("Unable to allocate a unique Document Code.", 409, IdentifierAllocationReasonCodes.DuplicateIdentifier, correlationId), null, null);
        }

        var allocation = NewAllocation(DocumentIdentifierType.DocumentCode, value, seq, _coding.OrgPrefix, _coding.DomainCode, typeCode, entry, isSystemAllocated: true, reason,
            DocumentIdentifierAllocationStatus.Assigned, correlationId);
        await _ledger.CreateAsync(allocation, ct);

        entry.DocumentCode = value;
        entry.IsSystemAllocated = true;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedBy = _currentUser.ActorName;
        await _register.UpdateAsync(entry, ct);

        return (null, entry, IdentifierWire.ToModel(allocation));
    }

    /// <summary>
    /// Draws sequence numbers until it finds one whose FORMATTED value is not already in the ledger. This tolerates
    /// collisions with manually reserved values (gaps are permitted, SOP §6.3). The counter never rolls back, so a
    /// skipped number is not reused.
    /// </summary>
    private async Task<(string? Value, long Seq)> GenerateUniqueAsync(DocumentIdentifierType type, string? prefix, string? domainCode, string? typeCode, int padding, CancellationToken ct)
    {
        for (var probe = 0; probe < MaxSequenceProbes; probe++)
        {
            var seq = await _counter.NextAsync(type, prefix, domainCode, typeCode, _currentUser.ActorName, ct);
            var value = Format(type, prefix, domainCode, typeCode, seq, padding);
            if (!await _ledger.ExistsValueIncludingDeletedAsync(type, value, ct))
            {
                return (value, seq);
            }
        }

        return (null, 0);
    }

    private string Format(DocumentIdentifierType type, string? prefix, string? domainCode, string? typeCode, long seq, int padding)
    {
        var number = seq.ToString().PadLeft(padding, '0');
        return type == DocumentIdentifierType.PermanentUid
            ? $"{prefix}-{number}"
            : $"{prefix}-{domainCode}-{typeCode}-{number}";
    }

    private DocumentIdentifierAllocation NewAllocation(
        DocumentIdentifierType type, string value, long? sequenceNumber, string? prefix, string? domainCode, string? typeCode,
        DocumentMasterRegisterEntry? entry, bool isSystemAllocated, DocumentIdentifierAllocationReason reason,
        DocumentIdentifierAllocationStatus status, string correlationId) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            IdentifierType = type,
            IdentifierValue = value,
            SequenceNumber = sequenceNumber,
            Prefix = prefix,
            DomainCode = domainCode,
            TypeCode = typeCode,
            RegisterEntryId = entry?.Id,
            ControlledDocumentId = entry?.ControlledDocumentId,
            AllocationStatus = status,
            AllocationReason = reason,
            IsSystemAllocated = isSystemAllocated,
            AllocatedAt = DateTimeOffset.UtcNow,
            AllocatedBy = _currentUser.ActorName,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

    // ── eligibility (SOP §2 record boundary, §13.2 variant inheritance, §13.3 external) ─────────────

    private static string? Ineligible(DocumentMasterRegisterEntry entry)
    {
        if (entry.RegisterStatus is DocumentRegisterStatus.Archived or DocumentRegisterStatus.Superseded or DocumentRegisterStatus.Retired)
        {
            return IdentifierAllocationReasonCodes.EntryNotAllocatable;
        }

        if (entry.IsRecord) return IdentifierAllocationReasonCodes.RecordNotEligible;
        if (entry.IsExternalDocument) return IdentifierAllocationReasonCodes.ExternalNotEligible;
        if (entry.IsVariant) return IdentifierAllocationReasonCodes.VariantInheritsParent;
        return null;
    }

    private static string IneligibleMessage(string reasonCode) => reasonCode switch
    {
        IdentifierAllocationReasonCodes.RecordNotEligible => "A record is not a controlled document and cannot receive a UID/Code (SOP §2).",
        IdentifierAllocationReasonCodes.ExternalNotEligible => "External documents are not GMG-coded; internal derived requirements are (SOP §13.3).",
        IdentifierAllocationReasonCodes.VariantInheritsParent => "A variant retains its parent's code and UID; it is not allocated a new one (SOP §13.2).",
        _ => "This register entry is not in an allocatable state."
    };

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static Response<IdentifierAllocationResultModel> Ok(DocumentMasterRegisterEntry entry, IdentifierAllocationModel? uid, IdentifierAllocationModel? code, string correlationId) =>
        Response<IdentifierAllocationResultModel>.Success(
            new IdentifierAllocationResultModel(entry.Id, entry.PermanentUid, entry.DocumentCode, entry.IsSystemAllocated, uid, code),
            correlationId: correlationId);

    private static Response<IdentifierAllocationResultModel> FailResult(string error, int status, string reason, string correlationId) =>
        Response<IdentifierAllocationResultModel>.Fail(error, status, reason, correlationId);

    private static Response<IdentifierAllocationModel> FailModel(string error, int status, string reason, string correlationId) =>
        Response<IdentifierAllocationModel>.Fail(error, status, reason, correlationId);

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
