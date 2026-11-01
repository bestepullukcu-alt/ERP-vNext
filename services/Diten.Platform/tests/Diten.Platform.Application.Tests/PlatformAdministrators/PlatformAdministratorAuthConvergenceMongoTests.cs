using System.Net;
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
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAdministrators;

/// <summary>
/// BL-521 FIX4 item 2 — WHEN THE RECORD WRITE DOES NOT HAPPEN, AUTH FOLLOWS THE RECORD AS IT IS NOW. FIX3 synced Auth first
/// and, when the record write was then refused, put back the values the record had when THIS request loaded it — values
/// another request may already have replaced. The reported regression: R1 (a name edit) and R2 (taking SuperAdmin away)
/// load the same version; R2 syncs Auth, R1 syncs Auth (SuperAdmin again), R2 writes, R1's write is refused and R1 restores
/// ITS picture: the record says SupportAdmin, every new token SuperAdmin. Now a refused, failed or cancelled write reads
/// the record again and Auth is converged to that; if Auth cannot be reached either, the record is marked pending and an
/// error is logged with identifiers only.
///
/// <para>Item 5 — the record's status travels with every sync, so Auth can follow it.</para>
///
/// <para>Deterministic (barriers, no timing): the production handler, the production repository over a test-owned replica
/// set, and an in-memory Auth that holds, per address, what the last sync applied — never the shared database.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class PlatformAdministratorAuthConvergenceMongoTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    /// <summary>The reported race, step by step: R2 syncs → R1 syncs → R2 writes → R1's write is refused.</summary>
    [Fact]
    public async Task A_name_edit_racing_a_super_admin_removal_leaves_auth_with_the_roles_the_record_has()
    {
        await using var world = await World.StartAsync();
        var target = await world.SeedAsync([AdministratorRole.SuperAdmin]); // edits its own name (allowed) in R1
        var remover = await world.SeedAsync([AdministratorRole.SuperAdmin]); // takes the target's SuperAdmin in R2 (not the last)

        var r2Synced = Signal();
        var r1Synced = Signal();
        var r2Done = Signal();
        var r2Auth = world.Client(after: async call => { if (call == 0) { r2Synced.TrySetResult(); await r1Synced.Task.WaitAsync(Wait); } });
        var r1Auth = world.Client(after: async call => { if (call == 0) { r1Synced.TrySetResult(); await r2Done.Task.WaitAsync(Wait); } });

        var r2 = Task.Run(async () =>
        {
            try
            {
                return await world.UpdateAsync(target, actingAs: remover, r2Auth, r => r with { Roles = ["SupportAdmin"] });
            }
            finally
            {
                r2Done.TrySetResult();
            }
        });
        await r2Synced.Task.WaitAsync(Wait);
        var r1 = Task.Run(() => world.UpdateAsync(target, actingAs: target, r1Auth, r => r with { DisplayName = "Renamed While Racing" }));
        var results = await Task.WhenAll(r2, r1);

        Assert.True(results[0].StatusCode == 204, $"R2: {results[0].StatusCode} {string.Join("; ", results[0].Errors)}");
        Assert.True(results[1].StatusCode == 409, $"R1: {results[1].StatusCode} {string.Join("; ", results[1].Errors)}");
        var record = await world.ReadAsync(target.Id);
        Assert.Equal([AdministratorRole.SupportAdmin], record.Roles);
        Assert.Equal(["SupportAdmin"], world.Auth.RolesOf(target.Email));
        // R1's convergence carried what the record holds NOW (R2's roles, R1's name refused), never R1's loaded picture
        var convergence = r1Auth.Calls[^1];
        Assert.Equal(["SupportAdmin"], convergence.Roles.ToList());
        Assert.Equal((record.DisplayName, "Active"), (convergence.DisplayName, convergence.Status));
    }

    /// <summary>The write itself throws (here: the database times out) after Auth took the change: Auth is converged to the
    /// record — which did not change — and the error still reaches the caller.</summary>
    [Fact]
    public async Task When_the_record_write_throws_auth_is_converged_to_the_record_and_the_error_surfaces()
    {
        await using var world = await World.StartAsync();
        var actor = await world.SeedAsync([AdministratorRole.SuperAdmin]);
        var target = await world.SeedAsync([AdministratorRole.SupportAdmin]);
        var auth = world.Client();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            world.UpdateAsync(target, actingAs: actor, auth, r => r with { Roles = ["BillingAdmin"] }, repository: new ThrowingWrites(world.Repository)));

        Assert.Equal([AdministratorRole.SupportAdmin], (await world.ReadAsync(target.Id)).Roles);
        Assert.Equal(2, auth.Calls.Count);
        Assert.Equal(["BillingAdmin"], auth.Calls[0].Roles.ToList());
        Assert.Equal(["SupportAdmin"], world.Auth.RolesOf(target.Email));
    }

    /// <summary>Auth refuses the sync itself: nothing is written, and Auth is converged to the record as it is.</summary>
    [Fact]
    public async Task When_auth_refuses_the_sync_the_record_is_not_written_and_auth_is_converged_to_it()
    {
        await using var world = await World.StartAsync();
        var actor = await world.SeedAsync([AdministratorRole.SuperAdmin]);
        var target = await world.SeedAsync([AdministratorRole.SupportAdmin]);
        var before = await world.ReadAsync(target.Id);
        var auth = world.Client(before: call => call == 0 ? throw new InvalidOperationException("refused by auth") : Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() => world.UpdateAsync(target, actingAs: actor, auth, r => r with { Roles = ["BillingAdmin"] }));

        var after = await world.ReadAsync(target.Id);
        Assert.Equal((before.Roles.Single(), before.Version), (after.Roles.Single(), after.Version));
        Assert.Equal((target.Email, "SupportAdmin", "Active"), (auth.Calls[^1].Email, auth.Calls[^1].Roles.Single(), auth.Calls[^1].Status));
    }

    /// <summary>Auth cannot be reached for the convergence either: an error is logged (identifiers only — never the
    /// address), the record is marked pending, and the next successful update of the record clears the mark.</summary>
    [Fact]
    public async Task When_auth_cannot_be_converged_the_record_is_marked_pending_and_the_next_successful_update_clears_it()
    {
        await using var world = await World.StartAsync();
        var actor = await world.SeedAsync([AdministratorRole.SuperAdmin]);
        var target = await world.SeedAsync([AdministratorRole.SupportAdmin]);
        var log = new CapturingLogger();
        var auth = world.Client(before: async call =>
        {
            if (call == 0)
            {
                await world.BumpVersionAsync(target.Id); // someone else saves the record while Auth is being synced
                return;
            }

            throw new InvalidOperationException("auth unreachable");
        });

        var refused = await world.UpdateAsync(target, actingAs: actor, auth, r => r with { Roles = ["BillingAdmin"] }, logger: log);

        Assert.Equal(409, refused.StatusCode);
        Assert.NotNull((await world.ReadAsync(target.Id)).AuthSyncPendingSinceUtc);
        var error = Assert.Single(log.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains(target.Id.ToString(), error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(target.Email, error.Message, StringComparison.OrdinalIgnoreCase);

        var next = await world.UpdateAsync(target, actingAs: actor, world.Client(), r => r with { DisplayName = "Converged Again" });

        Assert.True(next.StatusCode == 204, $"{next.StatusCode}: {string.Join("; ", next.Errors)}");
        Assert.Null((await world.ReadAsync(target.Id)).AuthSyncPendingSinceUtc);
    }

    /// <summary>Item 5 — every sync names the status it is for: the requested one on the way in, the record's own when
    /// converging.</summary>
    [Fact]
    public async Task Every_sync_names_the_status_auth_must_follow()
    {
        await using var world = await World.StartAsync();
        var actor = await world.SeedAsync([AdministratorRole.SuperAdmin]);
        var target = await world.SeedAsync([AdministratorRole.SupportAdmin]);
        var auth = world.Client();

        var response = await world.UpdateAsync(target, actingAs: actor, auth, r => r with { Status = "Suspended" });

        Assert.True(response.StatusCode == 204, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        Assert.Equal("Suspended", Assert.Single(auth.Calls).Status);
        Assert.Equal("Suspended", world.Auth.StatusOf(target.Email));
    }

    // ── BL-521 FIX5 item 10 — no second call to an Auth that did not answer ─────────────────────────────

    public static TheoryData<string> NoAnswers() => new() { "5xx", "connection", "timeout" };

    private static Exception NoAnswer(string kind) => kind switch
    {
        "5xx" => new PlatformAdministratorAuthCallException(HttpStatusCode.ServiceUnavailable),
        "connection" => new HttpRequestException("connection refused"),
        _ => new TaskCanceledException("the client's timeout", new TimeoutException())
    };

    /// <summary>
    /// Auth gives no answer to the FIRST sync (a 5xx, a refused connection, the client's timeout — which may have applied it in
    /// part). The convergence call that followed went to the same Auth: another full timeout for the caller to wait through, with
    /// nothing more to say to it. Now the record is marked pending at once, the one call is the only call, and the original
    /// failure is what surfaces. The record itself was never written.
    /// </summary>
    [Theory]
    [MemberData(nameof(NoAnswers))]
    public async Task When_auth_does_not_answer_the_first_sync_there_is_no_second_call_and_the_record_is_marked_pending(string kind)
    {
        await using var world = await World.StartAsync();
        var actor = await world.SeedAsync([AdministratorRole.SuperAdmin]);
        var target = await world.SeedAsync([AdministratorRole.SupportAdmin]);
        var log = new CapturingLogger();
        var auth = world.Client(before: _ => throw NoAnswer(kind));

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() =>
            world.UpdateAsync(target, actingAs: actor, auth, r => r with { Roles = ["BillingAdmin"] }, logger: log));

        Assert.Equal(NoAnswer(kind).GetType(), thrown.GetType());
        Assert.Single(auth.Calls); // never a second call to the Auth that did not answer
        var stored = await world.ReadAsync(target.Id);
        Assert.NotNull(stored.AuthSyncPendingSinceUtc);
        Assert.Equal([AdministratorRole.SupportAdmin], stored.Roles); // the record was not written
        Assert.Equal(target.Version, stored.Version);
        var error = Assert.Single(log.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains(target.Id.ToString(), error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(target.Email, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The contrast: Auth ANSWERED and refused (a 409). Whatever it applied before refusing is corrected: the record
    /// is read again and Auth is converged to it — a second call, as before.</summary>
    [Fact]
    public async Task When_auth_answers_and_refuses_the_first_sync_it_is_still_converged_to_the_record()
    {
        await using var world = await World.StartAsync();
        var actor = await world.SeedAsync([AdministratorRole.SuperAdmin]);
        var target = await world.SeedAsync([AdministratorRole.SupportAdmin]);
        var auth = world.Client(before: call => call == 0 ? throw new PlatformAdministratorAuthCallException(HttpStatusCode.Conflict) : Task.CompletedTask);

        await Assert.ThrowsAsync<PlatformAdministratorAuthCallException>(() =>
            world.UpdateAsync(target, actingAs: actor, auth, r => r with { Roles = ["BillingAdmin"] }));

        Assert.Equal(2, auth.Calls.Count);
        Assert.Equal(["SupportAdmin"], auth.Calls[1].Roles.ToList());
        Assert.Null((await world.ReadAsync(target.Id)).AuthSyncPendingSinceUtc);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        /// <summary>Auth, shared by every request of a test.</summary>
        public FakeAuth Auth { get; } = new();

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

        /// <summary>One request's way to Auth: <paramref name="before"/> / <paramref name="after"/> run around each of its
        /// syncs (0 = its first), so a test can hold a request exactly between two steps.</summary>
        public AuthClient Client(Func<int, Task>? before = null, Func<int, Task>? after = null) => new(Auth, before, after);

        public async Task<PlatformAdministrator> SeedAsync(IReadOnlyList<AdministratorRole> roles)
        {
            var email = $"converge.{Guid.NewGuid():N}@bl521.test";
            var administrator = new PlatformAdministrator
            {
                Email = email, NormalizedEmail = email, UserName = email, NormalizedUserName = email, DisplayName = "Convergence Probe",
                ActorType = ActorType.PlatformAdmin, AllowedTenantIds = [], Roles = roles.ToList(), Status = AdministratorStatus.Active,
                InvitationStatus = AdministratorInvitationStatus.Accepted, Version = 1, CreatedBy = "bl521-fix4"
            };
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).InsertOneAsync(administrator);
            Auth.Provision(email, roles.Select(r => r.ToString()).ToList());
            return administrator;
        }

        public async Task<PlatformAdministrator> ReadAsync(Guid id) =>
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).Find(x => x.Id == id).SingleAsync();

        /// <summary>Another user's save of the record: only its version moves.</summary>
        public Task BumpVersionAsync(Guid id) =>
            _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators)
                .UpdateOneAsync(x => x.Id == id, Builders<PlatformAdministrator>.Update.Inc(x => x.Version, 1));

        public async Task<Response<NoContent>> UpdateAsync(
            PlatformAdministrator target,
            PlatformAdministrator actingAs,
            AuthClient auth,
            Func<UpdatePlatformAdministratorRequest, UpdatePlatformAdministratorRequest> change,
            IPlatformAdministratorRepository? repository = null,
            ILogger<UpdatePlatformAdministratorHandler>? logger = null)
        {
            var current = await ReadAsync(target.Id);
            var request = change(new UpdatePlatformAdministratorRequest(
                current.Email, current.UserName, current.DisplayName, current.ActorType.ToString(), current.PartnerId,
                current.AllowedTenantIds, current.Status.ToString(), current.Roles.Select(r => r.ToString()).ToList(), current.Version));
            var user = new Actor(actingAs.Email);
            repository ??= Repository;
            return await new UpdatePlatformAdministratorHandler(repository, auth, user, new ActorSafetyGuard(user, repository),
                    logger ?? new CapturingLogger())
                .Handle(new UpdatePlatformAdministratorCommand(target.Id, request), CancellationToken.None);
        }

        public ValueTask DisposeAsync() => _mongo.DisposeAsync();
    }

    /// <summary>Auth as the sync sees it: per address, the roles and the status the last applied sync gave the account.</summary>
    private sealed class FakeAuth
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, (List<string> Roles, string? Status)> _accounts = new(StringComparer.OrdinalIgnoreCase);

        public void Provision(string email, List<string> roles)
        {
            lock (_gate) _accounts[email] = (roles, "Active");
        }

        public void Apply(PlatformAdministratorProvisioningSyncRequest request)
        {
            lock (_gate)
            {
                if (!_accounts.ContainsKey(request.Email))
                {
                    throw new InvalidOperationException("platform user not found");
                }

                _accounts[request.Email] = (request.Roles.ToList(), request.Status);
            }
        }

        public List<string> RolesOf(string email)
        {
            lock (_gate) return _accounts[email].Roles.ToList();
        }

        public string? StatusOf(string email)
        {
            lock (_gate) return _accounts[email].Status;
        }
    }

    private sealed class AuthClient(FakeAuth auth, Func<int, Task>? before, Func<int, Task>? after) : IPlatformAdministratorProvisioningService
    {
        private readonly object _gate = new();

        public List<PlatformAdministratorProvisioningSyncRequest> Calls { get; } = [];

        public Task<PlatformAdministratorProvisioningResult> ProvisionAsync(PlatformAdministratorProvisioningRequest request, CancellationToken ct) =>
            Task.FromResult(new PlatformAdministratorProvisioningResult(null, true));

        public async Task SyncAsync(PlatformAdministratorProvisioningSyncRequest request, CancellationToken ct)
        {
            int call;
            lock (_gate)
            {
                call = Calls.Count;
                Calls.Add(request);
            }

            if (before is not null) await before(call);
            auth.Apply(request);
            if (after is not null) await after(call);
        }
    }

    /// <summary>The production repository, except that the record write times out (after Auth was synced).</summary>
    private sealed class ThrowingWrites(IPlatformAdministratorRepository inner) : IPlatformAdministratorRepository
    {
        public Task<bool> UpdateAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default, AuthSyncPendingEffect authSyncPending = AuthSyncPendingEffect.Keep) =>
            throw new TimeoutException("the database did not answer the write");

        public Task<SuperAdminGuardedWriteResult> UpdateGuardedAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default, AuthSyncPendingEffect authSyncPending = AuthSyncPendingEffect.Keep) =>
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

    private sealed class CapturingLogger : ILogger<UpdatePlatformAdministratorHandler>
    {
        private readonly object _gate = new();
        private readonly List<(LogLevel Level, string Message)> _entries = [];

        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get { lock (_gate) return _entries.ToList(); }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_gate) _entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
