using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

/// <summary>
/// MOD-0022 E1 — tickets. Filters name the tenant explicitly: the internal consume runs without a tenant JWT (the
/// /internal surface is tenant-agnostic), so the tenant comes from the request and is bound in the filter itself.
/// </summary>
public sealed class ESignReauthTicketRepository : RepositoryBase<ESignReauthTicket>, IESignReauthTicketRepository
{
    public const string CollectionName = "esignReauthTickets";

    public ESignReauthTicketRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, CollectionName) { }

    public Task CreateAsync(ESignReauthTicket ticket, CancellationToken ct) => InsertOneAsync(ticket, ct);

    /// <summary>
    /// The single-use guarantee: ONE conditional update. The filter carries every binding (hash, tenant, user, intent,
    /// purpose) plus "not consumed" and "not expired"; the first caller flips ConsumedAtUtc, every later one matches
    /// nothing. Returns the ticket as it was before the update (its ConsumedAtUtc null), or null.
    /// </summary>
    public async Task<ESignReauthTicket?> TryConsumeAsync(
        string ticketHash,
        Guid tenantId,
        Guid userId,
        string intent,
        DateTime nowUtc,
        string consumedBy,
        CancellationToken ct)
    {
        var f = Builders<ESignReauthTicket>.Filter;
        var filter = f.And(
            f.Eq(x => x.TicketHash, ticketHash),
            f.Eq(x => x.TenantId, tenantId),
            f.Eq(x => x.UserId, userId),
            f.Eq(x => x.Intent, intent),
            f.Eq(x => x.Purpose, ESignReauthTicket.PurposeESign),
            f.Eq(x => x.ConsumedAtUtc, null),
            f.Gt(x => x.ExpiresAtUtc, nowUtc),
            f.Eq(x => x.IsDeleted, false));
        var update = Builders<ESignReauthTicket>.Update
            .Set(x => x.ConsumedAtUtc, nowUtc)
            .Set(x => x.ConsumedBy, consumedBy)
            .Set(x => x.UpdatedAt, (DateTimeOffset?)DateTimeOffset.UtcNow);

        return await Collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<ESignReauthTicket> { ReturnDocument = ReturnDocument.Before }, ct);
    }
}

/// <summary>MOD-0022 E1 — KS-3 windows, one row per (tenant, user).</summary>
public sealed class ESignReauthWindowRepository : RepositoryBase<ESignReauthWindow>, IESignReauthWindowRepository
{
    public const string CollectionName = "esignReauthWindows";

    public ESignReauthWindowRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, CollectionName) { }

    /// <summary>Opens or replaces the user's window in place (upsert on the unique (TenantId, UserId) key).</summary>
    public async Task UpsertAsync(ESignReauthWindow window, CancellationToken ct)
    {
        var f = Builders<ESignReauthWindow>.Filter;
        var filter = f.And(f.Eq(x => x.TenantId, window.TenantId), f.Eq(x => x.UserId, window.UserId));
        var update = Builders<ESignReauthWindow>.Update
            .SetOnInsert(x => x.Id, window.Id)
            .SetOnInsert(x => x.CreatedAt, window.CreatedAt)
            .SetOnInsert(x => x.CreatedBy, window.CreatedBy)
            .Set(x => x.UserAgentHash, window.UserAgentHash)
            .Set(x => x.PasswordVerifiedAtUtc, window.PasswordVerifiedAtUtc)
            .Set(x => x.ExpiresAtUtc, window.ExpiresAtUtc)
            .Set(x => x.RevokedAtUtc, null)
            .Set(x => x.RevokedReason, null)
            .Set(x => x.IsDeleted, false)
            .Set(x => x.UpdatedAt, (DateTimeOffset?)DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
    }

    public async Task<ESignReauthWindow?> GetOpenAsync(Guid tenantId, Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var f = Builders<ESignReauthWindow>.Filter;
        var filter = f.And(
            f.Eq(x => x.TenantId, tenantId),
            f.Eq(x => x.UserId, userId),
            f.Eq(x => x.RevokedAtUtc, null),
            f.Gt(x => x.ExpiresAtUtc, nowUtc),
            f.Eq(x => x.IsDeleted, false));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<long> RevokeAllAsync(Guid tenantId, Guid userId, string reason, DateTime nowUtc, CancellationToken ct)
    {
        var f = Builders<ESignReauthWindow>.Filter;
        var filter = f.And(f.Eq(x => x.TenantId, tenantId), f.Eq(x => x.UserId, userId), f.Eq(x => x.RevokedAtUtc, null));
        var update = Builders<ESignReauthWindow>.Update
            .Set(x => x.RevokedAtUtc, nowUtc)
            .Set(x => x.RevokedReason, reason)
            .Set(x => x.UpdatedAt, (DateTimeOffset?)DateTimeOffset.UtcNow);
        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount;
    }
}
