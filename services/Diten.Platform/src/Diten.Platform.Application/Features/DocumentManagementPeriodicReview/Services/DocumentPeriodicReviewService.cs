using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Services;

/// <summary>
/// MOD-0029-FU12 — periodic review / extension / overdue orchestration (GMG-QMS-SOP-0001 §9.15, §15). Governs the
/// review cycle: initiate 60 days before due, complete BY the due date, or formally extend BEFORE it — ONE extension,
/// max 60 days, with a documented risk assessment (GQD for Critical, plus Management Review escalation). An extension
/// applied for after the due date is not an extension: the review is overdue. There is NO tolerance band for an
/// overdue Critical review — it raises a GQD determination escalation.
///
/// This engine RECORDS governance and RAISES escalations; it never silently changes the document lifecycle (that stays
/// with the FU08 engine) and never performs a suspension (FU13). No hard delete; no second extension.
/// </summary>
public sealed class DocumentPeriodicReviewService
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IDocumentPeriodicReviewRepository _reviews;
    private readonly IDocumentPeriodicReviewExtensionRepository _extensions;
    private readonly IDocumentPeriodicReviewEscalationRepository _escalations;
    private readonly DocumentPeriodicReviewStatusEvaluator _status;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly DocumentPeriodicReviewOptions _options;

    public DocumentPeriodicReviewService(
        IDocumentMasterRegisterRepository register,
        IDocumentPeriodicReviewRepository reviews,
        IDocumentPeriodicReviewExtensionRepository extensions,
        IDocumentPeriodicReviewEscalationRepository escalations,
        DocumentPeriodicReviewStatusEvaluator status,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        IOptions<DocumentPeriodicReviewOptions> options)
    {
        _register = register;
        _reviews = reviews;
        _extensions = extensions;
        _escalations = escalations;
        _status = status;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _options = options.Value;
    }

    // ── schedule ──────────────────────────────────────────────────────────────

    public async Task<Response<PeriodicReviewScheduleModel>> GetScheduleAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return FailSchedule("Register entry not found.", 404, PeriodicReviewReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var schedule = await BuildScheduleAsync(entry, ct);
        return Response<PeriodicReviewScheduleModel>.Success(schedule, correlationId: correlationId);
    }

    // ── initiate ──────────────────────────────────────────────────────────────

    public async Task<Response<PeriodicReviewModel>> InitiateAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return FailReview("Register entry not found.", 404, PeriodicReviewReasonCodes.NotFoundNonLeakage, correlationId);
        }

        if (!DocumentReviewCycleCalculator.IsScheduledForReview(entry))
        {
            return FailReview($"A {entry.LifecycleStatus} document is not scheduled for periodic review.", 409, PeriodicReviewReasonCodes.NotScheduledForReview, correlationId);
        }

        // Idempotent: an already-open review is returned as-is.
        var open = await _reviews.GetOpenAsync(registerEntryId, ct);
        if (open is not null)
        {
            return Response<PeriodicReviewModel>.Success(PeriodicReviewWire.ToReview(open), correlationId: correlationId);
        }

        var due = DocumentReviewCycleCalculator.CurrentDueDate(entry);
        if (due is null)
        {
            return FailReview("The document has no effective date or last-review date to schedule a review from.", 409, PeriodicReviewReasonCodes.ScheduleIncomplete, correlationId);
        }

        var history = await _reviews.GetByRegisterEntryAsync(registerEntryId, ct);
        var review = new DocumentPeriodicReview
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            ReviewNumber = history.Count + 1,
            ReviewStatus = PeriodicReviewStatus.Initiated,
            ReviewDueDate = due.Value,
            InitiationWindowStartDate = DocumentReviewCycleCalculator.InitiationWindowStart(due.Value, _options.InitiationWindowDays),
            InitiatedAt = DateTimeOffset.UtcNow,
            InitiatedBy = _currentUser.ActorName,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _reviews.CreateAsync(review, ct);

        // Persist the computed due date on the register so the schedule is stable.
        if (entry.NextReviewDueDate is null)
        {
            entry.NextReviewDueDate = due.Value;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            entry.UpdatedBy = _currentUser.ActorName;
            await _register.UpdateAsync(entry, ct);
        }

        return Response<PeriodicReviewModel>.Success(PeriodicReviewWire.ToReview(review), 201, correlationId);
    }

    // ── complete ──────────────────────────────────────────────────────────────

    public async Task<Response<PeriodicReviewModel>> CompleteAsync(Guid registerEntryId, Guid reviewId, CompletePeriodicReviewInput input, string correlationId, CancellationToken ct)
    {
        var (fail, entry, review) = await LoadAsync(registerEntryId, reviewId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        var decision = PeriodicReviewWire.ParseDecision(input.Decision);
        if (decision is null)
        {
            return FailReview("A valid review decision is required.", 400, PeriodicReviewReasonCodes.ValidationFailed, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.ReviewEvidenceReference))
        {
            return FailReview("A review evidence reference is required.", 400, PeriodicReviewReasonCodes.EvidenceRequired, correlationId);
        }

        // SOP §9.15: an impact assessment is required for a Critical document and for any decision that changes the
        // document's standing.
        var needsImpact = entry!.Criticality == DocumentCriticality.Critical
            || decision is PeriodicReviewDecision.Revise or PeriodicReviewDecision.Retire or PeriodicReviewDecision.Suspend;
        if (needsImpact && string.IsNullOrWhiteSpace(input.ImpactAssessmentReference))
        {
            return FailReview("A documented impact assessment is required for this review decision.", 400, PeriodicReviewReasonCodes.ImpactAssessmentRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        review!.ReviewStatus = PeriodicReviewStatus.Completed;
        review.ReviewDecision = decision.Value;
        review.ReviewEvidenceReference = input.ReviewEvidenceReference.Trim();
        review.ImpactAssessmentReference = TrimOrNull(input.ImpactAssessmentReference);
        review.Comment = TrimOrNull(input.Comment);
        review.CompletedAt = now;
        review.CompletedBy = _currentUser.ActorName;
        review.UpdatedAt = now;
        review.UpdatedBy = _currentUser.ActorName;
        await _reviews.UpdateAsync(review, ct);

        entry.LastPeriodicReviewDate = now;
        entry.NextReviewDueDate = DocumentReviewCycleCalculator.NextDueDateAfterCompletion(entry, now);
        entry.UpdatedAt = now;
        entry.UpdatedBy = _currentUser.ActorName;
        await _register.UpdateAsync(entry, ct);

        // A Suspend decision RAISES a GQD determination — the suspension itself is FU13.
        if (decision == PeriodicReviewDecision.Suspend)
        {
            await RaiseEscalationAsync(entry, review, ReviewEscalationType.GqdDeterminationRequired, ReviewEscalationSeverity.Critical,
                ReviewEscalationRole.GQD, "Periodic review decided SUSPEND — GQD determination and suspension flow required.", correlationId, ct);
        }

        return Response<PeriodicReviewModel>.Success(PeriodicReviewWire.ToReview(review), correlationId: correlationId);
    }

    // ── extension ─────────────────────────────────────────────────────────────

    public async Task<Response<PeriodicReviewExtensionModel>> RequestExtensionAsync(
        Guid registerEntryId, Guid reviewId, RequestPeriodicReviewExtensionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, entry, review) = await LoadAsync(registerEntryId, reviewId, correlationId, ct);
        if (fail is not null)
        {
            return Response<PeriodicReviewExtensionModel>.Fail(fail.Errors, fail.StatusCode, fail.ReasonCode, correlationId);
        }

        if (review!.ReviewStatus == PeriodicReviewStatus.Completed)
        {
            return FailExtension("The review is already completed.", 409, PeriodicReviewReasonCodes.ValidationFailed, correlationId);
        }

        // ONE extension only — a requested/approved/expired extension has already used the single allowance.
        var existing = await _extensions.GetByReviewAsync(reviewId, ct);
        if (existing.Any(x => x.Status is PeriodicReviewExtensionStatus.Requested or PeriodicReviewExtensionStatus.Approved or PeriodicReviewExtensionStatus.Expired))
        {
            return FailExtension("Only one extension is permitted per review; no second extension is allowed (SOP §9.15).", 409, PeriodicReviewReasonCodes.ExtensionAlreadyUsed, correlationId);
        }

        // An extension applied for after the due date is not an extension — the review is overdue.
        var now = DateTimeOffset.UtcNow;
        if (now > review.ReviewDueDate)
        {
            return FailExtension("The due date has passed; this is not an extension — the review is OVERDUE (SOP §9.15).", 409, PeriodicReviewReasonCodes.ReviewAlreadyOverdue, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.RiskAssessmentReference))
        {
            return FailExtension("A documented risk assessment of continued use of the current version is required.", 400, PeriodicReviewReasonCodes.RiskAssessmentRequired, correlationId);
        }

        if (input.ExtensionDays <= 0 || input.ExtensionDays > _options.MaxExtensionDays)
        {
            return FailExtension($"An extension may be at most {_options.MaxExtensionDays} calendar days.", 409, PeriodicReviewReasonCodes.ExtensionTooLong, correlationId);
        }

        var extension = new DocumentPeriodicReviewExtension
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            PeriodicReviewId = reviewId,
            ExtensionNumber = 1,
            RequestedAt = now,
            RequestedBy = _currentUser.ActorName,
            OriginalDueDate = review.ReviewDueDate,
            ExtendedDueDate = review.ReviewDueDate.AddDays(input.ExtensionDays),
            ExtensionDays = input.ExtensionDays,
            RiskAssessmentReference = input.RiskAssessmentReference.Trim(),
            Justification = TrimOrNull(input.Justification),
            Status = PeriodicReviewExtensionStatus.Requested,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _extensions.CreateAsync(extension, ct);
        return Response<PeriodicReviewExtensionModel>.Success(PeriodicReviewWire.ToExtension(extension), 201, correlationId);
    }

    public async Task<Response<PeriodicReviewExtensionModel>> ApproveExtensionAsync(
        Guid registerEntryId, Guid reviewId, Guid extensionId, ApprovePeriodicReviewExtensionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, entry, review) = await LoadAsync(registerEntryId, reviewId, correlationId, ct);
        if (fail is not null)
        {
            return Response<PeriodicReviewExtensionModel>.Fail(fail.Errors, fail.StatusCode, fail.ReasonCode, correlationId);
        }

        var extension = await _extensions.GetByIdAsync(extensionId, ct);
        if (extension is null || extension.PeriodicReviewId != reviewId)
        {
            return FailExtension("Extension not found.", 404, PeriodicReviewReasonCodes.ExtensionNotFound, correlationId);
        }

        if (extension.Status != PeriodicReviewExtensionStatus.Requested)
        {
            return FailExtension($"Only a requested extension can be approved (current status: {extension.Status}).", 409, PeriodicReviewReasonCodes.ValidationFailed, correlationId);
        }

        // The extension must be APPROVED before the original due date (SOP §9.15).
        var now = DateTimeOffset.UtcNow;
        if (now > extension.OriginalDueDate)
        {
            return FailExtension("An extension cannot be approved after the due date has passed; the review is OVERDUE.", 409, PeriodicReviewReasonCodes.ReviewAlreadyOverdue, correlationId);
        }

        var role = PeriodicReviewWire.ParseRole(input.ApproverRole);
        if (role is null)
        {
            return FailExtension("A valid approver role is required.", 400, PeriodicReviewReasonCodes.ValidationFailed, correlationId);
        }

        if (entry!.Criticality == DocumentCriticality.Critical && role != ReviewEscalationRole.GQD)
        {
            return FailExtension("A Critical document extension must be approved by the GQD (SOP §9.15).", 409, PeriodicReviewReasonCodes.GqdApprovalRequired, correlationId);
        }

        extension.Status = PeriodicReviewExtensionStatus.Approved;
        extension.ApprovedAt = now;
        extension.ApprovedBy = _currentUser.ActorName;
        extension.ApproverRole = role.Value;
        extension.ManagementReviewEscalated = entry.Criticality == DocumentCriticality.Critical || input.ManagementReviewEscalated;
        extension.UpdatedAt = now;
        extension.UpdatedBy = _currentUser.ActorName;
        await _extensions.UpdateAsync(extension, ct);

        // The approved extension moves the due date.
        review!.ReviewDueDate = extension.ExtendedDueDate;
        review.ReviewStatus = PeriodicReviewStatus.Extended;
        review.UpdatedAt = now;
        review.UpdatedBy = _currentUser.ActorName;
        await _reviews.UpdateAsync(review, ct);

        entry.NextReviewDueDate = extension.ExtendedDueDate;
        entry.UpdatedAt = now;
        entry.UpdatedBy = _currentUser.ActorName;
        await _register.UpdateAsync(entry, ct);

        // Extending a Critical document is escalated to Management Review (SOP §9.15).
        if (entry.Criticality == DocumentCriticality.Critical)
        {
            await RaiseEscalationAsync(entry, review, ReviewEscalationType.ManagementReview, ReviewEscalationSeverity.Major,
                ReviewEscalationRole.ManagementReview,
                $"Critical document periodic review extended by {extension.ExtensionDays} days — Management Review escalation required.", correlationId, ct);
        }

        return Response<PeriodicReviewExtensionModel>.Success(PeriodicReviewWire.ToExtension(extension), correlationId: correlationId);
    }

    public async Task<Response<PeriodicReviewExtensionModel>> RejectExtensionAsync(
        Guid registerEntryId, Guid reviewId, Guid extensionId, RejectPeriodicReviewExtensionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, _, _) = await LoadAsync(registerEntryId, reviewId, correlationId, ct);
        if (fail is not null)
        {
            return Response<PeriodicReviewExtensionModel>.Fail(fail.Errors, fail.StatusCode, fail.ReasonCode, correlationId);
        }

        var extension = await _extensions.GetByIdAsync(extensionId, ct);
        if (extension is null || extension.PeriodicReviewId != reviewId)
        {
            return FailExtension("Extension not found.", 404, PeriodicReviewReasonCodes.ExtensionNotFound, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return FailExtension("A rejection reason is required.", 400, PeriodicReviewReasonCodes.ReasonRequired, correlationId);
        }

        extension.Status = PeriodicReviewExtensionStatus.Rejected;
        extension.RejectionReason = input.Reason.Trim();
        extension.UpdatedAt = DateTimeOffset.UtcNow;
        extension.UpdatedBy = _currentUser.ActorName;
        await _extensions.UpdateAsync(extension, ct);

        return Response<PeriodicReviewExtensionModel>.Success(PeriodicReviewWire.ToExtension(extension), correlationId: correlationId);
    }

    // ── overdue ───────────────────────────────────────────────────────────────

    public async Task<Response<PeriodicReviewScheduleModel>> EvaluateOverdueAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return FailSchedule("Register entry not found.", 404, PeriodicReviewReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var review = await _reviews.GetOpenAsync(registerEntryId, ct);
        var now = DateTimeOffset.UtcNow;

        if (review is not null && now > review.ReviewDueDate && review.ReviewStatus != PeriodicReviewStatus.Completed)
        {
            var extensions = await _extensions.GetByReviewAsync(review.Id, ct);
            var approved = extensions.FirstOrDefault(x => x.Status == PeriodicReviewExtensionStatus.Approved);

            if (review.ReviewStatus != PeriodicReviewStatus.Overdue)
            {
                review.ReviewStatus = PeriodicReviewStatus.Overdue;
                review.UpdatedAt = now;
                review.UpdatedBy = _currentUser.ActorName;
                await _reviews.UpdateAsync(review, ct);
            }

            if (approved is not null && now > approved.ExtendedDueDate)
            {
                // An approved extension expired without completion → GQD determination (SOP §9.15). No second extension.
                approved.Status = PeriodicReviewExtensionStatus.Expired;
                approved.UpdatedAt = now;
                approved.UpdatedBy = _currentUser.ActorName;
                await _extensions.UpdateAsync(approved, ct);

                await RaiseEscalationAsync(entry, review, ReviewEscalationType.ExtensionExpired,
                    entry.Criticality == DocumentCriticality.Critical ? ReviewEscalationSeverity.Critical : ReviewEscalationSeverity.Major,
                    ReviewEscalationRole.GQD,
                    "The approved extension expired without the review completing — the GQD shall determine, on a documented impact assessment, whether the document remains Effective or is Suspended.",
                    correlationId, ct);
            }
            else
            {
                // Overdue without an extension. There is NO tolerance band for a Critical review.
                await RaiseEscalationAsync(entry, review, ReviewEscalationType.OverdueCritical,
                    entry.Criticality switch
                    {
                        DocumentCriticality.Critical => ReviewEscalationSeverity.Critical,
                        DocumentCriticality.Major => ReviewEscalationSeverity.Major,
                        _ => ReviewEscalationSeverity.Warning
                    },
                    entry.Criticality == DocumentCriticality.Critical ? ReviewEscalationRole.GQD : ReviewEscalationRole.QADocumentation,
                    entry.Criticality == DocumentCriticality.Critical
                        ? "Critical periodic review is OVERDUE — no tolerance band; immediate GQD determination required."
                        : "Periodic review is overdue.",
                    correlationId, ct);
            }
        }

        return Response<PeriodicReviewScheduleModel>.Success(await BuildScheduleAsync(entry, ct), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<PeriodicReviewEscalationModel>>> GetEscalationsAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<IReadOnlyList<PeriodicReviewEscalationModel>>.Fail("Register entry not found.", 404, PeriodicReviewReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var rows = await _escalations.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<IReadOnlyList<PeriodicReviewEscalationModel>>.Success(rows.Select(PeriodicReviewWire.ToEscalation).ToList(), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<PeriodicReviewScheduleModel> BuildScheduleAsync(DocumentMasterRegisterEntry entry, CancellationToken ct)
    {
        var open = await _reviews.GetOpenAsync(entry.Id, ct);
        var extensions = open is null ? [] : await _extensions.GetByReviewAsync(open.Id, ct);
        return _status.BuildSchedule(entry, open, extensions, _options, DateTimeOffset.UtcNow);
    }

    private async Task<(Response<PeriodicReviewModel>? Fail, DocumentMasterRegisterEntry? Entry, DocumentPeriodicReview? Review)> LoadAsync(
        Guid registerEntryId, Guid reviewId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return (FailReview("Register entry not found.", 404, PeriodicReviewReasonCodes.NotFoundNonLeakage, correlationId), null, null);
        }

        var review = await _reviews.GetByIdAsync(reviewId, ct);
        if (review is null || review.RegisterEntryId != registerEntryId)
        {
            return (FailReview("Periodic review not found.", 404, PeriodicReviewReasonCodes.ReviewNotFound, correlationId), null, null);
        }

        return (null, entry, review);
    }

    /// <summary>Idempotent: does not duplicate an already-open escalation of the same type for the review.</summary>
    private async Task RaiseEscalationAsync(
        DocumentMasterRegisterEntry entry, DocumentPeriodicReview review, ReviewEscalationType type,
        ReviewEscalationSeverity severity, ReviewEscalationRole role, string description, string correlationId, CancellationToken ct)
    {
        var existing = await _escalations.GetByReviewAsync(review.Id, ct);
        if (existing.Any(x => x.EscalationType == type && x.Status is ReviewEscalationStatus.Open or ReviewEscalationStatus.Acknowledged))
        {
            return;
        }

        await _escalations.CreateAsync(new DocumentPeriodicReviewEscalation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = entry.Id,
            PeriodicReviewId = review.Id,
            EscalationType = type,
            Severity = severity,
            Status = ReviewEscalationStatus.Open,
            RequiredRole = role,
            Description = description,
            DueAt = DateTimeOffset.UtcNow.AddDays(1),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        }, ct);
    }

    private static Response<PeriodicReviewModel> FailReview(string error, int status, string reason, string correlationId) =>
        Response<PeriodicReviewModel>.Fail(error, status, reason, correlationId);

    private static Response<PeriodicReviewExtensionModel> FailExtension(string error, int status, string reason, string correlationId) =>
        Response<PeriodicReviewExtensionModel>.Fail(error, status, reason, correlationId);

    private static Response<PeriodicReviewScheduleModel> FailSchedule(string error, int status, string reason, string correlationId) =>
        Response<PeriodicReviewScheduleModel>.Fail(error, status, reason, correlationId);

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
