using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0280-FU01 T1a — tenant-scoped repositories over the live TenantRepository<T> base. Every read below ANDs the
// base's ExecutionFilter (TenantId from the server context + IsDeleted=false); every insert goes through the base's
// CreateAsync, which stamps TenantId from the context. The duplicate-key catch is the ONE place the driver's own
// exception type is allowed (architecture rule: MongoDB Driver types stay in Persistence).

internal static class TimeEntryWrites
{
    public static bool IsDuplicateKey(MongoWriteException exception)
        => exception.WriteError?.Category == ServerErrorCategory.DuplicateKey;

    public static bool IsDuplicateKey(MongoCommandException exception)
        => exception.Code == 11000;
}

/// <summary>Raw storage for <see cref="TimesheetWeek"/>.</summary>
public sealed class TimesheetWeekRepository : TenantRepository<TimesheetWeek>, ITimesheetWeekRepository
{
    public TimesheetWeekRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TimeEntryTimesheetWeeks)
    {
    }

    public async Task<bool> TryCreateAsync(TimesheetWeek week, CancellationToken ct = default)
    {
        try
        {
            await CreateAsync(week, ct);
            return true;
        }
        catch (MongoWriteException exception) when (TimeEntryWrites.IsDuplicateKey(exception))
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<TimesheetWeek>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var filter = Builders<TimesheetWeek>.Filter.And(
            ExecutionFilter,
            Builders<TimesheetWeek>.Filter.In(x => x.Id, ids));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TimesheetWeek>> ListRevisionsAsync(Guid userId, string weekKey, CancellationToken ct = default)
    {
        var filter = Builders<TimesheetWeek>.Filter.And(
            ExecutionFilter,
            Builders<TimesheetWeek>.Filter.Eq(x => x.UserId, userId),
            Builders<TimesheetWeek>.Filter.Eq(x => x.WeekKey, weekKey));
        return await Collection.Find(filter).SortBy(x => x.RevisionNumber).ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(TimesheetWeek week, int expectedVersion, CancellationToken ct = default)
    {
        week.Version = expectedVersion + 1;
        week.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<TimesheetWeek>.Filter.And(
            ExecutionFilter,
            Builders<TimesheetWeek>.Filter.Eq(x => x.Id, week.Id),
            Builders<TimesheetWeek>.Filter.Eq(x => x.Version, expectedVersion));

        try
        {
            var previous = await Collection.FindOneAndReplaceAsync(
                filter,
                week,
                new FindOneAndReplaceOptions<TimesheetWeek> { ReturnDocument = ReturnDocument.Before },
                ct);
            return previous is not null;
        }
        catch (MongoCommandException exception) when (TimeEntryWrites.IsDuplicateKey(exception))
        {
            // A partial unique index (one in force / one open per week) refused the new state: a concurrent writer
            // got there first. Same answer as a version mismatch — the caller reloads.
            week.Version = expectedVersion;
            return false;
        }
    }

    public async Task<IReadOnlyList<TimesheetWeek>> ListSubmittedForApproverAsync(Guid approverUserId, CancellationToken ct = default)
    {
        var filter = Builders<TimesheetWeek>.Filter.And(
            ExecutionFilter,
            Builders<TimesheetWeek>.Filter.Eq(x => x.Status, TimesheetWeekStatus.Submitted),
            Builders<TimesheetWeek>.Filter.Eq(x => x.AssignedApproverUserId, approverUserId));
        return await Collection.Find(filter).SortBy(x => x.SubmittedAtUtcTicks).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TimesheetWeek>> ListNeedingFinalizationAsync(int limit, CancellationToken ct = default)
    {
        limit = Math.Max(1, limit);

        // BL-479 — two queries, each with its OWN share of the limit (v2 F9): approved weeks whose task totals never
        // landed (F12; a missing field matches null too) are listed first, but may take at most half the slots, so a
        // pile of approved weeks that keep failing cannot starve the undecided ones either. A share the one side does
        // not use goes to the other. Ticks, not the DateTimeOffset itself: that is stored as a [ticks, offset] array,
        // which Mongo does not order by its first element (BL-030).
        var outstandingTotals = Builders<TimesheetWeek>.Filter.And(
            ExecutionFilter,
            Builders<TimesheetWeek>.Filter.Eq(x => x.Status, TimesheetWeekStatus.Approved),
            Builders<TimesheetWeek>.Filter.Eq(x => x.TotalsAppliedAtUtc, null));
        var pending = Builders<TimesheetWeek>.Filter.And(
            ExecutionFilter,
            Builders<TimesheetWeek>.Filter.Eq(x => x.Status, TimesheetWeekStatus.Submitted),
            Builders<TimesheetWeek>.Filter.Ne(x => x.WorkflowInstanceId, null));

        var approvedShare = Math.Max(1, (limit + 1) / 2);
        var weeks = await Collection.Find(outstandingTotals).SortBy(x => x.SubmittedAtUtcTicks).Limit(approvedShare).ToListAsync(ct);
        List<TimesheetWeek> waiting = limit - weeks.Count <= 0
            ? []
            : await Collection.Find(pending).SortBy(x => x.SubmittedAtUtcTicks).Limit(limit - weeks.Count).ToListAsync(ct);

        // The pending side left slots unused: give them back to approved weeks beyond the first share.
        var unused = limit - weeks.Count - waiting.Count;
        if (unused > 0 && weeks.Count == approvedShare)
        {
            weeks.AddRange(await Collection.Find(outstandingTotals).SortBy(x => x.SubmittedAtUtcTicks)
                .Skip(approvedShare).Limit(unused).ToListAsync(ct));
        }

        weeks.AddRange(waiting);
        return weeks;
    }

    public async Task<IReadOnlyList<Guid>> ListUserIdsWithWeeksAsync(IReadOnlyCollection<string> weekKeys, CancellationToken ct = default)
    {
        if (weekKeys.Count == 0)
        {
            return [];
        }

        var filter = Builders<TimesheetWeek>.Filter.And(
            ExecutionFilter,
            Builders<TimesheetWeek>.Filter.In(x => x.WeekKey, weekKeys));
        return await (await Collection.DistinctAsync(x => x.UserId, filter, cancellationToken: ct)).ToListAsync(ct);
    }
}

/// <summary>Raw storage for <see cref="TimeEntry"/>.</summary>
public sealed class TimeEntryRepository : TenantRepository<TimeEntry>, ITimeEntryRepository
{
    public TimeEntryRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TimeEntryEntries)
    {
    }

    public async Task<IReadOnlyList<TimeEntry>> ListByWeekAsync(Guid timesheetWeekId, CancellationToken ct = default)
    {
        var filter = Builders<TimeEntry>.Filter.And(
            ExecutionFilter,
            Builders<TimeEntry>.Filter.Eq(x => x.TimesheetWeekId, timesheetWeekId));
        return await Collection.Find(filter).SortBy(x => x.LocalDate).ToListAsync(ct);
    }

    // GetByIdAsync(id) comes from the base: tenant + IsDeleted=false + id.

    public async Task<IReadOnlyList<TimeEntry>> ListByTaskIdsAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
    {
        if (taskIds.Count == 0)
        {
            return [];
        }

        var nullableIds = taskIds.Select(id => (Guid?)id).ToList();
        var filter = Builders<TimeEntry>.Filter.And(
            ExecutionFilter,
            Builders<TimeEntry>.Filter.In(x => x.TaskItemId, nullableIds));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task UpdateAsync(TimeEntry entry, CancellationToken ct = default)
    {
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<TimeEntry>.Filter.And(
            ExecutionFilter,
            Builders<TimeEntry>.Filter.Eq(x => x.Id, entry.Id));
        await Collection.ReplaceOneAsync(filter, entry, cancellationToken: ct);
    }

    public async Task SoftDeleteAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var filter = Builders<TimeEntry>.Filter.And(
            ExecutionFilter,
            Builders<TimeEntry>.Filter.In(x => x.Id, ids));
        var update = Builders<TimeEntry>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);
        await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
    }
}

