using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Services;

/// <summary>
/// MOD-0029-FU12 — pure review-cycle maths (GMG-QMS-SOP-0001 §7.1 review cycles, §9.15 initiation window). Default
/// maximum cycles: Critical 24 months, Major 36, Minor 48, Urgent/temporary 1 (its 30-day lifecycle is FU13's subject
/// — flagged as a warning here). An explicit <c>ReviewCycleMonths</c> on the entry always wins.
/// </summary>
public static class DocumentReviewCycleCalculator
{
    public static int DefaultCycleMonths(DocumentCriticality criticality) => criticality switch
    {
        DocumentCriticality.Critical => 24,
        DocumentCriticality.Major => 36,
        DocumentCriticality.Minor => 48,
        DocumentCriticality.UrgentTemporary => 1,
        _ => 48
    };

    public static int EffectiveCycleMonths(DocumentMasterRegisterEntry entry) =>
        entry.ReviewCycleMonths is { } m && m > 0 ? m : DefaultCycleMonths(entry.Criticality);

    /// <summary>The baseline a due date is measured from: the last completed review, else the effective date.</summary>
    public static DateTimeOffset? ScheduleBaseline(DocumentMasterRegisterEntry entry) =>
        entry.LastPeriodicReviewDate ?? entry.EffectiveDate;

    /// <summary>
    /// The current due date: the stored <c>NextReviewDueDate</c> when set (it reflects any approved extension),
    /// otherwise computed from the baseline + cycle. Null when the document has no effective/review baseline yet.
    /// </summary>
    public static DateTimeOffset? CurrentDueDate(DocumentMasterRegisterEntry entry)
    {
        if (entry.NextReviewDueDate is { } stored)
        {
            return stored;
        }

        var baseline = ScheduleBaseline(entry);
        return baseline?.AddMonths(EffectiveCycleMonths(entry));
    }

    /// <summary>The next due date after a review completes at <paramref name="completedAt"/>.</summary>
    public static DateTimeOffset NextDueDateAfterCompletion(DocumentMasterRegisterEntry entry, DateTimeOffset completedAt) =>
        completedAt.AddMonths(EffectiveCycleMonths(entry));

    public static DateTimeOffset InitiationWindowStart(DateTimeOffset dueDate, int windowDays) =>
        dueDate.AddDays(-windowDays);

    /// <summary>Periodic review applies only to a document that is in force (SOP §9.15) — Effective or Under revision.</summary>
    public static bool IsScheduledForReview(DocumentMasterRegisterEntry entry) =>
        entry.LifecycleStatus.IsOperationallyEffective();
}
