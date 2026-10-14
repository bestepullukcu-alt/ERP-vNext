using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// WP-VW-W2 (K-W1 = A) — a rescheduled report is finalised together with the NEW planned visit it creates: the report
/// write (insert, or a version-checked replace) and the visit insert are ALL-OR-NOTHING. One transaction on a replica
/// set; compensated sequential writes on a standalone server (the CRM <c>SupportsTransactionsAsync</c> pattern).
/// </summary>
public interface IVisitRescheduleUnitOfWork
{
    /// <summary>Writes <paramref name="report"/> (insert when <paramref name="expectedVersion"/> is null, else replace at
    /// that version) and inserts <paramref name="newVisit"/>. False ⇒ a report concurrency mismatch; nothing written.</summary>
    Task<bool> SubmitWithNewVisitAsync(
        VisitReport report, int? expectedVersion, PlannedVisit newVisit, CancellationToken cancellationToken);
}
