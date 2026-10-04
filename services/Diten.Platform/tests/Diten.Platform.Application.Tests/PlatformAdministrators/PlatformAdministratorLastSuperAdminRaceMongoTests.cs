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
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAdministrators;

/// <summary>
/// BL-521 FIX3 item 3 — THE LAST PLATFORM SUPER ADMINISTRATOR UNDER A RACE. A and B are the only active platform
/// administrators holding SuperAdmin; A takes B out of the set while B takes A out of it, at the same moment. Before: the
/// count ("is another one left?") and the write were separate, with no transaction and only per-document version checks,
/// so both counts saw the other still in place, both writes went through and NO super administrator was left.
///
/// <para>Deterministic, not a timing lottery: every write under the guard runs through the production
/// <see cref="PlatformAdministratorRepository"/>, whose test seam holds a write INSIDE its transaction — after its count and
/// its own write, before its commit — until the other request has either reached the same point or finished. Without the
/// guard both reach it (both counted the other as present) and both commit; with it the second one is refused 409 before it
/// can count. Production handlers, production safety guard, a test-owned replica set — never the shared database.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class PlatformAdministratorLastSuperAdminRaceMongoTests
{
    public static TheoryData<string, string> Races() => new()
    {
        { "demote", "demote" },          // A demotes B to partner, B demotes A to partner (the reported case)
        { "suspend", "dropSuperAdmin" }, // A suspends B, B strips A's SuperAdmin through role assignment
        { "delete", "disable" },         // A deletes B, B disables A through the update path
    };

    [Theory]
    [MemberData(nameof(Races))]
    public async Task Two_concurrent_changes_that_each_remove_the_other_super_admin_leave_one_and_refuse_the_other_409(string aDoes, string bDoes)
    {
        await using var world = await World.StartAsync();
        var a = await world.SeedAsync(AdministratorRole.SuperAdmin);
        var b = await world.SeedAsync(AdministratorRole.SuperAdmin);
        await world.WarmUpGuardAsync(actingAs: a); // a running system: the guard document already exists
        Assert.Equal(2, await world.CountActivePlatformSuperAdminsAsync());

        var gate = new BothOrDone();
        var first = gate.Track(() => world.RunAsync(gate, actingAs: a, target: b, aDoes));
        var second = gate.Track(() => world.RunAsync(gate, actingAs: b, target: a, bDoes));
        var results = await Task.WhenAll(first, second);

        Assert.True(results.Count(r => r.StatusCode == 204) == 1, Describe(results));
        var refused = Assert.Single(results, r => r.StatusCode != 204);
        Assert.True(refused.StatusCode == 409, Describe(results));
        Assert.Contains(refused.ReasonCode, new[] { AdminSafetyReasonCodes.ConcurrentSuperAdminChange, AdminSafetyReasonCodes.LastActiveSuperAdmin });
        Assert.Equal(1, await world.CountActivePlatformSuperAdminsAsync());
    }

    /// <summary>A database that cannot run the transaction is no answer: 503, nothing written — never a write without
    /// the guard.</summary>
    [Fact]
    public async Task Without_transaction_support_a_super_admin_demotion_is_refused_503_and_nothing_is_written()
    {
        await using var mongo = await DisposableStandaloneMongo.StartAsync();
        var database = mongo.CreateDatabase();
        var repository = new PlatformAdministratorRepository(new PlatformDbContext(mongo.Client, database), new TenantContext());
        var actor = await World.InsertAsync(database, AdministratorRole.SuperAdmin);
        var target = await World.InsertAsync(database, AdministratorRole.SuperAdmin);

        var response = await World.UpdateAsync(repository, database, actor, target, r => r with { ActorType = "PartnerAdmin", PartnerId = Guid.NewGuid(), AllowedTenantIds = [Guid.NewGuid()] });

        Assert.True(response.StatusCode == 503, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        Assert.Equal(AdminSafetyReasonCodes.SafetyCheckUnavailable, response.ReasonCode);
        var after = await World.ReadAsync(database, target.Id);
        Assert.Equal((ActorType.PlatformAdmin, target.Version), (after.ActorType, after.Version));
    }

    private static string Describe(IEnumerable<Response<NoContent>> results) =>
        string.Join(" | ", results.Select(r => $"{r.StatusCode} {r.ReasonCode} {string.Join("; ", r.Errors)}"));

    /// <summary>
    /// The gate both requests' repositories share. A request that reaches the point just before its commit waits there
    /// until the other one reaches it too or finishes — so a request that holds the guard cannot commit before the other
    /// has tried its own claim, and two requests the guard did NOT stop always commit together (the bug, reproduced).
    /// </summary>
    private sealed class BothOrDone
    {
        private int _arrived;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ArriveAsync(CancellationToken ct)
        {
            if (Interlocked.Increment(ref _arrived) >= 2)
            {
                _release.TrySetResult();
            }

            await _release.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }

        public Task<Response<NoContent>> Track(Func<Task<Response<NoContent>>> run) => Task.Run(async () =>
        {
            try
            {
                return await run();
            }
            finally
            {
                _release.TrySetResult(); // this request is done: the other may commit
            }
        });
    }

    private sealed class World : IAsyncDisposable
    {
        private readonly DisposableMongoReplicaSet _mongo;
        private readonly IMongoDatabase _database;
        private readonly PlatformDbContext _dbContext;

        private World(DisposableMongoReplicaSet mongo, IMongoDatabase database)
        {
            _mongo = mongo;
            _database = database;
            _dbContext = new PlatformDbContext(mongo.Client, database);
        }

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

        public Task<PlatformAdministrator> SeedAsync(AdministratorRole role) => InsertAsync(_database, role);

        public static async Task<PlatformAdministrator> InsertAsync(IMongoDatabase database, AdministratorRole role)
        {
            var email = $"race.{Guid.NewGuid():N}@bl521.test";
            var administrator = new PlatformAdministrator
            {
                Email = email, NormalizedEmail = email, UserName = email, NormalizedUserName = email, DisplayName = "Race Probe",
                ActorType = ActorType.PlatformAdmin, AllowedTenantIds = [], Roles = [role], Status = AdministratorStatus.Active,
                InvitationStatus = AdministratorInvitationStatus.Accepted, Version = 1, CreatedBy = "bl521-fix3"
            };
            await database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).InsertOneAsync(administrator);
            return administrator;
        }

        public static async Task<PlatformAdministrator> ReadAsync(IMongoDatabase database, Guid id) =>
            await database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).Find(x => x.Id == id).SingleAsync();

        /// <summary>One ordinary guarded write before the race (a third super administrator is demoted), so the guard
        /// document exists as it does in any running system.</summary>
        public async Task WarmUpGuardAsync(PlatformAdministrator actingAs)
        {
            var third = await SeedAsync(AdministratorRole.SuperAdmin);
            var repository = new PlatformAdministratorRepository(_dbContext, new TenantContext());
            var response = await UpdateAsync(repository, _database, actingAs, third, r => r with { Roles = ["SupportAdmin"] });
            Assert.True(response.StatusCode == 204, $"warm-up: {response.StatusCode} {string.Join("; ", response.Errors)}");
        }

        public async Task<long> CountActivePlatformSuperAdminsAsync() =>
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).CountDocumentsAsync(x =>
                !x.IsDeleted && x.Status == AdministratorStatus.Active && x.ActorType == ActorType.PlatformAdmin
                && x.Roles.Contains(AdministratorRole.SuperAdmin));

        /// <summary>One request: its own repository (as each HTTP request has its own), sharing the gate.</summary>
        public async Task<Response<NoContent>> RunAsync(BothOrDone gate, PlatformAdministrator actingAs, PlatformAdministrator target, string change)
        {
            var repository = new PlatformAdministratorRepository(_dbContext, new TenantContext(), gate.ArriveAsync);
            var user = new Actor(actingAs.Email);
            var guard = new ActorSafetyGuard(user, repository);
            var version = (await ReadAsync(_database, target.Id)).Version;

            return change switch
            {
                "demote" => await UpdateAsync(repository, _database, actingAs, target,
                    r => r with { ActorType = "PartnerAdmin", PartnerId = Guid.NewGuid(), AllowedTenantIds = [Guid.NewGuid()] }),
                "disable" => await UpdateAsync(repository, _database, actingAs, target, r => r with { Status = "Disabled" }),
                "suspend" => await new SuspendPlatformAdministratorHandler(repository, user, guard)
                    .Handle(new SuspendPlatformAdministratorCommand(target.Id, new PlatformAdministratorStatusRequest("race", version)), CancellationToken.None),
                "delete" => await new DeletePlatformAdministratorHandler(repository, user, guard)
                    .Handle(new DeletePlatformAdministratorCommand(target.Id, new PlatformAdministratorVersionRequest(version)), CancellationToken.None),
                "dropSuperAdmin" => await new AssignPlatformAdministratorRolesHandler(repository, user, guard)
                    .Handle(new AssignPlatformAdministratorRolesCommand(target.Id, new AssignPlatformAdministratorRolesRequest(["SupportAdmin"], version)), CancellationToken.None),
                _ => throw new ArgumentOutOfRangeException(nameof(change), change, null)
            };
        }

        public static async Task<Response<NoContent>> UpdateAsync(
            PlatformAdministratorRepository repository,
            IMongoDatabase database,
            PlatformAdministrator actingAs,
            PlatformAdministrator target,
            Func<UpdatePlatformAdministratorRequest, UpdatePlatformAdministratorRequest> change)
        {
            var current = await ReadAsync(database, target.Id);
            var request = change(new UpdatePlatformAdministratorRequest(
                current.Email, current.UserName, current.DisplayName, current.ActorType.ToString(), current.PartnerId,
                current.AllowedTenantIds, current.Status.ToString(), current.Roles.Select(r => r.ToString()).ToList(), current.Version));
            var user = new Actor(actingAs.Email);
            return await new UpdatePlatformAdministratorHandler(repository, new NoProvisioning(), user, new ActorSafetyGuard(user, repository),
                    NullLogger<UpdatePlatformAdministratorHandler>.Instance)
                .Handle(new UpdatePlatformAdministratorCommand(target.Id, request), CancellationToken.None);
        }

        public ValueTask DisposeAsync() => _mongo.DisposeAsync();
    }

    private sealed class Actor(string email) : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string? Email => email;
        public string? DisplayName => "Acting Administrator";
        public string ActorName => email;
        public bool IsAuthenticated => true;
    }

    private sealed class NoProvisioning : IPlatformAdministratorProvisioningService
    {
        public Task<PlatformAdministratorProvisioningResult> ProvisionAsync(PlatformAdministratorProvisioningRequest request, CancellationToken ct) =>
            Task.FromResult(new PlatformAdministratorProvisioningResult(null, true));

        public Task SyncAsync(PlatformAdministratorProvisioningSyncRequest request, CancellationToken ct) => Task.CompletedTask;
    }
}
