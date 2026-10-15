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
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAdministrators;

/// <summary>
/// BL-521 FIX5 items 8, 9 and 14 — THE "AUTH SYNC PENDING" MARK SURVIVES EVERY WRITE THAT IS NOT ITS OWN.
///
/// <para>Item 8 — the mark (<see cref="PlatformAdministrator.AuthSyncPendingSinceUtc"/>) is set without moving the record's
/// version, but every OTHER write replaced the whole document with the value it had LOADED: a mark set between a request's
/// read and its write was erased by it, and the drift it flags went unflagged. Now an ordinary write keeps the stored mark
/// (the replacement is applied as a pipeline that merges the stored mark back in); only an update that explicitly says so, and
/// a successful convergence, clear it.</para>
///
/// <para>Item 9 — the login acceptance Auth reports on every platform sign-in and refresh is a targeted <c>$set</c> of the
/// sign-in moment and the invitation state: the record's version, its <c>UpdatedBy</c> and the mark do not change, so it can
/// neither refuse an administrator's concurrent edit nor be refused by one.</para>
///
/// <para>Item 14 — the branches nothing measured: the mark keeps its FIRST moment when set twice; a convergence clears a mark
/// only when there is one.</para>
///
/// <para>Production repository over a test-owned replica set; the shared database is never touched.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class PlatformAdministratorAuthSyncMarkMongoTests
{
    // ── item 14: the mark's own branches ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Setting_the_mark_keeps_its_first_moment_and_clearing_removes_it_without_moving_the_version()
    {
        await using var world = await World.StartAsync();
        var target = await world.SeedAsync();

        await world.Repository.MarkAuthSyncPendingAsync(target.Id, pending: true);
        var first = (await world.ReadAsync(target.Id)).AuthSyncPendingSinceUtc;
        await Task.Delay(50); // a later moment than the first
        await world.Repository.MarkAuthSyncPendingAsync(target.Id, pending: true);
        var second = (await world.ReadAsync(target.Id)).AuthSyncPendingSinceUtc;

        Assert.NotNull(first);
        Assert.Equal(first, second);

        await world.Repository.MarkAuthSyncPendingAsync(target.Id, pending: false);
        var cleared = await world.ReadAsync(target.Id);
        Assert.Null(cleared.AuthSyncPendingSinceUtc);
        Assert.Equal(target.Version, cleared.Version);
    }

    // ── item 8: an ordinary write keeps a mark it did not load ───────────────────────────────────────────

    [Fact]
    public async Task An_ordinary_write_keeps_a_mark_that_was_set_after_the_record_was_loaded()
    {
        await using var world = await World.StartAsync();
        var target = await world.SeedAsync();
        var loaded = await world.ReadAsync(target.Id);
        await world.Repository.MarkAuthSyncPendingAsync(target.Id, pending: true); // another request, after this one loaded

        var loadedVersion = loaded.Version; // the write bumps the entity's own version in place
        loaded.DisplayName = "Written Without The Mark";
        var written = await world.Repository.UpdateAsync(loaded, loadedVersion);

        Assert.True(written);
        var stored = await world.ReadAsync(target.Id);
        Assert.NotNull(stored.AuthSyncPendingSinceUtc);
        Assert.Equal(("Written Without The Mark", loadedVersion + 1), (stored.DisplayName, stored.Version));
    }

    [Fact]
    public async Task A_guarded_write_keeps_a_mark_that_was_set_after_the_record_was_loaded()
    {
        await using var world = await World.StartAsync();
        var target = await world.SeedAsync(AdministratorRole.SuperAdmin);
        await world.SeedAsync(AdministratorRole.SuperAdmin); // the other one the rule needs left standing
        var loaded = await world.ReadAsync(target.Id);
        await world.Repository.MarkAuthSyncPendingAsync(target.Id, pending: true);

        loaded.Roles = [AdministratorRole.SupportAdmin];
        var outcome = await world.Repository.UpdateGuardedAsync(loaded, loaded.Version);

        Assert.Equal(SuperAdminGuardedWriteResult.Written, outcome);
        var stored = await world.ReadAsync(target.Id);
        Assert.NotNull(stored.AuthSyncPendingSinceUtc);
        Assert.Equal([AdministratorRole.SupportAdmin], stored.Roles);
    }

    /// <summary>The same through a real handler: the record is loaded, another request marks it, the suspend is written.</summary>
    [Fact]
    public async Task A_suspend_through_the_handler_keeps_a_mark_set_between_its_read_and_its_write()
    {
        await using var world = await World.StartAsync();
        var target = await world.SeedAsync();
        var actor = await world.SeedAsync(AdministratorRole.SuperAdmin);
        var repository = new MarksTheRecordAfterItIsLoaded(world.Repository);
        var user = new Actor(actor.Email);

        var response = await new SuspendPlatformAdministratorHandler(repository, user, new ActorSafetyGuard(user, repository))
            .Handle(new SuspendPlatformAdministratorCommand(target.Id, new PlatformAdministratorStatusRequest("fix5", target.Version)), CancellationToken.None);

        Assert.True(response.StatusCode == 204, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        var stored = await world.ReadAsync(target.Id);
        Assert.Equal(AdministratorStatus.Suspended, stored.Status);
        Assert.NotNull(stored.AuthSyncPendingSinceUtc);
    }

    [Fact]
    public async Task An_update_that_says_so_clears_the_mark_on_both_write_paths()
    {
        await using var world = await World.StartAsync();
        var plain = await world.SeedAsync();
        var guarded = await world.SeedAsync(AdministratorRole.SuperAdmin);
        await world.SeedAsync(AdministratorRole.SuperAdmin);
        await world.Repository.MarkAuthSyncPendingAsync(plain.Id, pending: true);
        await world.Repository.MarkAuthSyncPendingAsync(guarded.Id, pending: true);

        var plainLoaded = await world.ReadAsync(plain.Id);
        plainLoaded.AuthSyncPendingSinceUtc = null;
        Assert.True(await world.Repository.UpdateAsync(plainLoaded, plainLoaded.Version, authSyncPending: AuthSyncPendingEffect.Clear));
        var guardedLoaded = await world.ReadAsync(guarded.Id);
        guardedLoaded.AuthSyncPendingSinceUtc = null;
        guardedLoaded.Roles = [AdministratorRole.SupportAdmin];
        Assert.Equal(SuperAdminGuardedWriteResult.Written, await world.Repository.UpdateGuardedAsync(guardedLoaded, guardedLoaded.Version, authSyncPending: AuthSyncPendingEffect.Clear));

        Assert.Null((await world.ReadAsync(plain.Id)).AuthSyncPendingSinceUtc);
        Assert.Null((await world.ReadAsync(guarded.Id)).AuthSyncPendingSinceUtc);
    }

    // ── item 14: a convergence clears a mark only when there is one ──────────────────────────────────────

    [Fact]
    public async Task A_successful_convergence_clears_a_mark_and_writes_nothing_when_there_is_none()
    {
        await using var world = await World.StartAsync();
        var marked = await world.SeedAsync();
        var clean = await world.SeedAsync();
        await world.Repository.MarkAuthSyncPendingAsync(marked.Id, pending: true);
        var counting = new CountsMarkWrites(world.Repository);
        var convergence = new PlatformAdministratorAuthConvergence(counting, new AcceptsEverything(), NullLogger.Instance);

        await convergence.ConvergeToRecordAsync(clean.Id);
        Assert.Empty(counting.Writes);

        await convergence.ConvergeToRecordAsync(marked.Id);
        Assert.Equal([(marked.Id, false)], counting.Writes);
        Assert.Null((await world.ReadAsync(marked.Id)).AuthSyncPendingSinceUtc);
    }

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

        public async Task<PlatformAdministrator> SeedAsync(AdministratorRole role = AdministratorRole.SupportAdmin)
        {
            var email = $"mark.{Guid.NewGuid():N}@bl521.test";
            var administrator = new PlatformAdministrator
            {
                Email = email, NormalizedEmail = email, UserName = email, NormalizedUserName = email, DisplayName = "Mark Probe",
                ActorType = ActorType.PlatformAdmin, AllowedTenantIds = [], Roles = [role], Status = AdministratorStatus.Active,
                InvitationStatus = AdministratorInvitationStatus.Accepted, Version = 1, CreatedBy = "bl521-fix5"
            };
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).InsertOneAsync(administrator);
            return administrator;
        }

        public async Task<PlatformAdministrator> ReadAsync(Guid id) =>
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).Find(x => x.Id == id).SingleAsync();

        public ValueTask DisposeAsync() => _mongo.DisposeAsync();
    }

    private sealed class AcceptsEverything : IPlatformAdministratorProvisioningService
    {
        public Task<PlatformAdministratorProvisioningResult> ProvisionAsync(PlatformAdministratorProvisioningRequest request, CancellationToken ct) =>
            Task.FromResult(new PlatformAdministratorProvisioningResult(null, true));

        public Task SyncAsync(PlatformAdministratorProvisioningSyncRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>Delegates everything; counts the writes of the mark.</summary>
    private class Delegating(IPlatformAdministratorRepository inner) : IPlatformAdministratorRepository
    {
        public virtual Task<PlatformAdministrator?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public virtual Task MarkAuthSyncPendingAsync(Guid id, bool pending, CancellationToken ct = default) => inner.MarkAuthSyncPendingAsync(id, pending, ct);
        public Task<PlatformAdministrator> CreateAsync(PlatformAdministrator administrator, CancellationToken ct = default) => inner.CreateAsync(administrator, ct);
        public Task<PlatformAdministrator?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default) => inner.GetByNormalizedEmailAsync(normalizedEmail, ct);
        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByEmailAsync(normalizedEmail, excludeId, ct);
        public Task<bool> ExistsByUserNameAsync(string normalizedUserName, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByUserNameAsync(normalizedUserName, excludeId, ct);
        public Task<bool> UpdateAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default, AuthSyncPendingEffect authSyncPending = AuthSyncPendingEffect.Keep) => inner.UpdateAsync(administrator, expectedVersion, ct, authSyncPending);
        public Task<bool> SoftDeleteAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default) => inner.SoftDeleteAsync(id, expectedVersion, actorName, ct);
        public Task<(IReadOnlyList<PlatformAdministrator> Items, long TotalCount)> QueryAsync(PlatformAdministratorQuery query, CancellationToken ct = default) => inner.QueryAsync(query, ct);
        public Task<PlatformAdministratorStatsSnapshot> GetStatsAsync(CancellationToken ct = default) => inner.GetStatsAsync(ct);
        public Task<long> CountActiveSuperAdminsAsync(Guid? excludeId = null, CancellationToken ct = default) => inner.CountActiveSuperAdminsAsync(excludeId, ct);
        public Task<SuperAdminGuardedWriteResult> UpdateGuardedAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default, AuthSyncPendingEffect authSyncPending = AuthSyncPendingEffect.Keep) => inner.UpdateGuardedAsync(administrator, expectedVersion, ct, authSyncPending);
        public Task<SuperAdminGuardedWriteResult> SoftDeleteGuardedAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default) => inner.SoftDeleteGuardedAsync(id, expectedVersion, actorName, ct);
        public Task<bool> RecordLoginAcceptedAsync(Guid id, DateTimeOffset acceptedAtUtc, CancellationToken ct = default) => inner.RecordLoginAcceptedAsync(id, acceptedAtUtc, ct);
    }

    private sealed class CountsMarkWrites(IPlatformAdministratorRepository inner) : Delegating(inner)
    {
        public List<(Guid Id, bool Pending)> Writes { get; } = [];

        public override Task MarkAuthSyncPendingAsync(Guid id, bool pending, CancellationToken ct = default)
        {
            Writes.Add((id, pending));
            return base.MarkAuthSyncPendingAsync(id, pending, ct);
        }
    }

    /// <summary>Another request marks the record the moment this request has loaded it.</summary>
    private sealed class MarksTheRecordAfterItIsLoaded(IPlatformAdministratorRepository inner) : Delegating(inner)
    {
        private int _loads;

        public override async Task<PlatformAdministrator?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var loaded = await base.GetByIdAsync(id, ct);
            if (loaded is { Roles: [AdministratorRole.SupportAdmin] } && Interlocked.Increment(ref _loads) == 1)
            {
                await base.MarkAuthSyncPendingAsync(id, pending: true, ct);
            }

            return loaded;
        }
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
