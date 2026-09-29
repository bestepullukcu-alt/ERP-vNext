using Diten.Platform.Domain.Entities.TimeEntry;

namespace Diten.Platform.Domain.Repositories;

// MOD-0280-FU01 (pack §3.1, §4) — the time-entry module's own storage seams. Every implementation sits on the live
// TenantRepository<T> base, so every read ANDs TenantId (from the server context) + IsDeleted=false and every insert
// stamps TenantId from the context: a cross-tenant id is simply "not found", with no metadata to leak.
//
// Duplicate-key races are turned into return values HERE (the MongoDB driver's exception type stays in Persistence,
// as the architecture rule requires) — a TryCreate that lost a race answers false, never a 500.

/// <summary>Raw storage for <see cref="TimesheetWeek"/> revisions.</summary>
public interface ITimesheetWeekRepository
{
    /// <summary>Inserts the revision. <c>false</c> when a unique index refused it (another request created the same
    /// revision number, or a second open revision, first).</summary>
    Task<bool> TryCreateAsync(TimesheetWeek week, CancellationToken ct = default);

    Task<TimesheetWeek?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<TimesheetWeek>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Every live revision of one person's week, lowest revision first.</summary>
    Task<IReadOnlyList<TimesheetWeek>> ListRevisionsAsync(Guid userId, string weekKey, CancellationToken ct = default);

    /// <summary>Optimistic-concurrency replace. <c>false</c> (never throws) when the stored version moved, or when a
    /// unique index refused the new state — the caller turns either into a 409.</summary>
    Task<bool> UpdateAsync(TimesheetWeek week, int expectedVersion, CancellationToken ct = default);

    /// <summary>Submitted revisions MOD-0023 assigned to <paramref name="approverUserId"/>, oldest submission first.</summary>
    Task<IReadOnlyList<TimesheetWeek>> ListSubmittedForApproverAsync(Guid approverUserId, CancellationToken ct = default);

    /// <summary>Submitted revisions that carry a MOD-0023 instance — the sweep's work list, oldest submission first.</summary>
    Task<IReadOnlyList<TimesheetWeek>> ListSubmittedAsync(int limit, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="TimeEntry"/> rows.</summary>
public interface ITimeEntryRepository
{
    Task<TimeEntry> CreateAsync(TimeEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<TimeEntry>> ListByWeekAsync(Guid timesheetWeekId, CancellationToken ct = default);

    /// <summary>Every live row that points at one of these tasks, across all people and revisions — the finalizer's
    /// input for recomputing <see cref="TaskTimeTotal"/>.</summary>
    Task<IReadOnlyList<TimeEntry>> ListByTaskIdsAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default);

    /// <summary>Replaces a draft row's mutable fields (minutes, note). Tenant + id scoped.</summary>
    Task UpdateAsync(TimeEntry entry, CancellationToken ct = default);

    /// <summary>Soft-deletes rows (IsDeleted, UpdatedAt). Nothing in this module is hard-deleted.</summary>
    Task SoftDeleteAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="WorkCategory"/>.</summary>
public interface IWorkCategoryRepository
{
    /// <summary><c>false</c> when the tenant-unique code index refused the insert.</summary>
    Task<bool> TryCreateAsync(WorkCategory category, CancellationToken ct = default);

    Task<WorkCategory?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<WorkCategory?> GetByCodeAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<WorkCategory>> ListAsync(CancellationToken ct = default);

    Task<bool> UpdateAsync(WorkCategory category, int expectedVersion, CancellationToken ct = default);
}

/// <summary>Raw storage for the one-per-tenant <see cref="TimeEntrySettings"/> row.</summary>
public interface ITimeEntrySettingsRepository
{
    Task<TimeEntrySettings?> GetAsync(CancellationToken ct = default);

    Task<bool> TryCreateAsync(TimeEntrySettings settings, CancellationToken ct = default);

    Task<bool> UpdateAsync(TimeEntrySettings settings, int expectedVersion, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="LegalEntityTimeSetting"/> — one row per legal entity, no row = timer off.</summary>
public interface ILegalEntityTimeSettingRepository
{
    Task<IReadOnlyList<LegalEntityTimeSetting>> ListAsync(CancellationToken ct = default);

    Task<LegalEntityTimeSetting?> GetByLegalEntityIdAsync(Guid legalEntityId, CancellationToken ct = default);

    Task<bool> TryCreateAsync(LegalEntityTimeSetting setting, CancellationToken ct = default);

    Task<bool> UpdateAsync(LegalEntityTimeSetting setting, int expectedVersion, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="TaskTimeTotal"/>. Written ONLY by the approval finalizer (D7).</summary>
public interface ITaskTimeTotalRepository
{
    Task<IReadOnlyList<TaskTimeTotal>> ListByTaskIdsAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default);

    /// <summary>SETS the task's approved minutes (never an increment — replaying it writes the same value), guarded by
    /// the row's version: <paramref name="expectedVersion"/> null means "no row yet" (an insert). <c>false</c> when
    /// another finalizer wrote in between — the caller recomputes from the current approvals and tries again.</summary>
    Task<bool> TrySetApprovedMinutesAsync(
        Guid taskItemId, int approvedMinutes, Guid lastFinalizedWeekId, DateTimeOffset recomputedAtUtc, int? expectedVersion,
        CancellationToken ct = default);
}