/// <summary>Raw storage for <see cref="WorkCategory"/>.</summary>
public sealed class WorkCategoryRepository : TenantRepository<WorkCategory>, IWorkCategoryRepository
{
    public WorkCategoryRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TimeEntryWorkCategories)
    {
    }

    public async Task<bool> TryCreateAsync(WorkCategory category, CancellationToken ct = default)
    {
        try
        {
            await CreateAsync(category, ct);
            return true;
        }
        catch (MongoWriteException exception) when (TimeEntryWrites.IsDuplicateKey(exception))
        {
            return false;
        }
    }

    public Task<WorkCategory?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var filter = Builders<WorkCategory>.Filter.And(
            ExecutionFilter,
            Builders<WorkCategory>.Filter.Eq(x => x.Code, code));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<IReadOnlyList<WorkCategory>> ListAsync(CancellationToken ct = default)
        => await Collection.Find(ExecutionFilter).SortBy(x => x.SortOrder).ThenBy(x => x.Code).ToListAsync(ct);

    public async Task<bool> UpdateAsync(WorkCategory category, int expectedVersion, CancellationToken ct = default)
    {
        category.Version = expectedVersion + 1;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<WorkCategory>.Filter.And(
            ExecutionFilter,
            Builders<WorkCategory>.Filter.Eq(x => x.Id, category.Id),
            Builders<WorkCategory>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, category, cancellationToken: ct);
        return result.MatchedCount == 1;
    }
}

