using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;
using Diten.Platform.Application.Security;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAdministrators;

/// <summary>
/// BL-521 FIX5 item 7 — A BULK DELETE IS HONEST. It decided each target as it came and deleted as it went, so a LATER refusal
/// (the last active super administrator, a concurrent change, a 503) answered an error with the earlier targets already gone,
/// and the same id listed twice deleted once and answered 409.
///
/// <para>Now the WHOLE set is decided FIRST — versions, the system seed, and the last-super-administrator rule over the set as
/// one (taking every active platform super administrator out in one request is refused although each of them alone could be
/// removed) — a repeated id is 400, and when any of that refuses, NOTHING is deleted. If a refusal still comes while deleting
/// (another request changed a target in between), the answer says which administrators were deleted and which one was
/// refused: the batch is not a single transaction (a non-super-administrator delete needs no transaction, and a standalone
/// development database has none), so it reports instead of pretending.</para>
///
/// <para>Production handler, production safety guard and repository over a test-owned replica set — never the shared
/// database.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class PlatformAdministratorBulkDeleteMongoTests
{
    [Fact]
    public async Task A_set_that_would_remove_every_active_super_admin_is_refused_as_a_whole_and_nothing_is_deleted()
    {
        await using var world = await World.StartAsync();
        var first = await world.SeedAsync(AdministratorRole.SuperAdmin);
        var second = await world.SeedAsync(AdministratorRole.SuperAdmin);
        var actor = await world.SeedAsync(AdministratorRole.SupportAdmin);

        var response = await world.BulkDeleteAsync(actor, world.Repository, [first, second]);

        Assert.True(response.StatusCode == 409, Describe(response));
        Assert.Equal(AdminSafetyReasonCodes.LastActiveSuperAdmin, response.ReasonCode);
        Assert.False((await world.ReadAsync(first.Id)).IsDeleted);
        Assert.False((await world.ReadAsync(second.Id)).IsDeleted);
    }

    /// <summary>The control: the same request with one active super administrator left standing is carried out whole.</summary>
    [Fact]
    public async Task A_set_that_leaves_a_super_admin_standing_is_deleted_whole()
    {
        await using var world = await World.StartAsync();
        var first = await world.SeedAsync(AdministratorRole.SuperAdmin);
        var second = await world.SeedAsync(AdministratorRole.SuperAdmin);
        var kept = await world.SeedAsync(AdministratorRole.SuperAdmin);
        var actor = await world.SeedAsync(AdministratorRole.SupportAdmin);

        var response = await world.BulkDeleteAsync(actor, world.Repository, [first, second]);

        Assert.True(response.StatusCode == 204, Describe(response));
        Assert.True((await world.ReadAsync(first.Id)).IsDeleted);
        Assert.True((await world.ReadAsync(second.Id)).IsDeleted);
        Assert.False((await world.ReadAsync(kept.Id)).IsDeleted);
    }

    [Fact]
    public async Task The_same_administrator_listed_twice_is_refused_400_and_nothing_is_deleted()
    {
        await using var world = await World.StartAsync();
        var one = await world.SeedAsync(AdministratorRole.ReadOnly);
        var two = await world.SeedAsync(AdministratorRole.ReadOnly);
        var actor = await world.SeedAsync(AdministratorRole.SupportAdmin);

        var response = await world.BulkDeleteAsync(actor, world.Repository, [one, two, one]);

        Assert.True(response.StatusCode == 400, Describe(response));
        Assert.Equal(AdminSafetyReasonCodes.BulkDuplicateIds, response.ReasonCode);
        Assert.False((await world.ReadAsync(one.Id)).IsDeleted);
        Assert.False((await world.ReadAsync(two.Id)).IsDeleted);
    }

    /// <summary>
    /// A target changed by another request AFTER the whole set was decided (here: right after the first delete): the answer is
    /// a refusal that SAYS what happened — the first was deleted, the second was refused, the third was never processed.
    /// </summary>
    [Fact]
    public async Task A_refusal_after_some_targets_were_deleted_says_what_was_deleted_and_what_was_refused()
    {
        await using var world = await World.StartAsync();
        var first = await world.SeedAsync(AdministratorRole.ReadOnly);
        var second = await world.SeedAsync(AdministratorRole.ReadOnly);
        var third = await world.SeedAsync(AdministratorRole.ReadOnly);
        var actor = await world.SeedAsync(AdministratorRole.SupportAdmin);
        var interfering = new ChangesTheSecondTargetAfterTheFirstDelete(world.Repository, world, second.Id);

        var response = await world.BulkDeleteAsync(actor, interfering, [first, second, third]);

        Assert.True(response.StatusCode == 409, Describe(response));
        Assert.Equal(AdminSafetyReasonCodes.BulkDeletePartial, response.ReasonCode);
        Assert.Equal(AdminSafetyMessages.BulkDeletePartial, response.Errors[0]);
        Assert.Contains(first.Id.ToString(), response.Errors.Single(e => e.StartsWith("deleted", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains(second.Id.ToString(), response.Errors.Single(e => e.StartsWith("refused", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.DoesNotContain(third.Id.ToString(), string.Join(" ", response.Errors.Skip(1)), StringComparison.Ordinal);
        Assert.True((await world.ReadAsync(first.Id)).IsDeleted);
        Assert.False((await world.ReadAsync(second.Id)).IsDeleted);
        Assert.False((await world.ReadAsync(third.Id)).IsDeleted);
    }

    private static string Describe(Response<NoContent> response) =>
        $"{response.StatusCode} {response.ReasonCode} {string.Join("; ", response.Errors)}";

    // ── world ───────────────────────────────────────────────────────────────────────────────────────────

    private sealed class World : IAsyncDisposable
    {
        private readonly DisposableMongoReplicaSet _mongo;
        private readonly IMongoDatabase _database;

        private World(DisposableMongoReplicaSet mongo, IMongoDatabase database)
        {
            _mongo = mongo;
            _database = database;
            Repository = new PlatformAdministratorRepository(new PlatformDbContext(mongo.Client, database), new TenantContext());
        }

        public PlatformAdministratorRepository Repository { get; }

        public static async Task<World> StartAsync()
        {
            var mongo = await DisposableMongoReplicaSet.StartAsync();
            var database = mongo.CreateDatabase();
            foreach (var collection in PlatformSchemaManifest.For(Enum.GetValues<SchemaProfile>())
                         .Where(c => c.Name is PlatformCollections.PlatformAdministrators or PlatformCollections.PlatformAdministratorSafetyGuards))
            {
                await collection.ApplyAsync(database, CancellationToken.None);
            }

            return new World(mongo, database);
        }

        public async Task<PlatformAdministrator> SeedAsync(AdministratorRole role)
        {
            var email = $"bulk.{Guid.NewGuid():N}@bl521.test";
            var administrator = new PlatformAdministrator
            {
                Email = email, NormalizedEmail = email, UserName = email, NormalizedUserName = email, DisplayName = "Bulk Probe",
                ActorType = ActorType.PlatformAdmin, AllowedTenantIds = [], Roles = [role], Status = AdministratorStatus.Active,
                InvitationStatus = AdministratorInvitationStatus.Accepted, Version = 1, CreatedBy = "bl521-fix5"
            };
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).InsertOneAsync(administrator);
            return administrator;
        }

        public async Task<PlatformAdministrator> ReadAsync(Guid id) =>
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).Find(x => x.Id == id).SingleAsync();

        public Task BumpVersionAsync(Guid id) =>
            _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators)
                .UpdateOneAsync(x => x.Id == id, Builders<PlatformAdministrator>.Update.Inc(x => x.Version, 1));

        /// <summary>The client sends the version it saw for each target (the stored one, read just before the request).</summary>
        public async Task<Response<NoContent>> BulkDeleteAsync(PlatformAdministrator actingAs, IPlatformAdministratorRepository repository, IReadOnlyList<PlatformAdministrator> targets)
        {
            var user = new Actor(actingAs.Email);
            var items = new List<PlatformAdministratorBulkDeleteItemRequest>();
            foreach (var target in targets)
            {
                items.Add(new PlatformAdministratorBulkDeleteItemRequest(target.Id, (await ReadAsync(target.Id)).Version));
            }

            return await new BulkDeletePlatformAdministratorsHandler(repository, user, new ActorSafetyGuard(user, repository))
                .Handle(new BulkDeletePlatformAdministratorsCommand(items), CancellationToken.None);
        }

        public ValueTask DisposeAsync() => _mongo.DisposeAsync();
    }

    /// <summary>The production repository, except that another request changes the SECOND target the moment the first delete
    /// has been written (after the whole set was decided, before the second delete).</summary>
    private sealed class ChangesTheSecondTargetAfterTheFirstDelete(IPlatformAdministratorRepository inner, World world, Guid secondTarget) : IPlatformAdministratorRepository
    {
        private int _deletes;

        public async Task<bool> SoftDeleteAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default)
        {
            var deleted = await inner.SoftDeleteAsync(id, expectedVersion, actorName, ct);
            if (Interlocked.Increment(ref _deletes) == 1)
            {
                await world.BumpVersionAsync(secondTarget);
            }

            return deleted;
        }

        public Task<PlatformAdministrator> CreateAsync(PlatformAdministrator administrator, CancellationToken ct = default) => inner.CreateAsync(administrator, ct);
        public Task<PlatformAdministrator?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<PlatformAdministrator?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default) => inner.GetByNormalizedEmailAsync(normalizedEmail, ct);
        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByEmailAsync(normalizedEmail, excludeId, ct);
        public Task<bool> ExistsByUserNameAsync(string normalizedUserName, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByUserNameAsync(normalizedUserName, excludeId, ct);
        public Task<bool> UpdateAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default, AuthSyncPendingEffect authSyncPending = AuthSyncPendingEffect.Keep) => inner.UpdateAsync(administrator, expectedVersion, ct, authSyncPending);
        public Task<(IReadOnlyList<PlatformAdministrator> Items, long TotalCount)> QueryAsync(PlatformAdministratorQuery query, CancellationToken ct = default) => inner.QueryAsync(query, ct);
        public Task<PlatformAdministratorStatsSnapshot> GetStatsAsync(CancellationToken ct = default) => inner.GetStatsAsync(ct);
        public Task<long> CountActiveSuperAdminsAsync(Guid? excludeId = null, CancellationToken ct = default) => inner.CountActiveSuperAdminsAsync(excludeId, ct);
        public Task<SuperAdminGuardedWriteResult> UpdateGuardedAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default, AuthSyncPendingEffect authSyncPending = AuthSyncPendingEffect.Keep) => inner.UpdateGuardedAsync(administrator, expectedVersion, ct, authSyncPending);
        public Task<SuperAdminGuardedWriteResult> SoftDeleteGuardedAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default) => inner.SoftDeleteGuardedAsync(id, expectedVersion, actorName, ct);
        public Task MarkAuthSyncPendingAsync(Guid id, bool pending, CancellationToken ct = default) => inner.MarkAuthSyncPendingAsync(id, pending, ct);
        public Task<bool> RecordLoginAcceptedAsync(Guid id, DateTimeOffset acceptedAtUtc, CancellationToken ct = default) => inner.RecordLoginAcceptedAsync(id, acceptedAtUtc, ct);
    }

    private sealed class Actor(string email) : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string? Email => email;
        public string? DisplayName => "Acting Administrator";
        public string ActorName => email;
        public bool IsAuthenticated => true;
    }
}
