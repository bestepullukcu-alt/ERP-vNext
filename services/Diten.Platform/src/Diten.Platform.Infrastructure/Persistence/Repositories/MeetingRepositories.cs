using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;
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
    public MeetingRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.MeetingMeetings)
    {
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

    public async Task<bool> UpdateAsync(Meeting meeting, int expectedVersion, CancellationToken ct = default)
    {
        meeting.Version = expectedVersion + 1;
        meeting.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<Meeting>.Filter.And(
            ExecutionFilter,
            Builders<Meeting>.Filter.Eq(x => x.Id, meeting.Id),
            Builders<Meeting>.Filter.Eq(x => x.Version, expectedVersion));

        var previous = await Collection.FindOneAndReplaceAsync(
            filter,
            meeting,
            new FindOneAndReplaceOptions<Meeting> { ReturnDocument = ReturnDocument.Before },
            ct);
        return previous is not null;
    }

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

    public async Task<bool> AnyByMeetingTypeIdAsync(Guid meetingTypeId, CancellationToken ct = default)
    {
        var filter = Builders<Meeting>.Filter.And(
            ExecutionFilter,
            Builders<Meeting>.Filter.Eq(x => x.MeetingTypeId, meetingTypeId));
        return await Collection.Find(filter).AnyAsync(ct);
    }
}

/// <summary>Raw storage for <see cref="MeetingAttendee"/>.</summary>
public sealed class MeetingAttendeeRepository : TenantRepository<MeetingAttendee>, IMeetingAttendeeRepository
{
    public MeetingAttendeeRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.MeetingAttendees)
    {
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

    public async Task<bool> UpdateAsync(AgendaItem item, int expectedVersion, CancellationToken ct = default)
    {
        item.Version = expectedVersion + 1;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<AgendaItem>.Filter.And(
            ExecutionFilter,
            Builders<AgendaItem>.Filter.Eq(x => x.Id, item.Id),
            Builders<AgendaItem>.Filter.Eq(x => x.Version, expectedVersion));

        var previous = await Collection.FindOneAndReplaceAsync(
            filter,
            item,
            new FindOneAndReplaceOptions<AgendaItem> { ReturnDocument = ReturnDocument.Before },
            ct);
        return previous is not null;
    }
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

    public async Task<bool> UpdateAsync(MeetingType type, int expectedVersion, CancellationToken ct = default)
    {
        type.Version = expectedVersion + 1;
        type.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<MeetingType>.Filter.And(
            ExecutionFilter,
            Builders<MeetingType>.Filter.Eq(x => x.Id, type.Id),
            Builders<MeetingType>.Filter.Eq(x => x.Version, expectedVersion));

        var previous = await Collection.FindOneAndReplaceAsync(
            filter,
            type,
            new FindOneAndReplaceOptions<MeetingType> { ReturnDocument = ReturnDocument.Before },
            ct);
        return previous is not null;
    }
}
