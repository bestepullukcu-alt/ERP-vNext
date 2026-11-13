using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>The list read's filter (all optional; days both included).</summary>
public sealed record VisitNotificationFilter(
    DateOnly? From = null, DateOnly? To = null, string? EventCode = null, string? Status = null, string? ResourceId = null,
    IReadOnlyCollection<string>? Reps = null);

/// <summary>
/// WP-VW-W5 (K-N6) — the visit notification log. Tenant scoped; the database keeps one row per (tenant, event, subject).
/// The scheduler's two cross-tenant reads (<see cref="ClaimNextAsync"/>) carry the tenant on every row they return.
/// </summary>
public interface IVisitNotificationRepository
{
    /// <summary>False when the (tenant, event, subject) row already exists — the e-mail was already queued / sent.</summary>
    Task<bool> TryInsertAsync(VisitNotification row, CancellationToken cancellationToken);

    /// <summary>WP-VW-W5 W5-FIX — whether a row of this exact event code exists for the subject (the sender asks it for the
    /// legacy spelling of a code, so a report mailed before the rename is not mailed again).</summary>
    Task<bool> ExistsAsync(Guid tenantId, string eventCode, Guid subjectId, CancellationToken cancellationToken);

    /// <summary>Optimistic replace on the technical version.</summary>
    Task<bool> ReplaceAsync(VisitNotification row, int expectedVersion, CancellationToken cancellationToken);

    Task<VisitNotification?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>A page, newest first; <see cref="VisitNotificationFilter.Reps"/> narrows to the caller's visibility.</summary>
    Task<(IReadOnlyList<VisitNotification> Items, long Total)> ListAsync(
        Guid tenantId, VisitNotificationFilter filter, int skip, int take, CancellationToken cancellationToken);

    /// <summary>
    /// The scheduler's ATOMIC claim (one find-and-update): a failed row whose next attempt is due, or a queued row left
    /// older than <paramref name="staleQueuedBeforeTicks"/> (its sender died), not claimed by anyone right now. The row
    /// comes back claimed until <paramref name="claimUntilTicks"/> by <paramref name="claimer"/>; two workers never get
    /// the same row. Null when nothing is due.
    /// </summary>
    Task<VisitNotification?> ClaimNextAsync(
        long nowTicks, long staleQueuedBeforeTicks, long claimUntilTicks, string claimer, int maxAttempts, CancellationToken cancellationToken);
}

/// <summary>WP-VW-W5 (K-N5) — one settings document per tenant (absent = the defaults).</summary>
public interface IVisitNotificationSettingsRepository
{
    Task<VisitNotificationSettings?> GetAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<bool> InsertAsync(VisitNotificationSettings entity, CancellationToken cancellationToken);

    Task<bool> ReplaceAsync(VisitNotificationSettings entity, int expectedVersion, CancellationToken cancellationToken);

    /// <summary>The scheduler's tenant source: the tenants whose settings turn the overdue reminder on (a tenant without
    /// a settings document is never swept — no backlog of old visits is mailed on the day this ships).</summary>
    Task<IReadOnlyList<Guid>> ListTenantsWithOverdueRemindersAsync(CancellationToken cancellationToken);
}
