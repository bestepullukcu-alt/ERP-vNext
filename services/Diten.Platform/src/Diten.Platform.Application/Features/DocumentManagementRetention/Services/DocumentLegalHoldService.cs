using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementRetention.Services;

/// <summary>
/// MOD-0029-FU15 — legal / litigation hold lifecycle (GMG-QMS-SOP-0001 §22).
///
/// SOP controls enforced here:
/// • ACTIVATION requires Legal approval evidence — a hold cannot go live on an unevidenced say-so.
/// • RELEASE requires BOTH a Legal written release approval reference AND a GQD concurrence reference. Either one
///   alone is refused. This is the single most important guard in this FU.
/// • Hold records are protected: nothing here deletes a hold, and release preserves the complete issuance trail
///   (issuer, evidence, timestamps) alongside the release trail. Backdating is impossible because every decision
///   is stamped server-side at the moment it is taken.
/// • Subject membership may change while a hold is active, but removal is a status change with a release
///   timestamp — the membership row survives as evidence that the hold once applied.
/// </summary>
public sealed class DocumentLegalHoldService
{
    private readonly IDocumentLegalHoldRepository _holds;
    private readonly IDocumentLegalHoldSubjectRepository _holdSubjects;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentLegalHoldService(
        IDocumentLegalHoldRepository holds,
        IDocumentLegalHoldSubjectRepository holdSubjects,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _holds = holds;
        _holdSubjects = holdSubjects;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<LegalHoldModel>> CreateAsync(LegalHoldFieldsInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        if (string.IsNullOrWhiteSpace(input.HoldTitle))
        {
            return Fail("A hold title is required.", 400, RetentionReasonCodes.HoldTitleRequired, correlationId);
        }

        var scopeType = RetentionWire.ParseScopeType(input.ScopeType);
        var hold = new DocumentLegalHold
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            HoldKey = string.IsNullOrWhiteSpace(input.HoldKey)
                ? $"HOLD-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}"
                : input.HoldKey.Trim().ToUpperInvariant(),
            HoldTitle = input.HoldTitle.Trim(),
            HoldStatus = LegalHoldStatus.Draft,
            HoldReason = RetentionWire.ParseHoldReason(input.HoldReason),
            ScopeType = scopeType,
            RegisterEntryIds = input.RegisterEntryIds?.ToList() ?? [],
            ControlledDocumentIds = input.ControlledDocumentIds?.ToList() ?? [],
            SubjectTypes = input.SubjectTypes?.Select(RetentionWire.ParseSubjectType).Where(x => x is not null)
                .Select(x => x!.Value).ToList() ?? [],
            ExternalDocumentIds = input.ExternalDocumentIds?.ToList() ?? [],
            ScopeDescription = Trim(input.ScopeDescription),
            IssuedByLegalUserId = input.IssuedByLegalUserId,
            IssuedByLegalRole = Trim(input.IssuedByLegalRole),
            EffectiveFrom = input.EffectiveFrom ?? DateTimeOffset.UtcNow,
            EffectiveUntil = input.EffectiveUntil,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        if (!HasUsableScope(hold))
        {
            return Fail(
                "The hold scope is empty. Provide the scope targets, or use GlobalDocumentControl, or describe a CustomQuery scope.",
                400, RetentionReasonCodes.HoldScopeRequired, correlationId);
        }

        await _holds.CreateAsync(hold, ct);
        return Response<LegalHoldModel>.Success(RetentionWire.ToHold(hold), 201, correlationId);
    }

    /// <summary>SOP §22 — a hold goes live only with Legal approval evidence on record.</summary>
    public async Task<Response<LegalHoldModel>> ActivateAsync(Guid id, ActivateLegalHoldInput input, string correlationId, CancellationToken ct)
    {
        var (fail, hold) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (hold!.HoldStatus is LegalHoldStatus.Released or LegalHoldStatus.Cancelled)
        {
            return Fail($"The hold is already {hold.HoldStatus}.", 409, RetentionReasonCodes.HoldAlreadyDecided, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.LegalApprovalEvidenceReference))
        {
            return Fail("Legal approval evidence is required to activate a legal hold.", 409,
                RetentionReasonCodes.HoldLegalApprovalRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        hold.HoldStatus = LegalHoldStatus.Active;
        hold.LegalApprovalEvidenceReference = input.LegalApprovalEvidenceReference.Trim();
        hold.IssuedAt ??= now;
        hold.IssuedByLegalUserId ??= _currentUser.UserId;
        hold.GqdConcurrenceUserId = input.GqdConcurrenceUserId ?? hold.GqdConcurrenceUserId;
        hold.GqdConcurrenceEvidenceReference = Trim(input.GqdConcurrenceEvidenceReference) ?? hold.GqdConcurrenceEvidenceReference;
        if (hold.GqdConcurrenceEvidenceReference is not null)
        {
            hold.GqdConcurrenceAt ??= now;
        }

        Touch(hold, now);
        await _holds.UpdateAsync(hold, ct);
        return Response<LegalHoldModel>.Success(RetentionWire.ToHold(hold), correlationId: correlationId);
    }

    /// <summary>
    /// SOP §22 — release requires Legal written approval AND GQD concurrence. Both references are mandatory;
    /// supplying only one is refused with a distinct reason code so the missing party is unambiguous.
    /// </summary>
    public async Task<Response<LegalHoldModel>> ReleaseAsync(Guid id, ReleaseLegalHoldInput input, string correlationId, CancellationToken ct)
    {
        var (fail, hold) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (hold!.HoldStatus != LegalHoldStatus.Active)
        {
            return Fail($"Only an active hold can be released; this hold is {hold.HoldStatus}.", 409,
                RetentionReasonCodes.HoldNotActive, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.ReleaseLegalApprovalReference))
        {
            return Fail("Legal written release approval is required to release a legal hold.", 409,
                RetentionReasonCodes.HoldReleaseApprovalRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.ReleaseGqdConcurrenceReference))
        {
            return Fail("GQD concurrence is required to release a legal hold.", 409,
                RetentionReasonCodes.HoldReleaseConcurrenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        hold.HoldStatus = LegalHoldStatus.Released;
        hold.ReleaseLegalApprovalReference = input.ReleaseLegalApprovalReference.Trim();
        hold.ReleaseGqdConcurrenceReference = input.ReleaseGqdConcurrenceReference.Trim();
        hold.ReleaseRequestedAt ??= now;
        hold.ReleaseRequestedBy ??= _currentUser.ActorName;
        hold.ReleasedAt = now;
        hold.ReleasedBy = _currentUser.ActorName;
        Touch(hold, now);
        await _holds.UpdateAsync(hold, ct);

        // Membership rows become Released history — never deleted.
        foreach (var membership in (await _holdSubjects.GetByHoldAsync(hold.Id, ct))
                 .Where(m => m.Status == LegalHoldSubjectStatus.Active))
        {
            membership.Status = LegalHoldSubjectStatus.Released;
            membership.HoldReleasedAt = now;
            membership.UpdatedAt = now;
            membership.UpdatedBy = _currentUser.ActorName;
            await _holdSubjects.UpdateAsync(membership, ct);
        }

        return Response<LegalHoldModel>.Success(RetentionWire.ToHold(hold), correlationId: correlationId);
    }

    /// <summary>Enrols a specific regulated record into a hold — the evidence the hold reached that record.</summary>
    public async Task<Response<LegalHoldSubjectModel>> AddSubjectAsync(
        Guid holdId, string subjectTypeRaw, Guid subjectId, Guid? registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var hold = await _holds.GetByIdAsync(holdId, ct);
        if (hold is null)
        {
            return Response<LegalHoldSubjectModel>.Fail("Legal hold not found.", 404, RetentionReasonCodes.HoldNotFound, correlationId);
        }

        var subjectType = RetentionWire.ParseSubjectType(subjectTypeRaw);
        if (subjectType is null || subjectId == Guid.Empty)
        {
            return Response<LegalHoldSubjectModel>.Fail(
                "A valid subject type and subject id are required.", 400, RetentionReasonCodes.ValidationFailed, correlationId);
        }

        // Idempotent: an existing active membership is returned rather than duplicated.
        var existing = (await _holdSubjects.GetByHoldAsync(holdId, ct))
            .FirstOrDefault(m => m.SubjectType == subjectType.Value && m.SubjectId == subjectId && m.Status == LegalHoldSubjectStatus.Active);
        if (existing is not null)
        {
            return Response<LegalHoldSubjectModel>.Success(RetentionWire.ToHoldSubject(existing), correlationId: correlationId);
        }

        var membership = new DocumentLegalHoldSubject
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            LegalHoldId = holdId,
            SubjectType = subjectType.Value,
            SubjectId = subjectId,
            RegisterEntryId = registerEntryId,
            HoldAppliedAt = DateTimeOffset.UtcNow,
            Status = LegalHoldSubjectStatus.Active,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _holdSubjects.CreateAsync(membership, ct);
        return Response<LegalHoldSubjectModel>.Success(RetentionWire.ToHoldSubject(membership), 201, correlationId);
    }

    public async Task<Response<IReadOnlyList<LegalHoldSubjectModel>>> GetSubjectsAsync(Guid holdId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var hold = await _holds.GetByIdAsync(holdId, ct);
        if (hold is null)
        {
            return Response<IReadOnlyList<LegalHoldSubjectModel>>.Fail(
                "Legal hold not found.", 404, RetentionReasonCodes.HoldNotFound, correlationId);
        }

        var rows = await _holdSubjects.GetByHoldAsync(holdId, ct);
        return Response<IReadOnlyList<LegalHoldSubjectModel>>.Success(
            rows.Select(RetentionWire.ToHoldSubject).ToList(), correlationId: correlationId);
    }

    public async Task<Response<LegalHoldModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, hold) = await LoadAsync(id, correlationId, ct);
        return fail ?? Response<LegalHoldModel>.Success(RetentionWire.ToHold(hold!), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<LegalHoldModel>>> ListAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _holds.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<LegalHoldModel>>.Success(
            rows.Select(RetentionWire.ToHold).ToList(), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static bool HasUsableScope(DocumentLegalHold h) => h.ScopeType switch
    {
        LegalHoldScopeType.GlobalDocumentControl => true,
        LegalHoldScopeType.RegisterEntry => h.RegisterEntryIds.Count > 0,
        LegalHoldScopeType.ControlledDocument => h.ControlledDocumentIds.Count > 0,
        LegalHoldScopeType.SubjectType => h.SubjectTypes.Count > 0,
        LegalHoldScopeType.ExternalDocument => h.ExternalDocumentIds.Count > 0,
        // Membership-driven and description-driven scopes need at least a description to be auditable.
        LegalHoldScopeType.Repository or LegalHoldScopeType.CustomQuery => !string.IsNullOrWhiteSpace(h.ScopeDescription),
        _ => false
    };

    private async Task<(Response<LegalHoldModel>? Fail, DocumentLegalHold? Hold)> LoadAsync(Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var hold = await _holds.GetByIdAsync(id, ct);
        return hold is null
            ? (Fail("Legal hold not found.", 404, RetentionReasonCodes.HoldNotFound, correlationId), null)
            : (null, hold);
    }

    private void Touch(DocumentLegalHold h, DateTimeOffset now)
    {
        h.UpdatedAt = now;
        h.UpdatedBy = _currentUser.ActorName;
    }

    private static Response<LegalHoldModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<LegalHoldModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
