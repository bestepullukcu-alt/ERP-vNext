using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Services;

/// <summary>
/// MOD-0029-FU21 — records and reviews GDocP correction trail entries (GMG-QMS-SOP-0001 §21).
///
/// WHAT THIS DOES NOT DO: it does not mutate the corrected aggregate. The field change stays with the owning
/// feature's update command; FU21 records that the change happened, from what, to what, why, and on whose
/// authority. That separation is what lets FU21 be added without rewriting FU06–FU20.
///
/// SOP controls enforced here:
/// • A reason is always mandatory — there is no silent correction path.
/// • <c>CorrectedAt</c> is stamped server-side. The input record has no field for it, so backdating the
///   correction itself is structurally impossible rather than merely validated against.
/// • Server-owned fields (Id, TenantId, CorrectedAt, WrittenAtUtc) cannot be the target of a correction at all.
/// • A previous value that cannot be established becomes an EXPLICIT sentinel, never a blank — a blank previous
///   value is indistinguishable from a lost one.
/// • An oversized snapshot is REFUSED rather than truncated: truncating the previous value would destroy the very
///   evidence the trail exists to preserve.
/// • High-risk corrections (reconstruction, data integrity, status, evidence swap, backdating) require a
///   deviation reference and route to second-person review.
/// • A decided review is final; it can never be re-reviewed into a different verdict.
///
/// Nothing here is ever hard-deleted, and the recorded values/reason/timestamp are never rewritten by a review.
/// </summary>
public sealed class DocumentGDocPCorrectionService : IGDocPCorrectionRecorder
{
    private readonly IDocumentGDocPCorrectionRecordRepository _records;
    private readonly IDocumentGDocPCorrectionReviewRepository _reviews;
    private readonly DocumentGDocPCorrectionEvaluator _evaluator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentGDocPCorrectionService(
        IDocumentGDocPCorrectionRecordRepository records,
        IDocumentGDocPCorrectionReviewRepository reviews,
        DocumentGDocPCorrectionEvaluator evaluator,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _records = records;
        _reviews = reviews;
        _evaluator = evaluator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    // ── record ────────────────────────────────────────────────────────────────

    public async Task<Response<GDocPCorrectionRecordModel>> RecordCorrectionAsync(
        RecordGDocPCorrectionInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        var subjectType = GDocPCorrectionWire.ParseSubjectType(input.SubjectType);
        if (subjectType is null || input.SubjectId == Guid.Empty)
        {
            return Fail("A valid subject type and subject id are required.", 400,
                GDocPCorrectionReasonCodes.SubjectRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.FieldPath))
        {
            return Fail("A field path is required — a correction must say WHICH field changed.", 400,
                GDocPCorrectionReasonCodes.FieldPathRequired, correlationId);
        }

        var fieldPath = input.FieldPath.Trim();

        // The server's own attestation of when things happened is not correctable by anyone.
        if (DocumentGDocPCorrectionEvaluator.IsImmutableServerField(fieldPath))
        {
            return Fail($"'{fieldPath}' is a server-owned immutable field and cannot be corrected.", 409,
                GDocPCorrectionReasonCodes.ServerTimestampImmutable, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.CorrectionReason))
        {
            return Fail("A correction reason is required.", 400,
                GDocPCorrectionReasonCodes.ReasonRequired, correlationId);
        }

        var valueFormat = GDocPCorrectionWire.ParseValueFormat(input.ValueFormat);
        var correctionType = GDocPCorrectionWire.ParseCorrectionType(input.CorrectionType);

        var (previousValue, newValue) = NormalizeSnapshots(input, valueFormat);

        // Truncation would destroy the evidence, so an oversized snapshot is refused outright.
        if (previousValue.Length > GDocPCorrectionWire.MaxSnapshotLength || newValue.Length > GDocPCorrectionWire.MaxSnapshotLength)
        {
            return Fail(
                $"A value snapshot exceeds {GDocPCorrectionWire.MaxSnapshotLength} characters. Record a reference or a redacted summary instead — snapshots are never truncated.",
                400, GDocPCorrectionReasonCodes.SnapshotTooLarge, correlationId);
        }

        // PRODUCT DECISION: an unchanged value is REFUSED, not silently accepted as a no-op. A trail entry
        // asserting "corrected X to X" is a false record, and a silent success would let the caller believe a
        // correction was logged when nothing changed.
        if (string.Equals(previousValue, newValue, StringComparison.Ordinal)
            && valueFormat != GDocPValueFormat.Redacted)
        {
            return Fail("The previous and new values are identical; there is nothing to correct.", 409,
                GDocPCorrectionReasonCodes.NoChange, correlationId);
        }

        var requirements = await _evaluator.EvaluateAsync(subjectType.Value, fieldPath, previousValue, newValue, correctionType, ct);

        if (input.SubjectIsEffective && !requirements.AllowCorrectionAfterEffective)
        {
            return Fail("An active policy does not permit correcting this field once the record is effective.", 409,
                GDocPCorrectionReasonCodes.CorrectionNotAllowedAfterEffective, correlationId);
        }

        if (input.SubjectIsApproved && !requirements.AllowCorrectionAfterApproval)
        {
            return Fail("An active policy does not permit correcting this field once the record is approved.", 409,
                GDocPCorrectionReasonCodes.CorrectionNotAllowedAfterApproval, correlationId);
        }

        var evidence = Trim(input.CorrectionEvidenceReference);
        if (requirements.RequiresEvidenceReference && evidence is null)
        {
            return Fail(
                correctionType is GDocPCorrectionType.Reconstruction or GDocPCorrectionType.DataIntegrityCorrection
                    ? "A reconstruction / data-integrity correction requires an evidence reference."
                    : "An evidence reference is required for this correction.",
                409,
                correctionType is GDocPCorrectionType.Reconstruction or GDocPCorrectionType.DataIntegrityCorrection
                    ? GDocPCorrectionReasonCodes.ReconstructionRequiresEvidence
                    : GDocPCorrectionReasonCodes.EvidenceRequired,
                correlationId);
        }

        var deviation = Trim(input.DeviationReference);
        if (requirements.RequiresDeviationReference && deviation is null)
        {
            return Fail(
                requirements.IsBackdating
                    ? "Moving a regulated timestamp earlier is backdating and requires a deviation reference."
                    : "This high-risk correction requires a deviation reference.",
                409,
                requirements.IsBackdating
                    ? GDocPCorrectionReasonCodes.BackdatingRequiresDeviation
                    : GDocPCorrectionReasonCodes.HighRiskRequiresDeviation,
                correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var record = new DocumentGDocPCorrectionRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CorrectionNumber = $"GDC-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            SubjectType = subjectType.Value,
            SubjectId = input.SubjectId,
            RegisterEntryId = input.RegisterEntryId,
            ControlledDocumentId = input.ControlledDocumentId,
            FieldPath = fieldPath,
            FieldDisplayName = Trim(input.FieldDisplayName),
            PreviousValueSnapshot = previousValue,
            NewValueSnapshot = newValue,
            ValueFormat = valueFormat,
            CorrectionType = correctionType,
            CorrectionReason = input.CorrectionReason.Trim(),
            CorrectionEvidenceReference = evidence,
            IsHighRiskCorrection = requirements.IsHighRisk,
            RequiresDeviationReference = requirements.RequiresDeviationReference,
            DeviationReference = deviation,
            IsBackdatingCorrection = requirements.IsBackdating,
            RiskAssessmentNote = requirements.RiskAssessmentNote,
            CorrectedByUserId = input.CorrectedByUserId ?? _currentUser.UserId,
            CorrectedByRole = Trim(input.CorrectedByRole),

            // SERVER-STAMPED. There is no input field for this — backdating the correction is structurally impossible.
            CorrectedAt = now,

            RequestedBy = Trim(input.RequestedBy),
            RequestedAt = input.RequestedBy is null ? null : now,

            // NotRequired is deliberately distinct from Reviewed: nobody has looked at it.
            ReviewStatus = requirements.RequiresReview ? GDocPReviewStatus.PendingReview : GDocPReviewStatus.NotRequired,

            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        await _records.CreateAsync(record, ct);
        return Response<GDocPCorrectionRecordModel>.Success(GDocPCorrectionWire.ToRecord(record), 201, correlationId);
    }

    // ── review ────────────────────────────────────────────────────────────────

    public async Task<Response<GDocPCorrectionRecordModel>> ReviewAsync(
        Guid id, ReviewGDocPCorrectionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, record) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (record!.IsReviewDecided())
        {
            return Fail($"The correction has already been {record.ReviewStatus}; a review decision is final.", 409,
                GDocPCorrectionReasonCodes.AlreadyReviewed, correlationId);
        }

        if (input.ReviewerUserId is null && string.IsNullOrWhiteSpace(input.ReviewerRole))
        {
            return Fail("A named reviewer (user or role) is required.", 400,
                GDocPCorrectionReasonCodes.ReviewerRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.ReviewEvidenceReference))
        {
            return Fail("Review evidence is required to approve a correction.", 400,
                GDocPCorrectionReasonCodes.ReviewEvidenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        await AppendReviewAsync(record, GDocPReviewDecision.Approved, input.ReviewerUserId, input.ReviewerRole,
            input.ReviewEvidenceReference, input.ReviewComment, now, correlationId, ct);

        record.ReviewStatus = GDocPReviewStatus.Reviewed;
        record.ReviewedBy = _currentUser.ActorName;
        record.ReviewedByUserId = input.ReviewerUserId ?? _currentUser.UserId;
        record.ReviewedAt = now;
        record.ReviewEvidenceReference = input.ReviewEvidenceReference.Trim();
        record.ReviewComment = Trim(input.ReviewComment);
        await PersistReviewAsync(record, now, ct);

        return Response<GDocPCorrectionRecordModel>.Success(GDocPCorrectionWire.ToRecord(record), correlationId: correlationId);
    }

    public async Task<Response<GDocPCorrectionRecordModel>> RejectAsync(
        Guid id, RejectGDocPCorrectionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, record) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (record!.IsReviewDecided())
        {
            return Fail($"The correction has already been {record.ReviewStatus}; a review decision is final.", 409,
                GDocPCorrectionReasonCodes.AlreadyReviewed, correlationId);
        }

        if (input.ReviewerUserId is null && string.IsNullOrWhiteSpace(input.ReviewerRole))
        {
            return Fail("A named reviewer (user or role) is required.", 400,
                GDocPCorrectionReasonCodes.ReviewerRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A rejection reason is required.", 400,
                GDocPCorrectionReasonCodes.ReviewReasonRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        await AppendReviewAsync(record, GDocPReviewDecision.Rejected, input.ReviewerUserId, input.ReviewerRole,
            null, input.Reason, now, correlationId, ct);

        record.ReviewStatus = GDocPReviewStatus.Rejected;
        record.ReviewedBy = _currentUser.ActorName;
        record.ReviewedByUserId = input.ReviewerUserId ?? _currentUser.UserId;
        record.ReviewedAt = now;
        record.ReviewComment = input.Reason.Trim();
        await PersistReviewAsync(record, now, ct);

        return Response<GDocPCorrectionRecordModel>.Success(GDocPCorrectionWire.ToRecord(record), correlationId: correlationId);
    }

    // ── reads ─────────────────────────────────────────────────────────────────

    public async Task<Response<GDocPCorrectionRecordModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, record) = await LoadAsync(id, correlationId, ct);
        return fail ?? Response<GDocPCorrectionRecordModel>.Success(
            GDocPCorrectionWire.ToRecord(record!), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<GDocPCorrectionRecordModel>>> ListAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _records.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<GDocPCorrectionRecordModel>>.Success(
            rows.Select(GDocPCorrectionWire.ToRecord).ToList(), correlationId: correlationId);
    }

    /// <summary>The full correction history of one regulated record — the GDocP question an auditor asks.</summary>
    public async Task<Response<IReadOnlyList<GDocPCorrectionRecordModel>>> GetBySubjectAsync(
        string subjectTypeRaw, Guid subjectId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var subjectType = GDocPCorrectionWire.ParseSubjectType(subjectTypeRaw);
        if (subjectType is null)
        {
            return Response<IReadOnlyList<GDocPCorrectionRecordModel>>.Fail(
                "A valid subject type is required.", 400, GDocPCorrectionReasonCodes.SubjectRequired, correlationId);
        }

        var rows = await _records.GetBySubjectAsync(subjectType.Value, subjectId, ct);
        return Response<IReadOnlyList<GDocPCorrectionRecordModel>>.Success(
            rows.Select(GDocPCorrectionWire.ToRecord).ToList(), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<GDocPCorrectionReviewModel>>> GetReviewsAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, _) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return Response<IReadOnlyList<GDocPCorrectionReviewModel>>.Fail(
                "Correction record not found.", 404, GDocPCorrectionReasonCodes.CorrectionNotFound, correlationId);
        }

        var rows = await _reviews.GetByCorrectionAsync(id, ct);
        return Response<IReadOnlyList<GDocPCorrectionReviewModel>>.Success(
            rows.Select(GDocPCorrectionWire.ToReview).ToList(), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<GDocPCorrectionRecordModel>>> GetPendingReviewAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _records.GetPendingReviewAsync(ct);
        return Response<IReadOnlyList<GDocPCorrectionRecordModel>>.Success(
            rows.Select(GDocPCorrectionWire.ToRecord).ToList(), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Normalizes the value snapshots so the trail is never ambiguous: a missing previous value becomes the
    /// explicit UNKNOWN sentinel (which the evaluator then treats as high risk), and a redacted value carries an
    /// explicit marker rather than a blank.
    /// </summary>
    private static (string Previous, string New) NormalizeSnapshots(RecordGDocPCorrectionInput input, GDocPValueFormat format)
    {
        if (format == GDocPValueFormat.Redacted)
        {
            return (
                string.IsNullOrWhiteSpace(input.PreviousValueSnapshot)
                    ? DocumentGDocPCorrectionRecord.RedactedMarker
                    : input.PreviousValueSnapshot.Trim(),
                string.IsNullOrWhiteSpace(input.NewValueSnapshot)
                    ? DocumentGDocPCorrectionRecord.RedactedMarker
                    : input.NewValueSnapshot.Trim());
        }

        var previous = string.IsNullOrWhiteSpace(input.PreviousValueSnapshot)
            ? DocumentGDocPCorrectionRecord.UnknownPreviousValue
            : input.PreviousValueSnapshot.Trim();

        // An empty NEW value is a legitimate correction (clearing a field); it is recorded as an explicit marker.
        var updated = input.NewValueSnapshot?.Trim() ?? string.Empty;
        return (previous, updated.Length == 0 ? "[EMPTY]" : updated);
    }

    private async Task AppendReviewAsync(
        DocumentGDocPCorrectionRecord record,
        GDocPReviewDecision decision,
        Guid? reviewerUserId,
        string? reviewerRole,
        string? evidenceReference,
        string? comment,
        DateTimeOffset now,
        string correlationId,
        CancellationToken ct) =>
        await _reviews.CreateAsync(new DocumentGDocPCorrectionReview
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            CorrectionRecordId = record.Id,
            ReviewDecision = decision,
            ReviewerUserId = reviewerUserId ?? _currentUser.UserId,
            ReviewerRole = Trim(reviewerRole),
            ReviewerName = _currentUser.ActorName,
            ReviewEvidenceReference = Trim(evidenceReference),
            ReviewComment = Trim(comment),
            ReviewedAt = now,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        }, ct);

    private async Task PersistReviewAsync(DocumentGDocPCorrectionRecord record, DateTimeOffset now, CancellationToken ct)
    {
        record.UpdatedAt = now;
        record.UpdatedBy = _currentUser.ActorName;
        await _records.UpdateReviewAsync(record, ct);
    }

    private async Task<(Response<GDocPCorrectionRecordModel>? Fail, DocumentGDocPCorrectionRecord? Record)> LoadAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var record = await _records.GetByIdAsync(id, ct);
        return record is null
            ? (Fail("Correction record not found.", 404, GDocPCorrectionReasonCodes.CorrectionNotFound, correlationId), null)
            : (null, record);
    }

    private static Response<GDocPCorrectionRecordModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<GDocPCorrectionRecordModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
