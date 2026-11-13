using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

/// <summary>Raw storage for <see cref="InternalDispatchKey"/> (WP-VW-W5 W5-FIX2). The unique (tenant, source module, key)
/// index is the claim: the first insert wins, a later one is a duplicate key and reads what holds the row. Every write after
/// the insert is conditional on the claim token, so a taken-over claim's late completion lands nowhere.</summary>
public sealed class InternalDispatchKeyRepository : TenantRepository<InternalDispatchKey>, IInternalDispatchKeyRepository
{
    public InternalDispatchKeyRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.InternalDispatchKeys)
    {
    }

    public async Task<InternalDispatchKeyClaim> ClaimAsync(
        string sourceModule, string key, DateTimeOffset now, TimeSpan staleAfter, CancellationToken ct = default)
    {
        var fresh = new InternalDispatchKey
        {
            TenantId = TenantContext.TenantId,
            SourceModule = sourceModule,
            Key = key,
            State = InternalDispatchKeyStates.Pending,
            ClaimToken = Guid.NewGuid(),
            ClaimedAtUtcTicks = now.UtcTicks,
            CreatedBy = "system"
        };

        try
        {
            await CreateAsync(fresh, ct);
            return new InternalDispatchKeyClaim(InternalDispatchKeyOutcome.Claimed, fresh);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
        }

        var existing = await GetAsync(sourceModule, key, ct);
        if (existing is null)
        {
            // Unreachable while the row is never deleted; answered as in flight so the caller retries later.
            return new InternalDispatchKeyClaim(InternalDispatchKeyOutcome.InFlight, fresh);
        }

        if (existing.State == InternalDispatchKeyStates.Done)
        {
            return new InternalDispatchKeyClaim(InternalDispatchKeyOutcome.Done, existing);
        }

        var takeOver = existing.State == InternalDispatchKeyStates.Released
                       || (existing.State == InternalDispatchKeyStates.Pending && existing.ClaimedAtUtcTicks < (now - staleAfter).UtcTicks);
        if (!takeOver)
        {
            return new InternalDispatchKeyClaim(InternalDispatchKeyOutcome.InFlight, existing);
        }

        var token = Guid.NewGuid();
        var result = await Collection.UpdateOneAsync(
            Builders<InternalDispatchKey>.Filter.And(
                ExecutionFilter,
                Builders<InternalDispatchKey>.Filter.Eq(x => x.Id, existing.Id),
                Builders<InternalDispatchKey>.Filter.Eq(x => x.ClaimToken, existing.ClaimToken),
                Builders<InternalDispatchKey>.Filter.Eq(x => x.State, existing.State)),
            Builders<InternalDispatchKey>.Update
                .Set(x => x.State, InternalDispatchKeyStates.Pending)
                .Set(x => x.ClaimToken, token)
                .Set(x => x.ClaimedAtUtcTicks, now.UtcTicks)
                .Set(x => x.UpdatedAt, now)
                .Inc(x => x.Version, 1),
            cancellationToken: ct);
        if (result.ModifiedCount == 1)
        {
            existing.State = InternalDispatchKeyStates.Pending;
            existing.ClaimToken = token;
            existing.ClaimedAtUtcTicks = now.UtcTicks;
            return new InternalDispatchKeyClaim(InternalDispatchKeyOutcome.Claimed, existing);
        }

        // Another call took it over first.
        var current = await GetAsync(sourceModule, key, ct) ?? existing;
        return new InternalDispatchKeyClaim(
            current.State == InternalDispatchKeyStates.Done ? InternalDispatchKeyOutcome.Done : InternalDispatchKeyOutcome.InFlight,
            current);
    }

    public async Task<InternalDispatchKey?> GetAsync(string sourceModule, string key, CancellationToken ct = default)
        => await Collection.Find(Builders<InternalDispatchKey>.Filter.And(
                Builders<InternalDispatchKey>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
                Builders<InternalDispatchKey>.Filter.Eq(x => x.SourceModule, sourceModule),
                Builders<InternalDispatchKey>.Filter.Eq(x => x.Key, key)))
            .FirstOrDefaultAsync(ct);

    public async Task<bool> CompleteAsync(Guid id, Guid claimToken, string resultJson, DateTimeOffset now, CancellationToken ct = default)
    {
        var result = await Collection.UpdateOneAsync(
            HeldBy(id, claimToken),
            Builders<InternalDispatchKey>.Update
                .Set(x => x.State, InternalDispatchKeyStates.Done)
                .Set(x => x.ResultJson, resultJson)
                .Set(x => x.CompletedAtUtcTicks, now.UtcTicks)
                .Set(x => x.UpdatedAt, now)
                .Inc(x => x.Version, 1),
            cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> ReleaseAsync(Guid id, Guid claimToken, DateTimeOffset now, CancellationToken ct = default)
    {
        var result = await Collection.UpdateOneAsync(
            HeldBy(id, claimToken),
            Builders<InternalDispatchKey>.Update
                .Set(x => x.State, InternalDispatchKeyStates.Released)
                .Set(x => x.UpdatedAt, now)
                .Inc(x => x.Version, 1),
            cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    private FilterDefinition<InternalDispatchKey> HeldBy(Guid id, Guid claimToken) =>
        Builders<InternalDispatchKey>.Filter.And(
            ExecutionFilter,
            Builders<InternalDispatchKey>.Filter.Eq(x => x.Id, id),
            Builders<InternalDispatchKey>.Filter.Eq(x => x.ClaimToken, claimToken),
            Builders<InternalDispatchKey>.Filter.Eq(x => x.State, InternalDispatchKeyStates.Pending));
}
