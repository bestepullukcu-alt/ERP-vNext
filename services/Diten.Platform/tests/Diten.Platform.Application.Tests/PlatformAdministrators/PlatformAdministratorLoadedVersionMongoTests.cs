using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAccount;
using Diten.Platform.Application.Features.PlatformAccount.Commands;
using Diten.Platform.Application.Features.PlatformAccount.Handlers.CommandHandlers;
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
/// BL-521 FIX4 item 3 — A DECISION MADE ON THE LOADED RECORD IS WRITTEN AT THE LOADED VERSION. Suspend, Delete, BulkDelete,
/// AssignRoles, Reactivate, ResendInvite and the account settings decided from the record they loaded (guarded or not, seed
/// or not, a super administrator or not) and then wrote with the version the CLIENT sent. Versions are consecutive
/// integers, so a client could name the NEXT one: if the record changed between this request's read and its write (here:
/// someone saves it right after the read), the stale snapshot was written over the newer record — an unguarded removal of
/// a super administrator, or an administrator undoing its own suspension through its account settings. Now a client
/// version that is not the loaded one is refused 409 before anything is decided, and the write is conditional on the
/// version that was read.
///
/// <para>Two cases per handler: (stale) the record is saved by someone else right after this request read it and the
/// client names the version that save produced — before, the stale snapshot was written; (wrong) the client names a
/// version other than the record's while nothing else happens — refused, nothing written (so the refusal does not depend
/// on the write's own condition).</para>
///
/// <para>Production handlers and the production repository over a test-owned replica set; the only addition is a wrapper
/// that runs another user's save right after the handler's read — never the shared database.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class PlatformAdministratorLoadedVersionMongoTests
{
    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var handler in new[] { "suspend", "delete", "bulkDelete", "assignRoles", "reactivate", "resendInvite", "accountSettings" })
        {
            data.Add(handler, "stale");
            data.Add(handler, "wrong");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_client_version_other_than_the_loaded_one_is_refused_409_and_nothing_is_written(string handler, string scenario)
    {
        await using var world = await World.StartAsync();
        var actor = await world.SeedAsync("actor", [AdministratorRole.SuperAdmin], AdministratorStatus.Active);
        await world.SeedAsync("second", [AdministratorRole.SuperAdmin], AdministratorStatus.Active); // nobody is the last one
        var target = await world.SeedAsync(
            "target",
            [AdministratorRole.SupportAdmin],
            handler == "reactivate" ? AdministratorStatus.Suspended : AdministratorStatus.Active);
        var loaded = await world.ReadAsync(target.Id);

        int clientVersion;
        if (scenario == "stale")
        {
            // Right after the handler's read, someone else saves the record (a new name, a new version); the client names
            // that new version. The snapshot the handler holds is now stale.
            world.Repository.AfterFirstLoadOf(target.Id, () => world.ConcurrentSaveAsync(target.Id));
            clientVersion = loaded.Version + 1;
        }
        else
        {
            clientVersion = loaded.Version + 1; // a version the record does not have; nothing else happens
        }

        var actingAs = handler == "accountSettings" ? target : actor;
        var response = await world.RunAsync(handler, target, actingAs, clientVersion);

        Assert.True(response.StatusCode == 409, $"{handler}/{scenario}: {response.StatusCode} {string.Join("; ", response.Errors)}");
        var after = await world.ReadAsync(target.Id);
        Assert.False(after.IsDeleted);
        Assert.Equal(loaded.Status, after.Status);
        Assert.Equal(loaded.Roles, after.Roles);
        if (scenario == "stale")
        {
            Assert.Equal((World.ConcurrentName, loaded.Version + 1), (after.DisplayName, after.Version)); // the newer save survives
        }
        else
        {
            Assert.Equal((loaded.DisplayName, loaded.Version), (after.DisplayName, after.Version));
        }

        Assert.Empty(world.Provisioning.Provisioned); // resend-invite: refused before anything reaches Auth
    }

    /// <summary>The update path refused a different version too, but only AFTER deciding the self rule, the seed rule and
    /// the last super administrator rule on the loaded record; now the version comes first.</summary>
    [Fact]
    public async Task The_update_path_refuses_a_different_version_before_any_other_rule_decides()
    {
        await using var world = await World.StartAsync();
        var self = await world.SeedAsync("self", [AdministratorRole.ReadOnly], AdministratorStatus.Active);
        await world.SeedAsync("second", [AdministratorRole.SuperAdmin], AdministratorStatus.Active);
        var loaded = await world.ReadAsync(self.Id);
        var user = new Actor(self.Email);

        var response = await new UpdatePlatformAdministratorHandler(world.Repository, world.Provisioning, user,
                new ActorSafetyGuard(user, world.Repository), NullLogger<UpdatePlatformAdministratorHandler>.Instance)
            .Handle(new UpdatePlatformAdministratorCommand(self.Id, new UpdatePlatformAdministratorRequest(
                loaded.Email, loaded.UserName, loaded.DisplayName, loaded.ActorType.ToString(), loaded.PartnerId, loaded.AllowedTenantIds,
                loaded.Status.ToString(), ["SuperAdmin"], loaded.Version + 1)), CancellationToken.None);

        Assert.True(response.StatusCode == 409, $"{response.StatusCode} {response.ReasonCode}");
        Assert.Null(response.ReasonCode); // not the self rule's 403
        Assert.Equal(AdminSafetyMessages.ChangedByAnotherUser, Assert.Single(response.Errors));
        Assert.Empty(world.Provisioning.Synced);
    }

    // ── world ───────────────────────────────────────────────────────────────────────────────────────────

    private sealed class World : IAsyncDisposable
    {
        public const string ConcurrentName = "Saved By Someone Else";

        private readonly DisposableMongoReplicaSet _mongo;
        private readonly IMongoDatabase _database;

        private World(DisposableMongoReplicaSet mongo, IMongoDatabase database)
        {
            _mongo = mongo;
            _database = database;
            Repository = new AfterLoad(new PlatformAdministratorRepository(new PlatformDbContext(mongo.Client, database), new TenantContext()));
        }

        public AfterLoad Repository { get; }

        public RecordingProvisioning Provisioning { get; } = new();

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

        public async Task<PlatformAdministrator> SeedAsync(string name, IReadOnlyList<AdministratorRole> roles, AdministratorStatus status)
        {
            var email = $"version.{name}.{Guid.NewGuid():N}@bl521.test";
            var administrator = new PlatformAdministrator
            {
                Email = email, NormalizedEmail = email, UserName = email, NormalizedUserName = email, DisplayName = "Version Probe",
                ActorType = ActorType.PlatformAdmin, AllowedTenantIds = [], Roles = roles.ToList(), Status = status,
                InvitationStatus = AdministratorInvitationStatus.Accepted, Version = 1, CreatedBy = "bl521-fix4"
            };
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).InsertOneAsync(administrator);
            return administrator;
        }

        public async Task<PlatformAdministrator> ReadAsync(Guid id) =>
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).Find(x => x.Id == id).SingleAsync();

        /// <summary>Another user's save: a new name and the next version.</summary>
        public Task ConcurrentSaveAsync(Guid id) =>
            _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).UpdateOneAsync(
                x => x.Id == id,
                Builders<PlatformAdministrator>.Update.Set(x => x.DisplayName, ConcurrentName).Inc(x => x.Version, 1));

        public async Task<Response<NoContent>> RunAsync(string handler, PlatformAdministrator target, PlatformAdministrator actingAs, int version)
        {
            var user = new Actor(actingAs.Email);
            var guard = new ActorSafetyGuard(user, Repository);
            switch (handler)
            {
                case "suspend":
                    return await new SuspendPlatformAdministratorHandler(Repository, user, guard)
                        .Handle(new SuspendPlatformAdministratorCommand(target.Id, new PlatformAdministratorStatusRequest("bl521-fix4", version)), CancellationToken.None);
                case "delete":
                    return await new DeletePlatformAdministratorHandler(Repository, user, guard)
                        .Handle(new DeletePlatformAdministratorCommand(target.Id, new PlatformAdministratorVersionRequest(version)), CancellationToken.None);
                case "bulkDelete":
                    return await new BulkDeletePlatformAdministratorsHandler(Repository, user, guard)
                        .Handle(new BulkDeletePlatformAdministratorsCommand([new PlatformAdministratorBulkDeleteItemRequest(target.Id, version)]), CancellationToken.None);
                case "assignRoles":
                    return await new AssignPlatformAdministratorRolesHandler(Repository, user, guard)
                        .Handle(new AssignPlatformAdministratorRolesCommand(target.Id, new AssignPlatformAdministratorRolesRequest(["SupportAdmin", "BillingAdmin"], version)), CancellationToken.None);
                case "reactivate":
                    return await new ReactivatePlatformAdministratorHandler(Repository, user)
                        .Handle(new ReactivatePlatformAdministratorCommand(target.Id, new PlatformAdministratorStatusRequest("bl521-fix4", version)), CancellationToken.None);
                case "resendInvite":
                    var resent = await new ResendPlatformAdministratorInviteHandler(Repository, Provisioning, user, NullLogger<ResendPlatformAdministratorInviteHandler>.Instance)
                        .Handle(new ResendPlatformAdministratorInviteCommand(target.Id, new PlatformAdministratorVersionRequest(version)), CancellationToken.None);
                    return resent.IsSuccessful
                        ? Response<NoContent>.Success(resent.StatusCode)
                        : Response<NoContent>.Fail(resent.Errors, resent.StatusCode, resent.ReasonCode);
                case "accountSettings":
                    return await new UpdatePlatformAccountSettingsHandler(Repository, user)
                        .Handle(new UpdatePlatformAccountSettingsCommand(new UpdatePlatformAccountSettingsRequest("Renamed By Myself", version)), CancellationToken.None);
                default:
                    throw new ArgumentOutOfRangeException(nameof(handler), handler, null);
            }
        }

        public ValueTask DisposeAsync() => _mongo.DisposeAsync();
    }

    /// <summary>The production repository; after the FIRST read of one record (by id or by address) it lets another user's
    /// save run before handing the — now stale — record to the handler.</summary>
    private sealed class AfterLoad(IPlatformAdministratorRepository inner) : IPlatformAdministratorRepository
    {
        private Guid _target;
        private Func<Task>? _hook;

        public void AfterFirstLoadOf(Guid id, Func<Task> hook)
        {
            _target = id;
            _hook = hook;
        }

        private async Task<PlatformAdministrator?> Loaded(PlatformAdministrator? administrator)
        {
            if (administrator is not null && administrator.Id == _target && Interlocked.Exchange(ref _hook, null) is { } hook)
            {
                await hook();
            }

            return administrator;
        }

        public async Task<PlatformAdministrator?> GetByIdAsync(Guid id, CancellationToken ct = default) => await Loaded(await inner.GetByIdAsync(id, ct));
        public async Task<PlatformAdministrator?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default) => await Loaded(await inner.GetByNormalizedEmailAsync(normalizedEmail, ct));

        public Task<PlatformAdministrator> CreateAsync(PlatformAdministrator administrator, CancellationToken ct = default) => inner.CreateAsync(administrator, ct);
        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByEmailAsync(normalizedEmail, excludeId, ct);
        public Task<bool> ExistsByUserNameAsync(string normalizedUserName, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByUserNameAsync(normalizedUserName, excludeId, ct);
        public Task<bool> UpdateAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default, AuthSyncPendingEffect authSyncPending = AuthSyncPendingEffect.Keep) => inner.UpdateAsync(administrator, expectedVersion, ct, authSyncPending);
        public Task<bool> SoftDeleteAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default) => inner.SoftDeleteAsync(id, expectedVersion, actorName, ct);
        public Task<(IReadOnlyList<PlatformAdministrator> Items, long TotalCount)> QueryAsync(PlatformAdministratorQuery query, CancellationToken ct = default) => inner.QueryAsync(query, ct);
        public Task<PlatformAdministratorStatsSnapshot> GetStatsAsync(CancellationToken ct = default) => inner.GetStatsAsync(ct);
        public Task<long> CountActiveSuperAdminsAsync(Guid? excludeId = null, CancellationToken ct = default) => inner.CountActiveSuperAdminsAsync(excludeId, ct);
        public Task<SuperAdminGuardedWriteResult> UpdateGuardedAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default, AuthSyncPendingEffect authSyncPending = AuthSyncPendingEffect.Keep) => inner.UpdateGuardedAsync(administrator, expectedVersion, ct, authSyncPending);
        public Task<SuperAdminGuardedWriteResult> SoftDeleteGuardedAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default) => inner.SoftDeleteGuardedAsync(id, expectedVersion, actorName, ct);
        public Task MarkAuthSyncPendingAsync(Guid id, bool pending, CancellationToken ct = default) => inner.MarkAuthSyncPendingAsync(id, pending, ct);
        public Task<bool> RecordLoginAcceptedAsync(Guid id, DateTimeOffset acceptedAtUtc, CancellationToken ct = default) => inner.RecordLoginAcceptedAsync(id, acceptedAtUtc, ct);
    }

    private sealed class RecordingProvisioning : IPlatformAdministratorProvisioningService
    {
        public List<PlatformAdministratorProvisioningRequest> Provisioned { get; } = [];
        public List<PlatformAdministratorProvisioningSyncRequest> Synced { get; } = [];

        public Task<PlatformAdministratorProvisioningResult> ProvisionAsync(PlatformAdministratorProvisioningRequest request, CancellationToken ct)
        {
            Provisioned.Add(request);
            return Task.FromResult(new PlatformAdministratorProvisioningResult(null, true));
        }

        public Task SyncAsync(PlatformAdministratorProvisioningSyncRequest request, CancellationToken ct)
        {
            Synced.Add(request);
            return Task.CompletedTask;
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