/// <summary>Raw storage for <see cref="TimeEntrySettings"/>.</summary>
public sealed class TimeEntrySettingsRepository : TenantRepository<TimeEntrySettings>, ITimeEntrySettingsRepository
{
    public TimeEntrySettingsRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TimeEntrySettings)
    {
    }

    public Task<TimeEntrySettings?> GetAsync(CancellationToken ct = default)
        => Collection.Find(ExecutionFilter).FirstOrDefaultAsync(ct)!;

    public async Task<bool> TryCreateAsync(TimeEntrySettings settings, CancellationToken ct = default)
    {
        try
        {
            await CreateAsync(settings, ct);
            return true;
        }
        catch (MongoWriteException exception) when (TimeEntryWrites.IsDuplicateKey(exception))
        {
            return false;
        }
    }

    public async Task<bool> UpdateAsync(TimeEntrySettings settings, int expectedVersion, CancellationToken ct = default)
    {
        settings.Version = expectedVersion + 1;
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<TimeEntrySettings>.Filter.And(
            ExecutionFilter,
            Builders<TimeEntrySettings>.Filter.Eq(x => x.Id, settings.Id),
            Builders<TimeEntrySettings>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, settings, cancellationToken: ct);
        return result.MatchedCount == 1;
    }
}

/// <summary>Raw storage for <see cref="LegalEntityTimeSetting"/>.</summary>
public sealed class LegalEntityTimeSettingRepository : TenantRepository<LegalEntityTimeSetting>, ILegalEntityTimeSettingRepository
{
    public LegalEntityTimeSettingRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TimeEntryLegalEntitySettings)
    {
    }

    public async Task<IReadOnlyList<LegalEntityTimeSetting>> ListAsync(CancellationToken ct = default)
        => await Collection.Find(ExecutionFilter).ToListAsync(ct);

    public Task<LegalEntityTimeSetting?> GetByLegalEntityIdAsync(Guid legalEntityId, CancellationToken ct = default)
    {
        var filter = Builders<LegalEntityTimeSetting>.Filter.And(
            ExecutionFilter,
            Builders<LegalEntityTimeSetting>.Filter.Eq(x => x.LegalEntityId, legalEntityId));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<bool> AnyTimerEnabledAsync(CancellationToken ct = default)
    {
        var filter = Builders<LegalEntityTimeSetting>.Filter.And(
            ExecutionFilter,
            Builders<LegalEntityTimeSetting>.Filter.Eq(x => x.TimerEnabled, true));
        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task<bool> TryCreateAsync(LegalEntityTimeSetting setting, CancellationToken ct = default)
    {
        try
        {
            await CreateAsync(setting, ct);
            return true;
        }
        catch (MongoWriteException exception) when (TimeEntryWrites.IsDuplicateKey(exception))
        {
            return false;
        }
    }

    public async Task<bool> UpdateAsync(LegalEntityTimeSetting setting, int expectedVersion, CancellationToken ct = default)
    {
        setting.Version = expectedVersion + 1;
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<LegalEntityTimeSetting>.Filter.And(
            ExecutionFilter,
            Builders<LegalEntityTimeSetting>.Filter.Eq(x => x.Id, setting.Id),
            Builders<LegalEntityTimeSetting>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, setting, cancellationToken: ct);
        return result.MatchedCount == 1;
    }
}

/// <summary>Raw storage for <see cref="TaskTimeTotal"/>. The finalizer is its only caller.</summary>
public sealed class TaskTimeTotalRepository : TenantRepository<TaskTimeTotal>, ITaskTimeTotalRepository
{
    public TaskTimeTotalRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TimeEntryTaskTotals)
    {
    }

    public async Task<IReadOnlyList<TaskTimeTotal>> ListByTaskIdsAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
    {
        if (taskIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<TaskTimeTotal>.Filter.And(
            ExecutionFilter,
            Builders<TaskTimeTotal>.Filter.In(x => x.TaskItemId, taskIds));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<bool> TrySetApprovedMinutesAsync(
        Guid taskItemId, int approvedMinutes, Guid lastFinalizedWeekId, DateTimeOffset recomputedAtUtc, int? expectedVersion,
        CancellationToken ct = default)
    {
        if (expectedVersion is null)
        {
            // First total for this task: an insert, and the tenant-unique task index refuses a second one — a
            // concurrent finalizer that got there first wins, and this caller recomputes on top of its value.
            try
            {
                await CreateAsync(new TaskTimeTotal
                {
                    TenantId = TenantContext.TenantId,
                    TaskItemId = taskItemId,
                    ApprovedMinutes = approvedMinutes,
                    RecomputedAtUtc = recomputedAtUtc,
                    LastFinalizedWeekId = lastFinalizedWeekId
                }, ct);
                return true;
            }
            catch (MongoWriteException exception) when (TimeEntryWrites.IsDuplicateKey(exception))
            {
                return false;
            }
        }

        var filter = Builders<TaskTimeTotal>.Filter.And(
            ExecutionFilter,
            Builders<TaskTimeTotal>.Filter.Eq(x => x.TaskItemId, taskItemId),
            Builders<TaskTimeTotal>.Filter.Eq(x => x.Version, expectedVersion.Value));
        // $set, never $inc — the recomputation IS the idempotency (pack §4.4); the version is what stops a total
        // computed from an older view of the approvals overwriting a newer one.
        var update = Builders<TaskTimeTotal>.Update
            .Set(x => x.ApprovedMinutes, approvedMinutes)
            .Set(x => x.RecomputedAtUtc, recomputedAtUtc)
            .Set(x => x.LastFinalizedWeekId, lastFinalizedWeekId)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Set(x => x.Version, expectedVersion.Value + 1);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.MatchedCount == 1;
    }
}

/// <summary>Raw storage for <see cref="TimerSegment"/> (T1b).</summary>
public sealed class TimerSegmentRepository : TenantRepository<TimerSegment>, ITimerSegmentRepository
{
    public TimerSegmentRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TimeEntryTimerSegments)
    {
    }

    public async Task<bool> TryStartAsync(TimerSegment segment, CancellationToken ct = default)
    {
        segment.IsRunning = true;
        try
        {
            await CreateAsync(segment, ct);
            return true;
        }
        catch (MongoWriteException exception) when (TimeEntryWrites.IsDuplicateKey(exception))
        {
            // The partial unique index (one running per person) or the start-transition index said no.
            return false;
        }
    }

    public Task<TimerSegment?> GetRunningAsync(Guid userId, CancellationToken ct = default)
    {
        var filter = Builders<TimerSegment>.Filter.And(
            ExecutionFilter,
            Builders<TimerSegment>.Filter.Eq(x => x.UserId, userId),
            Builders<TimerSegment>.Filter.Eq(x => x.IsRunning, true));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<IReadOnlyList<TimerSegment>> ListRunningByTaskAsync(Guid taskItemId, CancellationToken ct = default)
    {
        var filter = Builders<TimerSegment>.Filter.And(
            ExecutionFilter,
            Builders<TimerSegment>.Filter.Eq(x => x.TaskItemId, taskItemId),
            Builders<TimerSegment>.Filter.Eq(x => x.IsRunning, true));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TimerSegment>> ListRunningAsync(int limit, CancellationToken ct = default)
    {
        var filter = Builders<TimerSegment>.Filter.And(
            ExecutionFilter,
            Builders<TimerSegment>.Filter.Eq(x => x.IsRunning, true));
        return await Collection.Find(filter).Limit(Math.Max(1, limit)).ToListAsync(ct);
    }

    public async Task<bool> ExistsForStartTransitionAsync(Guid transitionId, CancellationToken ct = default)
    {
        var filter = Builders<TimerSegment>.Filter.And(
            ExecutionFilter,
            Builders<TimerSegment>.Filter.Eq(x => x.StartTransitionId, transitionId));
        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task<bool> TryCloseAsync(
        Guid segmentId, DateTimeOffset stoppedAtUtc, int durationSeconds, int outsideWorkingMinutes,
        TimerStopReason reason, Guid? stopTransitionId, CancellationToken ct = default)
    {
        // Conditional on IsRunning: exactly one closer wins, and only the winner writes the draft.
        var filter = Builders<TimerSegment>.Filter.And(
            ExecutionFilter,
            Builders<TimerSegment>.Filter.Eq(x => x.Id, segmentId),
            Builders<TimerSegment>.Filter.Eq(x => x.IsRunning, true));
        var update = Builders<TimerSegment>.Update
            .Set(x => x.IsRunning, false)
            .Set(x => x.StoppedAtUtc, stoppedAtUtc)
            .Set(x => x.DurationSeconds, durationSeconds)
            .Set(x => x.OutsideWorkingMinutes, outsideWorkingMinutes)
            .Set(x => x.StopReason, reason)
            .Set(x => x.StopTransitionId, stopTransitionId)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task<IReadOnlyList<TimerSegment>> ListClosedForDayAsync(Guid userId, DateOnly localDate, CancellationToken ct = default)
    {
        var filter = Builders<TimerSegment>.Filter.And(
            ExecutionFilter,
            Builders<TimerSegment>.Filter.Eq(x => x.UserId, userId),
            Builders<TimerSegment>.Filter.Eq(x => x.LocalDate, localDate),
            Builders<TimerSegment>.Filter.Eq(x => x.IsRunning, false));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TimerSegment>> ListForWeekAsync(Guid userId, string weekKey, CancellationToken ct = default)
    {
        var filter = Builders<TimerSegment>.Filter.And(
            ExecutionFilter,
            Builders<TimerSegment>.Filter.Eq(x => x.UserId, userId),
            Builders<TimerSegment>.Filter.Eq(x => x.WeekKey, weekKey));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TimerSegment>> ListClosedAtMidnightAsync(Guid userId, DateOnly localDate, CancellationToken ct = default)
    {
        var filter = Builders<TimerSegment>.Filter.And(
            ExecutionFilter,
            Builders<TimerSegment>.Filter.Eq(x => x.UserId, userId),
            Builders<TimerSegment>.Filter.Eq(x => x.LocalDate, localDate),
            Builders<TimerSegment>.Filter.Eq(x => x.StopReason, TimerStopReason.LocalMidnight));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<bool> TryClaimAutoCloseNotificationAsync(Guid segmentId, DateTimeOffset claimedAtUtc, CancellationToken ct = default)
    {
        var filter = Builders<TimerSegment>.Filter.And(
            ExecutionFilter,
            Builders<TimerSegment>.Filter.Eq(x => x.Id, segmentId),
            Builders<TimerSegment>.Filter.Eq(x => x.AutoCloseNotifiedAtUtc, null));
        var update = Builders<TimerSegment>.Update.Set(x => x.AutoCloseNotifiedAtUtc, claimedAtUtc);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public Task<long> CountUnminimisedAsync(Guid userId, string weekKey, CancellationToken ct = default)
        => Collection.CountDocumentsAsync(UnminimisedFilter(userId, weekKey), cancellationToken: ct);

    public async Task<long> MinimiseWeekAsync(
        Guid userId, string weekKey, DateTimeOffset minimisedAtUtc, CancellationToken ct = default, Guid? segmentId = null)
    {
        // D4 / R8 — an UPDATE: the instants go, the row, its duration, day, target and outside minutes stay.
        var update = Builders<TimerSegment>.Update
            .Set(x => x.StartedAtUtc, null)
            .Set(x => x.StoppedAtUtc, null)
            .Set(x => x.MinimisedAtUtc, minimisedAtUtc)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1);
        var filter = segmentId is { } one
            ? Builders<TimerSegment>.Filter.And(UnminimisedFilter(userId, weekKey), Builders<TimerSegment>.Filter.Eq(x => x.Id, one))
            : UnminimisedFilter(userId, weekKey);
        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount;
    }

    private FilterDefinition<TimerSegment> UnminimisedFilter(Guid userId, string weekKey)
        => Builders<TimerSegment>.Filter.And(
            ExecutionFilter,
            Builders<TimerSegment>.Filter.Eq(x => x.UserId, userId),
            Builders<TimerSegment>.Filter.Eq(x => x.WeekKey, weekKey),
            Builders<TimerSegment>.Filter.Eq(x => x.IsRunning, false),
            Builders<TimerSegment>.Filter.Eq(x => x.MinimisedAtUtc, null));

    public async Task<IReadOnlyList<Guid>> ListUserIdsWithSegmentsAsync(IReadOnlyCollection<string> weekKeys, CancellationToken ct = default)
    {
        if (weekKeys.Count == 0)
        {
            return [];
        }

        var filter = Builders<TimerSegment>.Filter.And(
            ExecutionFilter,
            Builders<TimerSegment>.Filter.In(x => x.WeekKey, weekKeys));
        return await (await Collection.DistinctAsync(x => x.UserId, filter, cancellationToken: ct)).ToListAsync(ct);
    }
}

/// <summary>Raw storage for <see cref="TimeSuggestion"/> decisions (T1b).</summary>
public sealed class TimeSuggestionRepository : TenantRepository<TimeSuggestion>, ITimeSuggestionRepository
{
    public TimeSuggestionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TimeEntrySuggestions)
    {
    }

    public async Task<IReadOnlyList<TimeSuggestion>> ListForMeetingsAsync(
        Guid userId, IReadOnlyCollection<Guid> meetingIds, CancellationToken ct = default)
    {
        if (meetingIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<TimeSuggestion>.Filter.And(
            ExecutionFilter,
            Builders<TimeSuggestion>.Filter.Eq(x => x.UserId, userId),
            Builders<TimeSuggestion>.Filter.In(x => x.MeetingId, meetingIds));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<bool> TryCreateAsync(TimeSuggestion suggestion, CancellationToken ct = default)
    {
        try
        {
            await CreateAsync(suggestion, ct);
            return true;
        }
        catch (MongoWriteException exception) when (TimeEntryWrites.IsDuplicateKey(exception))
        {
            return false;
        }
    }
}

/// <summary>Raw storage for <see cref="TimeEntryNotificationMark"/> (T3, pack §21.3 N6). The unique (tenant, kind, key)
/// index is the whole rule: the first insert wins, every later one is a duplicate key and answers false.</summary>
public sealed class TimeEntryNotificationMarkRepository : TenantRepository<TimeEntryNotificationMark>, ITimeEntryNotificationMarkRepository
{
    public TimeEntryNotificationMarkRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TimeEntryNotificationMarks)
    {
    }

    public async Task<bool> TryClaimAsync(string kind, string key, DateTimeOffset claimedAtUtc, CancellationToken ct = default)
    {
        try
        {
            await CreateAsync(new TimeEntryNotificationMark
            {
                TenantId = TenantContext.TenantId,
                Kind = kind,
                Key = key,
                CreatedAtUtc = claimedAtUtc,
                CreatedBy = "system"
            }, ct);
            return true;
        }
        catch (MongoWriteException exception) when (TimeEntryWrites.IsDuplicateKey(exception))
        {
            return false;
        }
    }

    public Task<bool> ExistsAsync(string kind, string key, CancellationToken ct = default)
        => Collection.Find(Builders<TimeEntryNotificationMark>.Filter.And(
                Builders<TimeEntryNotificationMark>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
                Builders<TimeEntryNotificationMark>.Filter.Eq(x => x.Kind, kind),
                Builders<TimeEntryNotificationMark>.Filter.Eq(x => x.Key, key)))
            .AnyAsync(ct);
}
