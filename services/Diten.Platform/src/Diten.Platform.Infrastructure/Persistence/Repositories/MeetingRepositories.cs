using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0357 S1 — tenant-scoped repository over the live TenantRepository<T> base. CreateAsync / GetByIdAsync /
// DeleteAsync (soft) are inherited: the base stamps TenantId from context on create and ANDs the tenant +
// IsDeleted execution filter into every read — the same guarantee every other Platform repository already gives.

/// <summary>Raw storage for <see cref="RecordLink"/>. See <see cref="IRecordLinkRepository"/> for why the
/// idempotency and both-directions-in-one-call rules live in the SERVICE, not here.</summary>
public sealed class RecordLinkRepository : TenantRepository<RecordLink>, IRecordLinkRepository
{
    public RecordLinkRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.MeetingRecordLinks)
    {
    }

    public async Task<RecordLink?> FindAsync(
        string sourceModuleCode, Guid sourceRecordId,
        string targetModuleCode, Guid targetRecordId,
        string linkType, CancellationToken ct = default)
    {
        var filter = Builders<RecordLink>.Filter.And(
            ExecutionFilter,
            Builders<RecordLink>.Filter.Eq(x => x.SourceModuleCode, sourceModuleCode),
            Builders<RecordLink>.Filter.Eq(x => x.SourceRecordId, sourceRecordId),
            Builders<RecordLink>.Filter.Eq(x => x.TargetModuleCode, targetModuleCode),
            Builders<RecordLink>.Filter.Eq(x => x.TargetRecordId, targetRecordId),
            Builders<RecordLink>.Filter.Eq(x => x.LinkType, linkType));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<RecordLink> FindOrCreateAsync(RecordLink candidate, CancellationToken ct = default)
    {
        var existing = await FindAsync(
            candidate.SourceModuleCode, candidate.SourceRecordId,
            candidate.TargetModuleCode, candidate.TargetRecordId,
            candidate.LinkType, ct);
        if (existing is not null)
        {
            return existing;
        }

        try
        {
            return await CreateAsync(candidate, ct);
        }
        catch (MongoWriteException exception) when (
            exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // The race this method exists for: another request inserted the identical six values between our
            // FindAsync above and this InsertOneAsync. The unique index refused OUR write; the winner's row is
            // what FindAsync now finds — never a 500 to the loser of a race that produced the correct outcome
            // either way.
            var winner = await FindAsync(
                candidate.SourceModuleCode, candidate.SourceRecordId,
                candidate.TargetModuleCode, candidate.TargetRecordId,
                candidate.LinkType, ct);
            return winner
                ?? throw new InvalidOperationException(
                    "A duplicate-key write was refused but no matching link could be re-read afterward.");
        }
    }

    public async Task<RecordLink?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var filter = Builders<RecordLink>.Filter.And(
            ExecutionFilter,
            Builders<RecordLink>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<RecordLink>> ListBySourceAsync(
        IReadOnlyCollection<Guid> sourceRecordIds, CancellationToken ct = default)
    {
        if (sourceRecordIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<RecordLink>.Filter.And(
            ExecutionFilter,
            Builders<RecordLink>.Filter.In(x => x.SourceRecordId, sourceRecordIds));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RecordLink>> ListByTargetAsync(
        IReadOnlyCollection<Guid> targetRecordIds, CancellationToken ct = default)
    {
        if (targetRecordIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<RecordLink>.Filter.And(
            ExecutionFilter,
            Builders<RecordLink>.Filter.In(x => x.TargetRecordId, targetRecordIds));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    /// <summary>
    /// MOD-0357 S2 fix — the base <c>TenantRepository{T}.DeleteAsync</c> only ever set <c>IsDeleted</c> +
    /// <c>UpdatedAt</c>; <see cref="RecordLink.DeletedAt"/> is a field this entity declares specifically for an
    /// auditable "when" (its own doc comment says so), and nothing was ever setting it. Overridden here rather
    /// than in the base: <c>DeletedAt</c> is not a member of <c>TenantScopedEntity</c>, so the base has no
    /// generic way to set it for every entity that happens to declare one.
    /// </summary>
    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<RecordLink>.Filter.And(
            ExecutionFilter,
            Builders<RecordLink>.Filter.Eq(x => x.Id, id));

        var update = Builders<RecordLink>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}

/// <summary>Raw storage for <see cref="Meeting"/>. <see cref="UpdateAsync"/> mirrors
/// <c>TaskItemRepository.UpdateAsync</c>'s optimistic-concurrency shape exactly.</summary>
public sealed class MeetingRepository : TenantRepository<Meeting>, IMeetingRepository
{
    private readonly IPlatformDbContext _dbContext;

    public MeetingRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.MeetingMeetings)
    {
        _dbContext = dbContext;
    }

    public async Task<Meeting?> GetByIdAsync(IPlatformTransactionSession session, Guid id, CancellationToken ct = default)
    {
        var filter = Builders<Meeting>.Filter.And(
            ExecutionFilter,
            Builders<Meeting>.Filter.Eq(x => x.Id, id));
        return await Collection.Find(PlatformMongoTransactionSession.Require(session, _dbContext), filter).FirstOrDefaultAsync(ct);
    }

    public Task<Meeting?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var filter = Builders<Meeting>.Filter.And(
            ExecutionFilter,
            Builders<Meeting>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<Meeting> FindOrCreateAsync(Meeting candidate, CancellationToken ct = default)
    {
        var existing = await FindByIdempotencyKeyAsync(candidate.IdempotencyKey, ct);
        if (existing is not null)
        {
            return existing;
        }

        try
        {
            return await CreateAsync(candidate, ct);
        }
        catch (MongoWriteException exception) when (
            exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var winner = await FindByIdempotencyKeyAsync(candidate.IdempotencyKey, ct);
            return winner
                ?? throw new InvalidOperationException(
                    "A duplicate-key write was refused but no matching meeting could be re-read afterward.");
        }
    }

    public Task<bool> UpdateAsync(Meeting meeting, int expectedVersion, CancellationToken ct = default)
        => ReadVersionWrites.ReplaceAsync(Collection, ExecutionFilter, meeting, expectedVersion, ct);

    public Task<bool> UpdateAsync(IPlatformTransactionSession session, Meeting meeting, int expectedVersion, CancellationToken ct = default)
        => ReadVersionWrites.ReplaceAsync(
            Collection, PlatformMongoTransactionSession.Require(session, _dbContext), ExecutionFilter, meeting, expectedVersion, ct);

    public async Task<IReadOnlyList<Meeting>> ListAsync(CancellationToken ct = default)
        => await Collection.Find(ExecutionFilter).ToListAsync(ct);

    public async Task<IReadOnlyList<Meeting>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var filter = Builders<Meeting>.Filter.And(
            ExecutionFilter,
            Builders<Meeting>.Filter.In(x => x.Id, ids));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    /// <summary>
    /// MOD-0280-FU01 T1b v2 F9 — the date window in the DATABASE. <c>StartAt</c> is stored by the driver's default
    /// DateTimeOffset serializer as <c>[localTicks, offsetMinutes]</c> (BL-030), so the UTC instant is
    /// <c>localTicks − offsetMinutes × 60·10⁷</c>; the filter compares that, computed per document, against the window's
    /// UTC ticks — correct whatever offset a meeting was saved with.
    /// </summary>
    public async Task<IReadOnlyList<Meeting>> ListByIdsStartingBetweenAsync(
        IReadOnlyCollection<Guid> ids, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var utcTicks = new BsonDocument("$subtract", new BsonArray
        {
            new BsonDocument("$arrayElemAt", new BsonArray { "$StartAt", 0 }),
            new BsonDocument("$multiply", new BsonArray
            {
                new BsonDocument("$arrayElemAt", new BsonArray { "$StartAt", 1 }), TimeSpan.TicksPerMinute
            })
        });
        var window = new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("$gte", new BsonArray { utcTicks, fromUtc.UtcTicks }),
            new BsonDocument("$lt", new BsonArray { utcTicks, toUtc.UtcTicks })
        }));

        var filter = Builders<Meeting>.Filter.And(
            ExecutionFilter,
            Builders<Meeting>.Filter.In(x => x.Id, ids),
            new BsonDocumentFilterDefinition<Meeting>(window));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<bool> AnyByMeetingTypeIdAsync(Guid meetingTypeId, CancellationToken ct = default)
    {
        var filter = Builders<Meeting>.Filter.And(
            ExecutionFilter,
            Builders<Meeting>.Filter.Eq(x => x.MeetingTypeId, meetingTypeId));
        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task<Meeting?> FindByFollowUpOfMeetingIdAsync(Guid meetingId, CancellationToken ct = default)
    {
        var filter = Builders<Meeting>.Filter.And(
            ExecutionFilter,
            Builders<Meeting>.Filter.Eq(x => x.FollowUpOfMeetingId, meetingId));

        // LIVE-MEASURED (S7): ordered IN MEMORY, not by the server — the same reason
        // BusinessReferenceDataStewardshipRepository.GetUsageRegistrationsAsync and
        // WorkflowInstanceRepository.GetLatestByObjectRefAsync already do (BL-030): with no
        // DateTimeOffsetSerializer registered, CreatedAt is stored as a BSON [ticks, offsetMinutes] array, and a
        // server-side sort on it does not reliably reflect chronological order. Verified broken against a real
        // Mongo here too — two follow-ups seeded 15ms apart came back in a non-deterministic order. This
        // repository's own row count per source meeting is always small (a handful of accidental duplicate
        // schedules at most), so an in-memory sort costs nothing worth avoiding a full-collection scan for.
        var rows = await Collection.Find(filter).ToListAsync(ct);
        return rows.OrderBy(x => x.CreatedAt).FirstOrDefault();
    }
}

/// <summary>Raw storage for <see cref="MeetingAttendee"/>.</summary>
public sealed class MeetingAttendeeRepository : TenantRepository<MeetingAttendee>, IMeetingAttendeeRepository
{
    private readonly IPlatformDbContext _dbContext;

    public MeetingAttendeeRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.MeetingAttendees)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MeetingAttendee>> ListByMeetingIdAsync(Guid meetingId, CancellationToken ct = default)
    {
        var filter = Builders<MeetingAttendee>.Filter.And(
            ExecutionFilter,
            Builders<MeetingAttendee>.Filter.Eq(x => x.MeetingId, meetingId));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MeetingAttendee>> ListByMeetingIdsAsync(
        IReadOnlyCollection<Guid> meetingIds, CancellationToken ct = default)
    {
        if (meetingIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<MeetingAttendee>.Filter.And(
            ExecutionFilter,
            Builders<MeetingAttendee>.Filter.In(x => x.MeetingId, meetingIds));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public Task<MeetingAttendee?> FindAsync(Guid meetingId, Guid userId, CancellationToken ct = default)
    {
        var filter = Builders<MeetingAttendee>.Filter.And(
            ExecutionFilter,
            Builders<MeetingAttendee>.Filter.Eq(x => x.MeetingId, meetingId),
            Builders<MeetingAttendee>.Filter.Eq(x => x.UserId, userId));
        return Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task UpdateInvitationResponseAsync(Guid id, InvitationResponse response, CancellationToken ct = default)
    {
        var filter = Builders<MeetingAttendee>.Filter.And(
            ExecutionFilter,
            Builders<MeetingAttendee>.Filter.Eq(x => x.Id, id));
        var update = Builders<MeetingAttendee>.Update.Set(x => x.InvitationResponse, response);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<MeetingAttendee>> ListPendingByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var filter = Builders<MeetingAttendee>.Filter.And(
            ExecutionFilter,
            Builders<MeetingAttendee>.Filter.Eq(x => x.UserId, userId),
            Builders<MeetingAttendee>.Filter.Eq(x => x.InvitationResponse, InvitationResponse.Pending));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MeetingAttendee>> ListNotDeclinedByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var filter = Builders<MeetingAttendee>.Filter.And(
            ExecutionFilter,
            Builders<MeetingAttendee>.Filter.Eq(x => x.UserId, userId),
            Builders<MeetingAttendee>.Filter.Ne(x => x.InvitationResponse, InvitationResponse.Declined));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task UpdateAttendanceStatusAsync(
        Guid meetingId, Guid userId, AttendanceStatus status, CancellationToken ct = default)
        => await Collection.UpdateOneAsync(AttendeeFilter(meetingId, userId), AttendanceUpdate(status), cancellationToken: ct);

    public async Task UpdateAttendanceStatusAsync(
        IPlatformTransactionSession session, Guid meetingId, Guid userId, AttendanceStatus status, CancellationToken ct = default)
        => await Collection.UpdateOneAsync(
            PlatformMongoTransactionSession.Require(session, _dbContext), AttendeeFilter(meetingId, userId), AttendanceUpdate(status),
            cancellationToken: ct);

    private FilterDefinition<MeetingAttendee> AttendeeFilter(Guid meetingId, Guid userId)
        => Builders<MeetingAttendee>.Filter.And(
            ExecutionFilter,
            Builders<MeetingAttendee>.Filter.Eq(x => x.MeetingId, meetingId),
            Builders<MeetingAttendee>.Filter.Eq(x => x.UserId, userId));

    private static UpdateDefinition<MeetingAttendee> AttendanceUpdate(AttendanceStatus status)
        => Builders<MeetingAttendee>.Update.Set(x => x.AttendanceStatus, status);

    public async Task<bool> MarkMailUndeliveredAsync(
        Guid meetingId, Guid userId, DateTimeOffset failedAt, CancellationToken ct = default)
    {
        var filter = Builders<MeetingAttendee>.Filter.And(
            ExecutionFilter,
            Builders<MeetingAttendee>.Filter.Eq(x => x.MeetingId, meetingId),
            Builders<MeetingAttendee>.Filter.Eq(x => x.UserId, userId));
        var update = Builders<MeetingAttendee>.Update.Set(x => x.MailUndeliveredAt, failedAt);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.MatchedCount > 0;
    }
}

/// <summary>Raw storage for <see cref="AgendaItem"/>.</summary>
public sealed class AgendaItemRepository : TenantRepository<AgendaItem>, IAgendaItemRepository
{
    public AgendaItemRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.MeetingAgendaItems)
    {
    }

    public async Task<IReadOnlyList<AgendaItem>> ListByMeetingIdAsync(Guid meetingId, CancellationToken ct = default)
    {
        var filter = Builders<AgendaItem>.Filter.And(
            ExecutionFilter,
            Builders<AgendaItem>.Filter.Eq(x => x.MeetingId, meetingId));
        return await Collection.Find(filter).SortBy(x => x.SortOrder).ToListAsync(ct);
    }

    public Task<bool> UpdateAsync(AgendaItem item, int expectedVersion, CancellationToken ct = default)
        => ReadVersionWrites.ReplaceAsync(Collection, ExecutionFilter, item, expectedVersion, ct);
}

/// <summary>Raw storage for <see cref="MeetingType"/>.</summary>
public sealed class MeetingTypeRepository : TenantRepository<MeetingType>, IMeetingTypeRepository
{
    public MeetingTypeRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.MeetingTypes)
    {
    }

    public async Task<IReadOnlyList<MeetingType>> ListAsync(CancellationToken ct = default)
        => await Collection.Find(ExecutionFilter).ToListAsync(ct);

    public Task<MeetingType?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        var filter = Builders<MeetingType>.Filter.And(
            ExecutionFilter,
            Builders<MeetingType>.Filter.Eq(x => x.Name, name));
        return Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public Task<bool> UpdateAsync(MeetingType type, int expectedVersion, CancellationToken ct = default)
        => ReadVersionWrites.ReplaceAsync(Collection, ExecutionFilter, type, expectedVersion, ct);
}

/// <summary>Raw storage for <see cref="MeetingMinutesVersion"/> — MOD-0357 S6. See the interface's own doc
/// comment for why this is append-only by convention rather than by anything enforced here.</summary>
public sealed class MeetingMinutesVersionRepository
    : TenantRepository<MeetingMinutesVersion>, IMeetingMinutesVersionRepository
{
    private readonly IPlatformDbContext _dbContext;

    public MeetingMinutesVersionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.MeetingMinutesVersions)
    {
        _dbContext = dbContext;
    }

    /// <summary>The one place a <c>MongoWriteException</c> from THIS collection is allowed to be caught
    /// (architecture rule) — a genuine version-number race (two concurrent first-drafts, or two concurrent
    /// corrections) is refused by the unique index and turned into "no" here, never a 500.</summary>
    public async Task<MeetingMinutesVersion?> TryCreateAsync(MeetingMinutesVersion version, CancellationToken ct = default)
    {
        try
        {
            return await CreateAsync(version, ct);
        }
        catch (MongoWriteException exception) when (
            exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return null;
        }
    }

    /// <summary>BL-533 — the same insert inside the caller's Platform transaction (a correction). A refusal by the unique
    /// index aborts the transaction on the server; the caller aborts its side and answers 409.</summary>
    public async Task<MeetingMinutesVersion?> TryCreateAsync(
        IPlatformTransactionSession session, MeetingMinutesVersion version, CancellationToken ct = default)
    {
        try
        {
            return await ReadVersionWrites.InsertAsync(
                Collection, PlatformMongoTransactionSession.Require(session, _dbContext), version, TenantContext.TenantId, ct);
        }
        catch (MongoWriteException exception) when (
            exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return null;
        }
    }

    public Task<MeetingMinutesVersion?> GetLatestByMeetingIdAsync(Guid meetingId, CancellationToken ct = default)
    {
        var filter = Builders<MeetingMinutesVersion>.Filter.And(
            ExecutionFilter,
            Builders<MeetingMinutesVersion>.Filter.Eq(x => x.MeetingId, meetingId));
        return Collection.Find(filter)
            .SortByDescending(x => x.VersionNumber)
            .Limit(1)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<MeetingMinutesVersion>> ListByMeetingIdAsync(Guid meetingId, CancellationToken ct = default)
    {
        var filter = Builders<MeetingMinutesVersion>.Filter.And(
            ExecutionFilter,
            Builders<MeetingMinutesVersion>.Filter.Eq(x => x.MeetingId, meetingId));
        return await Collection.Find(filter).SortByDescending(x => x.VersionNumber).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MeetingMinutesVersion>> ListPublishedByMeetingIdsAsync(
        IReadOnlyCollection<Guid> meetingIds, CancellationToken ct = default)
    {
        if (meetingIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<MeetingMinutesVersion>.Filter.And(
            ExecutionFilter,
            Builders<MeetingMinutesVersion>.Filter.In(x => x.MeetingId, meetingIds),
            Builders<MeetingMinutesVersion>.Filter.Eq(x => x.Status, MinutesStatus.Published));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public Task<bool> UpdateAsync(MeetingMinutesVersion version, int expectedVersion, CancellationToken ct = default)
        => ReadVersionWrites.ReplaceAsync(Collection, ExecutionFilter, version, expectedVersion, ct);

    public Task<bool> UpdateAsync(
        IPlatformTransactionSession session, MeetingMinutesVersion version, int expectedVersion, CancellationToken ct = default)
        => ReadVersionWrites.ReplaceAsync(
            Collection, PlatformMongoTransactionSession.Require(session, _dbContext), ExecutionFilter, version, expectedVersion, ct);
}

/// <summary>Raw storage for <see cref="MeetingSeries"/> (MOD-0357 S11).</summary>
public sealed class MeetingSeriesRepository : TenantRepository<MeetingSeries>, IMeetingSeriesRepository
{
    public MeetingSeriesRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.MeetingSeries)
    {
    }

    // CreateAsync is NOT overridden here — TenantRepository<T>'s own base implementation already stamps
    // TenantId from the ambient context on every write (defensively, regardless of what the candidate object
    // was constructed with), the same convention MeetingTypeRepository already relies on without overriding it.

    public async Task<MeetingSeries?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<MeetingSeries>.Filter.And(ExecutionFilter, Builders<MeetingSeries>.Filter.Eq(x => x.Id, id));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    // Sorted by Name — a STRING field. BL-030: this entity carries three DateTimeOffset fields (StartsAt,
    // EndsAt, LastGeneratedAt), and a server-side sort on any of them is not trusted for exact chronological
    // order in this codebase (no DateTimeOffsetSerializer registered — see MeetingRepository.
    // FindByFollowUpOfMeetingIdAsync's own doc comment, measured broken against a real server). Neither list
    // below needs date order at all, so the safe field is used instead of adding a needless in-memory sort.
    public async Task<IReadOnlyList<MeetingSeries>> ListAllAsync(CancellationToken ct = default)
        => await Collection.Find(ExecutionFilter).SortBy(x => x.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<MeetingSeries>> ListActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<MeetingSeries>.Filter.And(
            ExecutionFilter,
            Builders<MeetingSeries>.Filter.Eq(x => x.IsActive, true));
        return await Collection.Find(filter).SortBy(x => x.Name).ToListAsync(ct);
    }

    public Task<MeetingSeries?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        var filter = Builders<MeetingSeries>.Filter.And(
            ExecutionFilter,
            Builders<MeetingSeries>.Filter.Eq(x => x.Name, name));
        return Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public Task<bool> UpdateAsync(MeetingSeries series, int expectedVersion, CancellationToken ct = default)
        => ReadVersionWrites.ReplaceAsync(Collection, ExecutionFilter, series, expectedVersion, ct);
}
