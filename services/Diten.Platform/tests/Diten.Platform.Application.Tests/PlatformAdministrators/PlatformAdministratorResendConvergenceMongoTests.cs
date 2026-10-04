using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;
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
/// BL-521 FIX5 item 5 — A RESEND WHOSE RECORD WRITE DOES NOT HAPPEN LEAVES AUTH WITH THE RECORD, NOT WITH THE STALE PICTURE THE
/// RESEND LOADED. Resend provisions Auth FIRST (roles, status, a fresh setup link) from the record it loaded and writes the
/// record afterwards; a write that is then refused (another request saved in between), fails or never answers left Auth holding
/// what the resend had loaded — the same drift FIX4 closed for the update, which re-reads the record and converges Auth to it.
/// Resend now does what the update does (the same helper): the record is read again and Auth is synced to what it holds now.
///
/// <para>Deterministic (no timing): the production handler, the production repository over a test-owned replica set, and an
/// in-memory Auth whose provision / sync apply to one account per address. The hook runs another request's whole update
/// (record write AND its sync) just before the resend's own provision reaches Auth, so the resend writes the stale picture
/// last.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class PlatformAdministratorResendConvergenceMongoTests
{
    [Fact]
    public async Task A_resend_whose_write_is_refused_leaves_auth_with_the_roles_the_record_has()
    {
        await using var world = await World.StartAsync();
        var target = await world.SeedAsync([AdministratorRole.SuperAdmin]);
        var auth = world.Auth(beforeProvision: () => world.AnotherRequestTakesSuperAdminAwayAsync(target));

        var response = await world.ResendAsync(target, auth);

        Assert.True(response.StatusCode == 409, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        var record = await world.ReadAsync(target.Id);
        Assert.Equal([AdministratorRole.SupportAdmin], record.Roles);
        // the resend's provision wrote its stale SuperAdmin; the convergence that followed put the record's roles back
        Assert.Equal(["SupportAdmin"], world.Accounts.RolesOf(target.Email));
        var convergence = Assert.Single(auth.Syncs);
        Assert.Equal(["SupportAdmin"], convergence.Roles.ToList());
        Assert.Equal("Active", convergence.Status);
    }

    [Fact]
    public async Task A_resend_whose_write_throws_converges_auth_to_the_record_and_the_error_surfaces()
    {
        await using var world = await World.StartAsync();
        var target = await world.SeedAsync([AdministratorRole.SupportAdmin]);
        var auth = world.Auth();

        await Assert.ThrowsAsync<TimeoutException>(() => world.ResendAsync(target, auth, repository: new ThrowingUpdates(world.Repository)));

        var convergence = Assert.Single(auth.Syncs);
        Assert.Equal((target.Email, "Active"), (convergence.Email, convergence.Status));
        Assert.Equal(["SupportAdmin"], convergence.Roles.ToList());
    }

    [Fact]
    public async Task A_resend_that_is_written_makes_no_further_call_to_auth()
    {
        await using var world = await World.StartAsync();
        var target = await world.SeedAsync([AdministratorRole.SupportAdmin]);
        var auth = world.Auth();

        var response = await world.ResendAsync(target, auth);

        Assert.True(response.StatusCode == 200, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        Assert.Empty(auth.Syncs);
        var provision = Assert.Single(auth.Provisions);
        // FIX5 item 11 — the record's status travels with the provision, and the log can name the administrator
        Assert.Equal("Active", provision.Status);
        Assert.Equal(target.Id, provision.AdministratorId);
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
        public Accounts Accounts { get; } = new();

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

        public AuthClient Auth(Func<Task>? beforeProvision = null) => new(Accounts, beforeProvision);

        public async Task<PlatformAdministrator> SeedAsync(IReadOnlyList<AdministratorRole> roles)
        {
            var email = $"resend.{Guid.NewGuid():N}@bl521.test";
            var administrator = new PlatformAdministrator
            {
                Email = email, NormalizedEmail = email, UserName = email, NormalizedUserName = email, DisplayName = "Resend Probe",
                ActorType = ActorType.PlatformAdmin, AllowedTenantIds = [], Roles = roles.ToList(), Status = AdministratorStatus.Active,
                InvitationStatus = AdministratorInvitationStatus.Invited, Version = 1, CreatedBy = "bl521-fix5"
            };
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).InsertOneAsync(administrator);
            Accounts.Apply(email, roles.Select(r => r.ToString()).ToList(), "Active");
            return administrator;
        }

        public async Task<PlatformAdministrator> ReadAsync(Guid id) =>
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).Find(x => x.Id == id).SingleAsync();

        /// <summary>Another request's update, whole: the record is written (SuperAdmin → SupportAdmin, version + 1) and its
        /// sync has reached Auth.</summary>
        public async Task AnotherRequestTakesSuperAdminAwayAsync(PlatformAdministrator target)
        {
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).UpdateOneAsync(
                x => x.Id == target.Id,
                Builders<PlatformAdministrator>.Update.Set(x => x.Roles, new List<AdministratorRole> { AdministratorRole.SupportAdmin }).Inc(x => x.Version, 1));
            Accounts.Apply(target.Email, ["SupportAdmin"], "Active");
        }

        public async Task<Response<PlatformAdministratorInviteResultDto>> ResendAsync(
            PlatformAdministrator target,
            AuthClient auth,
            IPlatformAdministratorRepository? repository = null)
        {
            var current = await ReadAsync(target.Id);
            return await new ResendPlatformAdministratorInviteHandler(repository ?? Repository, auth, new Actor("resending.admin@bl521.test"), NullLogger<ResendPlatformAdministratorInviteHandler>.Instance)
                .Handle(new ResendPlatformAdministratorInviteCommand(target.Id, new PlatformAdministratorVersionRequest(current.Version)), CancellationToken.None);
        }

        public ValueTask DisposeAsync() => _mongo.DisposeAsync();
    }

    /// <summary>Auth's accounts as the provision and the sync leave them: roles and status per address.</summary>
    private sealed class Accounts
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, (List<string> Roles, string? Status)> _accounts = new(StringComparer.OrdinalIgnoreCase);

        public void Apply(string email, List<string> roles, string? status)
        {
            lock (_gate) _accounts[email] = (roles, status);
        }

        public List<string> RolesOf(string email)
        {
            lock (_gate) return _accounts[email].Roles.ToList();
        }
    }

    private sealed class AuthClient(Accounts accounts, Func<Task>? beforeProvision) : IPlatformAdministratorProvisioningService
    {
        public List<PlatformAdministratorProvisioningRequest> Provisions { get; } = [];
        public List<PlatformAdministratorProvisioningSyncRequest> Syncs { get; } = [];

        public async Task<PlatformAdministratorProvisioningResult> ProvisionAsync(PlatformAdministratorProvisioningRequest request, CancellationToken ct)
        {
            Provisions.Add(request);
            if (beforeProvision is not null) await beforeProvision();
            accounts.Apply(request.Email, request.Roles.ToList(), request.Status);
            return new PlatformAdministratorProvisioningResult(null, true);
        }

        public Task SyncAsync(PlatformAdministratorProvisioningSyncRequest request, CancellationToken ct)
        {
            Syncs.Add(request);
            accounts.Apply(request.Email, request.Roles.ToList(), request.Status);
            return Task.CompletedTask;
        }
    }

    /// <summary>The production repository, except that the ordinary write times out (after Auth was provisioned).</summary>
    private sealed class ThrowingUpdates(IPlatformAdministratorRepository inner) : IPlatformAdministratorRepository
    {
        public Task<bool> UpdateAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default, AuthSyncPendingEffect authSyncPending = AuthSyncPendingEffect.Keep) =>
            throw new TimeoutException("the database did not answer the write");

        public Task<PlatformAdministrator> CreateAsync(PlatformAdministrator administrator, CancellationToken ct = default) => inner.CreateAsync(administrator, ct);
        public Task<PlatformAdministrator?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<PlatformAdministrator?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default) => inner.GetByNormalizedEmailAsync(normalizedEmail, ct);
        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByEmailAsync(normalizedEmail, excludeId, ct);
        public Task<bool> ExistsByUserNameAsync(string normalizedUserName, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByUserNameAsync(normalizedUserName, excludeId, ct);
        public Task<bool> SoftDeleteAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default) => inner.SoftDeleteAsync(id, expectedVersion, actorName, ct);
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
