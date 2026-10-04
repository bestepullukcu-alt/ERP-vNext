using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class PlatformAdministratorRepository
    : GlobalRepository<PlatformAdministrator>, IPlatformAdministratorRepository
{
    /// <summary>BL-521 FIX3 item 3 — the id of the one document every write under the super-admin guard claims.</summary>
    internal const string SuperAdminGuardId = "active-platform-super-admins";

    private const int MaximumCommitAttempts = 3;
    private const string UnknownCommitResultLabel = "UnknownTransactionCommitResult";

    private readonly IMongoClient _client;
    private readonly IMongoCollection<BsonDocument> _guards;
    private readonly Func<CancellationToken, Task>? _beforeGuardedCommit;
    private readonly Func<CancellationToken, Task>? _beforeGuardClaim;
    private readonly Func<int, bool, CancellationToken, Task>? _commitProbe;

    public PlatformAdministratorRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : this(dbContext, tenantContext, beforeGuardedCommit: null)
    {
    }

    /// <summary>
    /// TEST SEAMS. Production goes through the public constructor (no seam; DI sees only that one).
    /// <para><paramref name="beforeGuardedCommit"/> (BL-521 FIX3 item 3) runs inside a guarded write's transaction — after
    /// its claim, its count and its own write, just before its commit — so a test can hold one write exactly where a second
    /// one must be refused.</para>
    /// <para><paramref name="beforeGuardClaim"/> (FIX4 item 4) runs when a guarded write starts, BEFORE its transaction and
    /// its claim — after the handler's own pre-check — so a test can let another write commit exactly in between.</para>
    /// <para><paramref name="commitProbe"/> (FIX4 item 4) is called with (attempt, afterCommit) around every commit attempt,
    /// so a test can make a commit's result unknown before the commit was sent, or after it was durably written.</para>
    /// </summary>
    internal PlatformAdministratorRepository(
        IPlatformDbContext dbContext,
        ITenantContext tenantContext,
        Func<CancellationToken, Task>? beforeGuardedCommit,
        Func<CancellationToken, Task>? beforeGuardClaim = null,
        Func<int, bool, CancellationToken, Task>? commitProbe = null)
        : base(dbContext.Database, tenantContext, PlatformCollections.PlatformAdministrators)
    {
        _client = dbContext.Client;
        _guards = dbContext.Database.GetCollection<BsonDocument>(PlatformCollections.PlatformAdministratorSafetyGuards);
        _beforeGuardedCommit = beforeGuardedCommit;
        _beforeGuardClaim = beforeGuardClaim;
        _commitProbe = commitProbe;
    }

    public async Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<PlatformAdministrator>>
        {
            ExecutionFilter,
            Builders<PlatformAdministrator>.Filter.Eq(x => x.NormalizedEmail, normalizedEmail)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return await Collection.Find(Builders<PlatformAdministrator>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task<bool> ExistsByUserNameAsync(string normalizedUserName, Guid? excludeId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return false;
        }

        var filters = new List<FilterDefinition<PlatformAdministrator>>
        {
            ExecutionFilter,
            Builders<PlatformAdministrator>.Filter.Eq(x => x.NormalizedUserName, normalizedUserName)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return await Collection.Find(Builders<PlatformAdministrator>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task<bool> UpdateAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default)
    {
        administrator.UpdatedAt = DateTimeOffset.UtcNow;
        administrator.Version = expectedVersion + 1;

        var result = await Collection.ReplaceOneAsync(VersionFilter(administrator.Id, expectedVersion), administrator, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default)
    {
        var result = await Collection.UpdateOneAsync(VersionFilter(id, expectedVersion), SoftDelete(actorName), cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    /// <inheritdoc />
    public async Task MarkAuthSyncPendingAsync(Guid id, bool pending, CancellationToken ct = default)
    {
        // An operational mark, not a change of the administrator: the version does not move (a bump would refuse the
        // operator's next edit of the record as "changed by another user"). Setting keeps the FIRST unconverged moment.
        var byId = Builders<PlatformAdministrator>.Filter.Eq(x => x.Id, id);
        var filter = pending
            ? Builders<PlatformAdministrator>.Filter.And(byId, Builders<PlatformAdministrator>.Filter.Eq(x => x.AuthSyncPendingSinceUtc, null))
            : byId;
        var update = Builders<PlatformAdministrator>.Update.Set(x => x.AuthSyncPendingSinceUtc, pending ? DateTimeOffset.UtcNow : null);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    /// <inheritdoc />
    public Task<SuperAdminGuardedWriteResult> UpdateGuardedAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default) =>
        WriteUnderSuperAdminGuardAsync(
            administrator.Id,
            async session =>
            {
                administrator.UpdatedAt = DateTimeOffset.UtcNow;
                administrator.Version = expectedVersion + 1;
                var result = await Collection.ReplaceOneAsync(session, VersionFilter(administrator.Id, expectedVersion), administrator, cancellationToken: ct);
                return result.ModifiedCount == 1;
            },
            // FIX4 item 4 — written iff the record now sits at exactly the version this write produced, holding what it wrote.
            async token =>
            {
                var stored = await ReadIncludingDeletedAsync(administrator.Id, token);
                return stored is { IsDeleted: false }
                       && stored.Version == expectedVersion + 1
                       && HoldsTheSameAdministrator(stored, administrator);
            },
            ct);

    /// <inheritdoc />
    public Task<SuperAdminGuardedWriteResult> SoftDeleteGuardedAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default) =>
        WriteUnderSuperAdminGuardAsync(
            id,
            async session =>
            {
                var result = await Collection.UpdateOneAsync(session, VersionFilter(id, expectedVersion), SoftDelete(actorName), cancellationToken: ct);
                return result.ModifiedCount == 1;
            },
            async token =>
            {
                var stored = await ReadIncludingDeletedAsync(id, token);
                return stored is { IsDeleted: true }
                       && stored.Version == expectedVersion + 1
                       && string.Equals(stored.UpdatedBy, actorName, StringComparison.Ordinal);
            },
            ct);

    private Task<PlatformAdministrator?> ReadIncludingDeletedAsync(Guid id, CancellationToken ct) =>
        Collection.Find(Builders<PlatformAdministrator>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(ct)!;

    private static bool HoldsTheSameAdministrator(PlatformAdministrator stored, PlatformAdministrator written) =>
        string.Equals(stored.Email, written.Email, StringComparison.Ordinal)
        && string.Equals(stored.UserName, written.UserName, StringComparison.Ordinal)
        && string.Equals(stored.DisplayName, written.DisplayName, StringComparison.Ordinal)
        && stored.ActorType == written.ActorType
        && stored.PartnerId == written.PartnerId
        && stored.AllowedTenantIds.SequenceEqual(written.AllowedTenantIds)
        && stored.Status == written.Status
        && stored.Roles.SequenceEqual(written.Roles)
        && string.Equals(stored.LastStatusReason, written.LastStatusReason, StringComparison.Ordinal)
        && string.Equals(stored.UpdatedBy, written.UpdatedBy, StringComparison.Ordinal);

    /// <summary>
    /// BL-521 FIX3 item 3 — one transaction: (1) claim the guard document, (2) count the OTHER active platform super
    /// administrators, (3) write. The claim is a real write to ONE shared document, so a second guarded write that starts
    /// while this one is open conflicts on it (WriteConflict) and is refused; one that starts after this one committed claims
    /// cleanly and its count sees this write. Snapshot isolation alone would not do it: A writes B's record and B writes
    /// A's — two different documents — and both counts would pass (write skew); the shared claim turns that into a conflict.
    /// </summary>
    private async Task<SuperAdminGuardedWriteResult> WriteUnderSuperAdminGuardAsync(
        Guid targetId,
        Func<IClientSessionHandle, Task<bool>> write,
        Func<CancellationToken, Task<bool>> landed,
        CancellationToken ct)
    {
        if (_beforeGuardClaim is not null)
        {
            await _beforeGuardClaim(ct);
        }

        try
        {
            for (var attempt = 1; ; attempt++)
            {
                using var session = await _client.StartSessionAsync(cancellationToken: ct);
                session.StartTransaction();
                try
                {
                    if (!await ClaimSuperAdminGuardAsync(session, ct))
                    {
                        // The guard document does not exist yet (the first guarded write this database sees): create it
                        // outside the transaction, once, and start over. It is never deleted.
                        await AbortQuietlyAsync(session);
                        if (attempt > 1)
                        {
                            return SuperAdminGuardedWriteResult.Unavailable;
                        }

                        await EnsureSuperAdminGuardAsync(ct);
                        continue;
                    }

                    var remaining = await Collection.CountDocumentsAsync(session, ActiveSuperAdminsFilter(targetId), cancellationToken: ct);
                    if (remaining == 0)
                    {
                        await AbortQuietlyAsync(session);
                        return SuperAdminGuardedWriteResult.LastActiveSuperAdmin;
                    }

                    if (!await write(session))
                    {
                        await AbortQuietlyAsync(session);
                        return SuperAdminGuardedWriteResult.VersionConflict;
                    }

                    if (_beforeGuardedCommit is not null)
                    {
                        await _beforeGuardedCommit(ct);
                    }

                    await CommitAsync(session, ct);
                    return SuperAdminGuardedWriteResult.Written;
                }
                catch
                {
                    await AbortQuietlyAsync(session);
                    throw;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (MongoException exception) when (exception.HasErrorLabel(UnknownCommitResultLabel))
        {
            // BL-521 FIX4 item 4 — the commit was sent (or may have been) and its answer never came back, even after the
            // commit-only retries: it may well have been written. Before, this was answered "not written" (503) while the
            // record could already say otherwise. The record is read again and decides.
            return await LandedAsync(landed) ? SuperAdminGuardedWriteResult.Written : SuperAdminGuardedWriteResult.Unavailable;
        }
        catch (MongoException exception) when (IsConcurrentGuardClaim(exception))
        {
            return SuperAdminGuardedWriteResult.ConcurrentChange;
        }
        catch (MongoException)
        {
            // No replica set (transactions refused), the database unreachable: the write did not happen under the guard,
            // so it is answered as not having happened.
            return SuperAdminGuardedWriteResult.Unavailable;
        }
        catch (NotSupportedException)
        {
            return SuperAdminGuardedWriteResult.Unavailable;
        }
    }

    private async Task<bool> ClaimSuperAdminGuardAsync(IClientSessionHandle session, CancellationToken ct)
    {
        var claim = await _guards.UpdateOneAsync(
            session,
            SuperAdminGuardFilter,
            Builders<BsonDocument>.Update.Inc("Generation", 1L).Set("ClaimedAt", DateTime.UtcNow),
            cancellationToken: ct);
        return claim.MatchedCount == 1;
    }

    private async Task EnsureSuperAdminGuardAsync(CancellationToken ct)
    {
        try
        {
            await _guards.UpdateOneAsync(
                SuperAdminGuardFilter,
                Builders<BsonDocument>.Update.SetOnInsert("Generation", 0L),
                new UpdateOptions { IsUpsert = true },
                ct);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another request created it at the same moment: it exists, which is all this needs.
        }
    }

    private async Task CommitAsync(IClientSessionHandle session, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (_commitProbe is not null)
                {
                    await _commitProbe(attempt, false, ct);
                }

                await session.CommitTransactionAsync(ct);

                if (_commitProbe is not null)
                {
                    await _commitProbe(attempt, true, ct);
                }

                return;
            }
            catch (MongoException exception) when (exception.HasErrorLabel(UnknownCommitResultLabel) && attempt < MaximumCommitAttempts)
            {
                // Commit-only retry on the same session; the body is never replayed.
            }
        }
    }

    /// <summary>FIX4 item 4 — whether an unknown commit wrote: the record read again, outside the transaction. A read that
    /// fails too cannot say it did, so it did not (the caller converges Auth to whatever the record holds).</summary>
    private static async Task<bool> LandedAsync(Func<CancellationToken, Task<bool>> landed)
    {
        try
        {
            return await landed(CancellationToken.None);
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            return false;
        }
    }

    private static async Task AbortQuietlyAsync(IClientSessionHandle session)
    {
        if (!session.IsInTransaction)
        {
            return;
        }

        try
        {
            await session.AbortTransactionAsync(CancellationToken.None);
        }
        catch (MongoException)
        {
            // The server already ended it (a write conflict aborts the transaction): nothing left to abort.
        }
    }

    /// <summary>Another guarded write holds the claim (or committed past this one's snapshot): a conflict, not an outage.
    /// A connection failure is an outage, even when the driver labels it transient.</summary>
    private static bool IsConcurrentGuardClaim(MongoException exception) =>
        exception is not MongoConnectionException
        && (exception is MongoCommandException { Code: 112 }
            || exception is MongoWriteException { WriteError.Code: 112 }
            || exception.HasErrorLabel("TransientTransactionError"));

    private static FilterDefinition<BsonDocument> SuperAdminGuardFilter =>
        Builders<BsonDocument>.Filter.Eq("_id", SuperAdminGuardId);

    private FilterDefinition<PlatformAdministrator> VersionFilter(Guid id, int expectedVersion) =>
        Builders<PlatformAdministrator>.Filter.And(
            ExecutionFilter,
            Builders<PlatformAdministrator>.Filter.Eq(x => x.Id, id),
            Builders<PlatformAdministrator>.Filter.Eq(x => x.Version, expectedVersion));

    private static UpdateDefinition<PlatformAdministrator> SoftDelete(string actorName) =>
        Builders<PlatformAdministrator>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedBy, actorName)
            .Inc(x => x.Version, 1);

    public async Task<(IReadOnlyList<PlatformAdministrator> Items, long TotalCount)> QueryAsync(
        PlatformAdministratorQuery query,
        CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<PlatformAdministrator>> { ExecutionFilter };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var escaped = System.Text.RegularExpressions.Regex.Escape(query.Search.Trim());
            var regex = new BsonRegularExpression(escaped, "i");
            filters.Add(Builders<PlatformAdministrator>.Filter.Or(
                Builders<PlatformAdministrator>.Filter.Regex(x => x.Email, regex),
                Builders<PlatformAdministrator>.Filter.Regex(x => x.UserName, regex),
                Builders<PlatformAdministrator>.Filter.Regex(x => x.DisplayName, regex)));
        }

        if (query.Statuses is { Count: > 0 })
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.In(x => x.Status, query.Statuses));
        }

        if (query.ActorTypes is { Count: > 0 })
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.In(x => x.ActorType, query.ActorTypes));
        }

        if (query.Roles is { Count: > 0 })
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.AnyIn(x => x.Roles, query.Roles));
        }

        if (query.InvitationStatuses is { Count: > 0 })
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.In(x => x.InvitationStatus, query.InvitationStatuses));
        }

        if (query.PartnerId.HasValue)
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.Eq(x => x.PartnerId, query.PartnerId.Value));
        }

        var filter = Builders<PlatformAdministrator>.Filter.And(filters);
        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await Collection.Find(filter)
            .Sort(BuildSort(query.Sort))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<PlatformAdministratorStatsSnapshot> GetStatsAsync(CancellationToken ct = default)
    {
        var total = await Collection.CountDocumentsAsync(ExecutionFilter, cancellationToken: ct);
        var active = await CountStatusAsync(AdministratorStatus.Active, ct);
        var suspended = await CountStatusAsync(AdministratorStatus.Suspended, ct);
        var disabled = await CountStatusAsync(AdministratorStatus.Disabled, ct);
        var pending = await Collection.CountDocumentsAsync(
            Builders<PlatformAdministrator>.Filter.And(
                ExecutionFilter,
                Builders<PlatformAdministrator>.Filter.Eq(x => x.InvitationStatus, AdministratorInvitationStatus.PendingInvitation)),
            cancellationToken: ct);

        return new PlatformAdministratorStatsSnapshot(total, active, suspended, disabled, pending);
    }

    private Task<long> CountStatusAsync(AdministratorStatus status, CancellationToken ct) =>
        Collection.CountDocumentsAsync(
            Builders<PlatformAdministrator>.Filter.And(
                ExecutionFilter,
                Builders<PlatformAdministrator>.Filter.Eq(x => x.Status, status)),
            cancellationToken: ct);

    public Task<PlatformAdministrator?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return Task.FromResult<PlatformAdministrator?>(null);
        }

        var email = normalizedEmail.Trim().ToLowerInvariant();
        var emailRegex = new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(email)}$", "i");
        var filter = Builders<PlatformAdministrator>.Filter.And(
            ExecutionFilter,
            Builders<PlatformAdministrator>.Filter.Or(
                Builders<PlatformAdministrator>.Filter.Eq(x => x.NormalizedEmail, email),
                Builders<PlatformAdministrator>.Filter.Regex(x => x.NormalizedEmail, emailRegex),
                Builders<PlatformAdministrator>.Filter.Regex(x => x.Email, emailRegex)));

        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    /// <summary>
    /// The live, active PLATFORM administrators holding SuperAdmin (optionally without one id). BL-521 FIX2 item 5 — a
    /// PartnerAdmin record carrying the SuperAdmin role is not a remaining super administrator: since FIX1 its record makes
    /// it a partner whatever its roles say, so counting it let the last real one be demoted, suspended or deleted.
    /// </summary>
    public Task<long> CountActiveSuperAdminsAsync(Guid? excludeId = null, CancellationToken ct = default) =>
        Collection.CountDocumentsAsync(ActiveSuperAdminsFilter(excludeId), cancellationToken: ct);

    /// <summary>The one definition of "an active platform super administrator" — the pre-check count and the count
    /// inside the guarded write (FIX3 item 3) both use it.</summary>
    private FilterDefinition<PlatformAdministrator> ActiveSuperAdminsFilter(Guid? excludeId)
    {
        var filters = new List<FilterDefinition<PlatformAdministrator>>
        {
            ExecutionFilter,
            Builders<PlatformAdministrator>.Filter.Eq(x => x.Status, AdministratorStatus.Active),
            Builders<PlatformAdministrator>.Filter.Eq(x => x.ActorType, ActorType.PlatformAdmin),
            Builders<PlatformAdministrator>.Filter.AnyEq(x => x.Roles, AdministratorRole.SuperAdmin)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Builders<PlatformAdministrator>.Filter.And(filters);
    }

    private static SortDefinition<PlatformAdministrator> BuildSort(string? sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort) ? "displayName" : sort.Trim();
        var descending = normalized.StartsWith("-", StringComparison.Ordinal);
        var field = descending ? normalized[1..] : normalized;

        if (field.Contains(':', StringComparison.Ordinal))
        {
            var parts = field.Split(':', 2, StringSplitOptions.TrimEntries);
            field = parts[0];
            descending = parts.Length > 1 && string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase);
        }

        return field.ToLowerInvariant() switch
        {
            "email" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.Email) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.Email),
            "username" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.UserName) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.UserName),
            "displayname" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.DisplayName) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.DisplayName),
            "actortype" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.ActorType) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.ActorType),
            "status" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.Status) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.Status),
            "invitationstatus" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.InvitationStatus) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.InvitationStatus),
            "lastloginatutc" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.LastLoginAtUtc) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.LastLoginAtUtc),
            "createdat" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.CreatedAt) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.CreatedAt),
            "updatedat" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.UpdatedAt) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.UpdatedAt),
            _ => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.DisplayName) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.DisplayName)
        };
    }
}
