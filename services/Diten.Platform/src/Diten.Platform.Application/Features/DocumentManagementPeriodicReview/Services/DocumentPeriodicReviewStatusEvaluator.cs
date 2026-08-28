using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Services;

/// <summary>
/// MOD-0029-FU12 — pure review schedule/state projection (GMG-QMS-SOP-0001 §9.15). Computes due date, initiation
/// window, due-soon/overdue flags and the permitted actions from the entry + its open review + extensions. Read-only:
/// raising escalations and persisting the overdue status is the service's job.
/// </summary>
public sealed class DocumentPeriodicReviewStatusEvaluator
{
    public PeriodicReviewScheduleModel BuildSchedule(
        DocumentMasterRegisterEntry entry,
        DocumentPeriodicReview? openReview,
        IReadOnlyList<DocumentPeriodicReviewExtension> extensions,
        DocumentPeriodicReviewOptions options,
        DateTimeOffset now)
    {
        var blocking = new List<string>();
        var warnings = new List<string>();

        var scheduled = DocumentReviewCycleCalculator.IsScheduledForReview(entry);
        if (!scheduled)
        {
            blocking.Add($"A {entry.LifecycleStatus} document is not scheduled for periodic review (SOP §9.15 applies to documents in force).");
        }

        if (entry.Criticality == DocumentCriticality.UrgentTemporary)
        {
            warnings.Add("Urgent/temporary instructions have a 30-day maximum validity handled by the expiry flow, not the periodic-review cycle.");
        }

        // The open review carries the current (possibly extended) due date; otherwise compute it.
        var due = openReview?.ReviewDueDate ?? DocumentReviewCycleCalculator.CurrentDueDate(entry);
        if (due is null)
        {
            warnings.Add("Review schedule is incomplete: the document has no effective date or last-review date to measure from.");
        }

        var windowStart = due is { } d ? DocumentReviewCycleCalculator.InitiationWindowStart(d, options.InitiationWindowDays) : (DateTimeOffset?)null;
        var completed = openReview?.ReviewStatus == PeriodicReviewStatus.Completed;
        var isOverdue = scheduled && due is { } dd && now > dd && !completed;
        var isDueSoon = !isOverdue && scheduled && windowStart is { } ws && due is { } de && now >= ws && now <= de;

        var extensionUsed = extensions.Any(x =>
            x.Status is PeriodicReviewExtensionStatus.Requested or PeriodicReviewExtensionStatus.Approved or PeriodicReviewExtensionStatus.Expired);
        var hasOpenExtension = extensions.Any(x => x.Status == PeriodicReviewExtensionStatus.Requested);

        if (isOverdue)
        {
            blocking.Add(entry.Criticality == DocumentCriticality.Critical
                ? "Critical periodic review is OVERDUE — there is no tolerance band; GQD determination is required (SOP §9.15)."
                : "Periodic review is overdue.");
        }

        if (scheduled && windowStart is { } w && now < w && openReview is null)
        {
            warnings.Add("The initiation window has not opened yet (60 calendar days before the due date).");
        }

        var canInitiate = scheduled && due is not null && openReview is null;
        var canRequestExtension = scheduled && openReview is not null && !completed && !isOverdue && !extensionUsed;
        var canComplete = openReview is not null && openReview.ReviewStatus is not (PeriodicReviewStatus.Completed or PeriodicReviewStatus.Cancelled);
        var requiresGqd = isOverdue && (entry.Criticality == DocumentCriticality.Critical
            || extensions.Any(x => x.Status == PeriodicReviewExtensionStatus.Expired));

        return new PeriodicReviewScheduleModel(
            entry.Id,
            DocumentReviewCycleCalculator.EffectiveCycleMonths(entry),
            entry.LastPeriodicReviewDate,
            due,
            due is { } dueDate ? (int)Math.Round((dueDate - now).TotalDays) : null,
            windowStart,
            (openReview?.ReviewStatus ?? PeriodicReviewStatus.NotStarted).ToString(),
            isDueSoon,
            isOverdue,
            hasOpenExtension,
            extensionUsed,
            canInitiate,
            canRequestExtension,
            canComplete,
            requiresGqd,
            blocking,
            warnings,
            openReview is null ? null : PeriodicReviewWire.ToReview(openReview));
    }
}
