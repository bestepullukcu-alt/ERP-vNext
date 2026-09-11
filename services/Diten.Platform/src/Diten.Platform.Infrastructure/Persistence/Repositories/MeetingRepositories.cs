using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
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
}
