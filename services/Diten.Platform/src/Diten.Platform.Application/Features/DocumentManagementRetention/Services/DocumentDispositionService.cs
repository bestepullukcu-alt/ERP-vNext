using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementRetention.Services;

/// <summary>
/// MOD-0029-FU15 — disposition request lifecycle (GMG-QMS-SOP-0001 §22).
///
/// CRITICAL BOUNDARY — READ BEFORE EXTENDING: this service NEVER deletes anything. "Execute" writes an evidence
/// marker (<see cref="DispositionRequestStatus.ExecutedAsNoDeleteMarker"/>) recording that disposition was
/// authorised; the subject record itself is left completely untouched and remains retrievable. There is no purge
/// path, no scheduler and no cascade. Actual destruction is a deliberate future task that would consume these
/// markers as its input, and it must re-verify holds at execution time.
///
/// Guards, each re-checked at every transition (never trusted from an earlier step):
/// • An active legal hold blocks submit, approve and execute.
/// • The subject must have been evaluated and found eligible — an unevaluated subject is refused.
/// • Approval evidence is mandatory before approve/execute.
/// </summary>
public sealed class DocumentDispositionService
{
    private readonly IDocumentDispositionRequestRepository _requests;
    private readonly IDocumentRetentionSubjectRepository _subjects;
    private readonly DocumentLegalHoldEvaluator _holdEvaluator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentDispositionService(
        IDocumentDispositionRequestRepository requests,
        IDocumentRetentionSubjectRepository subjects,
        DocumentLegalHoldEvaluator holdEvaluator,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _requests = requests;
        _subjects = subjects;
        _holdEvaluator = holdEvaluator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<DispositionRequestModel>> CreateAsync(CreateDispositionRequestInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var subjectType = RetentionWire.ParseSubjectType(input.SubjectType);
        if (subjectType is null || input.SubjectId == Guid.Empty)
        {
            return Fail("A valid subject type and subject id are required.", 400, RetentionReasonCodes.ValidationFailed, correlationId);
        }

        var snapshot = await _subjects.GetBySubjectAsync(subjectType.Value, input.SubjectId, ct);
        var now = DateTimeOffset.UtcNow;

        var request = new DocumentDispositionRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequestNumber = $"DISP-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            SubjectType = subjectType.Value,
            SubjectId = input.SubjectId,
            RegisterEntryId = input.RegisterEntryId ?? snapshot?.RegisterEntryId,
            PolicyId = snapshot?.PolicyId,
            RequestStatus = DispositionRequestStatus.Draft,
            EligibilityCheckedAt = snapshot?.LastEvaluatedAt,
            EligibilityResult = MapEligibility(snapshot),
            RequestedBy = _currentUser.ActorName,
            RequestedAt = now,
            Comment = Trim(input.Comment),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        await _requests.CreateAsync(request, ct);
        return Response<DispositionRequestModel>.Success(RetentionWire.ToDisposition(request), 201, correlationId);
    }

    public async Task<Response<DispositionRequestModel>> SubmitAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, request) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (request!.RequestStatus is not (DispositionRequestStatus.Draft or DispositionRequestStatus.BlockedByHold))
        {
            return Fail($"A {request.RequestStatus} request cannot be submitted.", 409, RetentionReasonCodes.DispositionInvalidState, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var snapshot = await _subjects.GetBySubjectAsync(request.SubjectType, request.SubjectId, ct);

        // A hold is re-checked live, never taken from the snapshot alone.
        if (await IsBlockedAsync(request, now, ct))
        {
            return await BlockAsync(request, now, correlationId, ct);
        }

        if (snapshot is null)
        {
            request.EligibilityResult = DispositionEligibilityResult.NotEligible;
            await PersistAsync(request, now, ct);
            return Fail("The subject has not been evaluated for retention; evaluate it before submitting a disposition request.",
                409, RetentionReasonCodes.DispositionNotEvaluated, correlationId);
        }

        request.EligibilityCheckedAt = now;
        request.EligibilityResult = MapEligibility(snapshot);
        request.PolicyId ??= snapshot.PolicyId;

        if (!snapshot.IsDispositionEligible)
        {
            await PersistAsync(request, now, ct);
            return Fail($"The subject is not disposition eligible ({snapshot.EvaluationStatus}).",
                409, RetentionReasonCodes.DispositionNotEligible, correlationId);
        }

        request.RequestStatus = DispositionRequestStatus.Submitted;
        await PersistAsync(request, now, ct);
        return Response<DispositionRequestModel>.Success(RetentionWire.ToDisposition(request), correlationId: correlationId);
    }

    public async Task<Response<DispositionRequestModel>> ApproveAsync(Guid id, ApproveDispositionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, request) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (request!.RequestStatus != DispositionRequestStatus.Submitted)
        {
            return Fail($"Only a submitted request can be approved; this request is {request.RequestStatus}.",
                409, RetentionReasonCodes.DispositionInvalidState, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.ApprovalEvidenceReference))
        {
            return Fail("Approval evidence is required to approve a disposition request.", 409,
                RetentionReasonCodes.DispositionApprovalEvidenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        if (await IsBlockedAsync(request, now, ct))
        {
            return await BlockAsync(request, now, correlationId, ct);
        }

        request.RequestStatus = DispositionRequestStatus.ApprovedForDisposition;
        request.ApprovalEvidenceReference = input.ApprovalEvidenceReference.Trim();
        request.ApprovedBy = _currentUser.ActorName;
        request.ApprovedByUserId = input.ApprovedByUserId ?? _currentUser.UserId;
        request.ApprovedAt = now;
        await PersistAsync(request, now, ct);
        return Response<DispositionRequestModel>.Success(RetentionWire.ToDisposition(request), correlationId: correlationId);
    }

    public async Task<Response<DispositionRequestModel>> RejectAsync(Guid id, RejectDispositionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, request) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A rejection reason is required.", 400, RetentionReasonCodes.ValidationFailed, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        request!.RequestStatus = DispositionRequestStatus.Rejected;
        request.RejectionReason = input.Reason.Trim();
        await PersistAsync(request, now, ct);
        return Response<DispositionRequestModel>.Success(RetentionWire.ToDisposition(request), correlationId: correlationId);
    }

    /// <summary>
    /// Records that disposition was executed — AS A MARKER ONLY. The subject record is not read, not modified and
    /// certainly not deleted by this method. That is the entire point of the FU15 boundary.
    /// </summary>
    public async Task<Response<DispositionRequestModel>> ExecuteMarkerAsync(
        Guid id, ExecuteDispositionMarkerInput input, string correlationId, CancellationToken ct)
    {
        var (fail, request) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (request!.RequestStatus != DispositionRequestStatus.ApprovedForDisposition)
        {
            return Fail($"Only an approved request can be executed; this request is {request.RequestStatus}.",
                409, RetentionReasonCodes.DispositionInvalidState, correlationId);
        }

        if (string.IsNullOrWhiteSpace(request.ApprovalEvidenceReference))
        {
            return Fail("Approval evidence is required before a disposition can be executed.", 409,
                RetentionReasonCodes.DispositionApprovalEvidenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        if (await IsBlockedAsync(request, now, ct))
        {
            return await BlockAsync(request, now, correlationId, ct);
        }

        request.RequestStatus = DispositionRequestStatus.ExecutedAsNoDeleteMarker;
        request.ExecutionEvidenceReference = Trim(input.ExecutionEvidenceReference);
        request.ExecutedAt = now;
        request.ExecutedBy = _currentUser.ActorName;
        await PersistAsync(request, now, ct);
        return Response<DispositionRequestModel>.Success(RetentionWire.ToDisposition(request), correlationId: correlationId);
    }

    public async Task<Response<DispositionRequestModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, request) = await LoadAsync(id, correlationId, ct);
        return fail ?? Response<DispositionRequestModel>.Success(RetentionWire.ToDisposition(request!), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<DispositionRequestModel>>> ListAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _requests.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<DispositionRequestModel>>.Success(
            rows.Select(RetentionWire.ToDisposition).ToList(), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<bool> IsBlockedAsync(DocumentDispositionRequest r, DateTimeOffset now, CancellationToken ct) =>
        (await _holdEvaluator.GetBlockingHoldsAsync(r.SubjectType, r.SubjectId, r.RegisterEntryId, null, now, ct)).Count > 0;

    private async Task<Response<DispositionRequestModel>> BlockAsync(
        DocumentDispositionRequest r, DateTimeOffset now, string correlationId, CancellationToken ct)
    {
        r.RequestStatus = DispositionRequestStatus.BlockedByHold;
        r.EligibilityResult = DispositionEligibilityResult.BlockedByHold;
        r.EligibilityCheckedAt = now;
        await PersistAsync(r, now, ct);
        return Fail("An active legal hold blocks disposition of this record.", 409,
            RetentionReasonCodes.DispositionBlockedByHold, correlationId);
    }

    private static DispositionEligibilityResult MapEligibility(DocumentRetentionSubject? s) => s?.EvaluationStatus switch
    {
        RetentionEvaluationStatus.Eligible => DispositionEligibilityResult.Eligible,
        RetentionEvaluationStatus.BlockedByHold => DispositionEligibilityResult.BlockedByHold,
        RetentionEvaluationStatus.MissingPolicy => DispositionEligibilityResult.MissingPolicy,
        RetentionEvaluationStatus.MissingTriggerDate => DispositionEligibilityResult.MissingTriggerDate,
        _ => DispositionEligibilityResult.NotEligible
    };

    private async Task PersistAsync(DocumentDispositionRequest r, DateTimeOffset now, CancellationToken ct)
    {
        r.UpdatedAt = now;
        r.UpdatedBy = _currentUser.ActorName;
        await _requests.UpdateAsync(r, ct);
    }

    private async Task<(Response<DispositionRequestModel>? Fail, DocumentDispositionRequest? Request)> LoadAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var request = await _requests.GetByIdAsync(id, ct);
        return request is null
            ? (Fail("Disposition request not found.", 404, RetentionReasonCodes.DispositionNotFound, correlationId), null)
            : (null, request);
    }

    private static Response<DispositionRequestModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<DispositionRequestModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
