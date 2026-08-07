using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.S2S;

[Collection(AuthServiceRealMongoTestCollection.Name)]
public sealed class ExplicitRoleGrantProvisioningMongoTests
{
    public static IEnumerable<object[]> Participants => Enum.GetValues<ExplicitRoleGrantTransactionParticipant>().Select(x => new object[] { x });

    [Fact]
    public async Task Grant_replay_new_key_noop_revoke_and_absent_revoke_have_exact_version_semantics()
    {
        await using var fixture = await Fixture.CreateAsync();
        var grant = fixture.Request(ExplicitRoleGrantMutationV1.Grant, "grant-1");
        var applied = await fixture.Coordinator.ExecuteAsync(grant, CancellationToken.None);
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Applied, applied.Status); Assert.True(applied.AuthorizationStateChanged); Assert.Equal(1, applied.AuthorizationVersion);
        var replay = await fixture.Coordinator.ExecuteAsync(grant, CancellationToken.None);
        Assert.Equal(applied, replay);
        var noOp = await fixture.Coordinator.ExecuteAsync(fixture.Request(ExplicitRoleGrantMutationV1.Grant, "grant-2"), CancellationToken.None);
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.NoOp, noOp.Status); Assert.Equal(1, noOp.AuthorizationVersion);
        var revoked = await fixture.Coordinator.ExecuteAsync(fixture.Request(ExplicitRoleGrantMutationV1.Revoke, "revoke-1"), CancellationToken.None);
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Applied, revoked.Status); Assert.Equal(2, revoked.AuthorizationVersion);
        var absent = await fixture.Coordinator.ExecuteAsync(fixture.Request(ExplicitRoleGrantMutationV1.Revoke, "revoke-2"), CancellationToken.None);
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.NoOp, absent.Status); Assert.Equal(2, absent.AuthorizationVersion);
        Assert.Equal(0, await fixture.Count("rolePermissions")); Assert.Equal(4, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));
        Assert.Equal(4, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.AuditsCollection)); Assert.Equal(0, await fixture.Count("entitlementStates"));
    }

    [Fact]
    public async Task Same_identity_different_payload_is_409_and_actor_operation_tenant_are_identity_parts()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Request(ExplicitRoleGrantMutationV1.Grant, "same-key");
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Applied, (await fixture.Coordinator.ExecuteAsync(request, CancellationToken.None)).Status);
        var changed = request with { PermissionId = Guid.NewGuid() };
        changed = changed with { CanonicalPayloadHash = ExplicitRoleGrantProvisioningV1.ComputeHash(changed) };
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Conflict, (await fixture.Coordinator.ExecuteAsync(changed, CancellationToken.None)).Status);
        var actorRequest = fixture.Request(ExplicitRoleGrantMutationV1.Grant, "same-key") with { AuthenticatedActorId = Guid.NewGuid() };
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.NoOp, (await fixture.Coordinator.ExecuteAsync(actorRequest, CancellationToken.None)).Status);
        var revoke = fixture.Request(ExplicitRoleGrantMutationV1.Revoke, "same-key");
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Applied, (await fixture.Coordinator.ExecuteAsync(revoke, CancellationToken.None)).Status);
        Assert.Equal(3, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));
    }

    [Fact]
    public async Task Authorization_cross_tenant_scope_and_manifest_ownership_fail_closed()
    {
        var denied = await Fixture.CreateAsync(ExplicitRoleGrantAuthorizationDecision.Denied);
        await using (denied) { Assert.Equal(ExplicitRoleGrantProvisioningStatus.Forbidden, (await denied.Coordinator.ExecuteAsync(denied.Request(ExplicitRoleGrantMutationV1.Grant, "deny"), CancellationToken.None)).Status); Assert.Equal(0, await denied.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection)); }
        var unavailable = await Fixture.CreateAsync(ExplicitRoleGrantAuthorizationDecision.Unavailable);
        await using (unavailable) { Assert.Equal(ExplicitRoleGrantProvisioningStatus.Unavailable, (await unavailable.Coordinator.ExecuteAsync(unavailable.Request(ExplicitRoleGrantMutationV1.Grant, "unavailable"), CancellationToken.None)).Status); }
        await using var fixture = await Fixture.CreateAsync();
        var crossTenant = fixture.Request(ExplicitRoleGrantMutationV1.Grant, "cross") with { TenantId = Guid.NewGuid() };
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.NotFound, (await fixture.Coordinator.ExecuteAsync(crossTenant, CancellationToken.None)).Status);
        await fixture.Database.GetCollection<PermissionOwnerDocument>("s2sPermissionCatalogPermissionOwners").DeleteManyAsync(FilterDefinition<PermissionOwnerDocument>.Empty);
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Conflict, (await fixture.Coordinator.ExecuteAsync(fixture.Request(ExplicitRoleGrantMutationV1.Grant, "owner"), CancellationToken.None)).Status);
        Assert.Equal(0, await fixture.Count("rolePermissions")); Assert.Equal(0, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));
    }

    [Theory]
    [MemberData(nameof(Participants))]
    public async Task Failure_after_each_participant_rolls_back_all_state(ExplicitRoleGrantTransactionParticipant participant)
    {
        await using var fixture = await Fixture.CreateAsync(); var before = await fixture.SnapshotAsync();
        fixture.Probe.FailAfter = participant;
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Unavailable, (await fixture.Coordinator.ExecuteAsync(fixture.Request(ExplicitRoleGrantMutationV1.Grant, "fault"), CancellationToken.None)).Status);
        Assert.Equal(before, await fixture.SnapshotAsync()); fixture.Probe.FailAfter = null;
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Applied, (await fixture.Coordinator.ExecuteAsync(fixture.Request(ExplicitRoleGrantMutationV1.Grant, "fault"), CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Unknown_commit_is_commit_only_body_once_and_exhaustion_is_503()
    {
        await using var before = await Fixture.CreateAsync(); before.Probe.UnknownBefore = 1;
        var beforeResult = await before.Coordinator.ExecuteAsync(before.Request(ExplicitRoleGrantMutationV1.Grant, "before"), CancellationToken.None);
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Applied, beforeResult.Status);
        Assert.Equal(1, beforeResult.AuthorizationVersion);
        Assert.Equal(1, before.Probe.BodyCount); Assert.Equal(2, before.Probe.CommitCount);
        Assert.Equal(0, before.Probe.BarrierInvocationCount);
        Assert.Equal(1, await before.RoleFenceAsync());
        await using var after = await Fixture.CreateAsync(); after.Probe.UnknownAfter = true;
        var afterResult = await after.Coordinator.ExecuteAsync(after.Request(ExplicitRoleGrantMutationV1.Grant, "after"), CancellationToken.None);
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Applied, afterResult.Status);
        Assert.Equal(1, afterResult.AuthorizationVersion);
        Assert.Equal(1, after.Probe.BodyCount); Assert.Equal(1, after.Probe.CommitCount);
        Assert.Equal(0, after.Probe.BarrierInvocationCount);
        Assert.Equal(1, await after.RoleFenceAsync());
        Assert.Equal(1, await after.Count("rolePermissions"));
        Assert.Equal(1, await after.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));
        Assert.Equal(1, await after.Count(ExplicitRoleGrantProvisioningCoordinator.VersionsCollection));
        Assert.Equal(1, await after.Count(ExplicitRoleGrantProvisioningCoordinator.AuditsCollection));
        await using var exhausted = await Fixture.CreateAsync(); exhausted.Probe.UnknownBefore = 3;
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Unavailable, (await exhausted.Coordinator.ExecuteAsync(exhausted.Request(ExplicitRoleGrantMutationV1.Grant, "exhaust"), CancellationToken.None)).Status);
        Assert.Equal(1, exhausted.Probe.BodyCount); Assert.Equal(3, exhausted.Probe.CommitCount);
        Assert.Equal(0, exhausted.Probe.BarrierInvocationCount);
        Assert.Equal(0, await exhausted.Count("rolePermissions"));
        Assert.Equal(0, await exhausted.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));
        Assert.Equal(0, await exhausted.Count(ExplicitRoleGrantProvisioningCoordinator.VersionsCollection));
        Assert.Equal(0, await exhausted.Count(ExplicitRoleGrantProvisioningCoordinator.AuditsCollection));
        Assert.Equal(0, await exhausted.RoleFenceAsync());
    }

    [Fact]
    public async Task Cancellation_propagates_with_zero_residue_and_indexes_have_no_ttl()
    {
        await using var fixture = await Fixture.CreateAsync(); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Coordinator.ExecuteAsync(fixture.Request(ExplicitRoleGrantMutationV1.Grant, "cancel"), cancelled.Token));
        Assert.Equal(0, await fixture.Count("rolePermissions")); Assert.Equal(0, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));
        var result = await fixture.Coordinator.ExecuteAsync(fixture.Request(ExplicitRoleGrantMutationV1.Grant, "indexes"), CancellationToken.None);
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Applied, result.Status);
        var indexes = await (await fixture.Database.GetCollection<ExplicitRoleGrantReceiptDocument>(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection).Indexes.ListAsync()).ToListAsync();
        var exact = Assert.Single(indexes, x => x["name"] == "ux_tenant_actor_operation_idempotency"); Assert.True(exact["unique"].ToBoolean()); Assert.False(exact.Contains("expireAfterSeconds"));
    }

    [Fact]
    public async Task Concurrent_distinct_idempotency_keys_for_same_grant_have_one_change_one_noop()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = fixture.Request(ExplicitRoleGrantMutationV1.Grant, "concurrent-1");
        var second = fixture.Request(ExplicitRoleGrantMutationV1.Grant, "concurrent-2");
        var results = await Task.WhenAll(
            fixture.Coordinator.ExecuteAsync(first, CancellationToken.None),
            fixture.Coordinator.ExecuteAsync(second, CancellationToken.None));

        Assert.Contains(results, result => result.Status == ExplicitRoleGrantProvisioningStatus.Applied);
        Assert.True(results.Any(result => result.Status == ExplicitRoleGrantProvisioningStatus.NoOp),
            $"Expected NoOp; statuses were {string.Join(',', results.Select(result => result.Status))}; body attempts: {fixture.Probe.BodyCount}.");
        Assert.All(results, result => Assert.Equal(1, result.AuthorizationVersion));
        Assert.Equal(1, await fixture.Count("rolePermissions"));
        Assert.Equal(2, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));
        Assert.Equal(2, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.AuditsCollection));
    }

    [Fact]
    public async Task Concurrent_same_key_is_stable_and_distinct_absent_revokes_are_both_noop()
    {
        await using var grantFixture = await Fixture.CreateAsync();
        var same = grantFixture.Request(ExplicitRoleGrantMutationV1.Grant, "same-concurrent");
        var sameResults = await Task.WhenAll(
            grantFixture.Coordinator.ExecuteAsync(same, CancellationToken.None),
            grantFixture.Coordinator.ExecuteAsync(same, CancellationToken.None));
        Assert.Equal(sameResults[0], sameResults[1]);
        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Applied, sameResults[0].Status);
        Assert.Equal(1, await grantFixture.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));

        await using var revokeFixture = await Fixture.CreateAsync();
        var revokes = await Task.WhenAll(
            revokeFixture.Coordinator.ExecuteAsync(revokeFixture.Request(ExplicitRoleGrantMutationV1.Revoke, "absent-1"), CancellationToken.None),
            revokeFixture.Coordinator.ExecuteAsync(revokeFixture.Request(ExplicitRoleGrantMutationV1.Revoke, "absent-2"), CancellationToken.None));
        Assert.All(revokes, result =>
        {
            Assert.Equal(ExplicitRoleGrantProvisioningStatus.NoOp, result.Status);
            Assert.Equal(0, result.AuthorizationVersion);
        });
        Assert.Equal(2, await revokeFixture.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));
        Assert.Equal(0, await revokeFixture.Count(ExplicitRoleGrantProvisioningCoordinator.VersionsCollection));
    }

    [Fact]
    public async Task Transient_retry_is_bounded_and_exhaustion_has_no_authorization_residue()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Probe.TransientAfter = ExplicitRoleGrantTransactionParticipant.RoleFence;
        fixture.Probe.TransientRemaining = 3;

        var result = await fixture.Coordinator.ExecuteAsync(
            fixture.Request(ExplicitRoleGrantMutationV1.Grant, "transient-exhausted"), CancellationToken.None);

        Assert.Equal(ExplicitRoleGrantProvisioningStatus.Unavailable, result.Status);
        Assert.Equal(3, fixture.Probe.BodyCount);
        Assert.Equal(0, await fixture.Count("rolePermissions"));
        Assert.Equal(0, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));
        Assert.Equal(0, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.VersionsCollection));
        Assert.Equal(0, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.AuditsCollection));
    }

    [Fact]
    public async Task Retry_barrier_returns_not_found_without_cross_tenant_disclosure()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Probe.TransientAfter = ExplicitRoleGrantTransactionParticipant.RoleFence;
        fixture.Probe.TransientRemaining = 1;
        var otherTenantId = Guid.NewGuid();
        fixture.Probe.BeforeRetryBarrierAsyncAction = () => fixture.Database.GetCollection<Role>("roles")
            .UpdateOneAsync(Builders<Role>.Filter.And(
                    Builders<Role>.Filter.Eq(role => role.Id, fixture.RoleId),
                    Builders<Role>.Filter.Eq(role => role.TenantId, fixture.TenantId)),
                Builders<Role>.Update.Set(role => role.TenantId, otherTenantId));

        var result = await fixture.Coordinator.ExecuteAsync(
            fixture.Request(ExplicitRoleGrantMutationV1.Grant, "deleted-during-retry"), CancellationToken.None);

        Assert.Equal(ExplicitRoleGrantProvisioningStatus.NotFound, result.Status);
        Assert.Equal(1, fixture.Probe.BodyCount);
        Assert.Equal(0, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));
        var movedRole = await fixture.Database.GetCollection<Role>("roles").Find(role => role.Id == fixture.RoleId).SingleAsync();
        Assert.Equal(otherTenantId, movedRole.TenantId);
        Assert.Equal(0, movedRole.ExplicitGrantValidationFence);
    }

    [Fact]
    public async Task Cancellation_at_transient_boundary_propagates_without_retry()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        fixture.Probe.TransientAfter = ExplicitRoleGrantTransactionParticipant.RoleFence;
        fixture.Probe.TransientRemaining = 1;
        fixture.Probe.BeforeRetryBarrierAsyncAction = () => { cancellation.Cancel(); return Task.CompletedTask; };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Coordinator.ExecuteAsync(
            fixture.Request(ExplicitRoleGrantMutationV1.Grant, "cancel-at-retry"), cancellation.Token));
        Assert.Equal(1, fixture.Probe.BodyCount);
        Assert.Equal(0, await fixture.Count("rolePermissions"));
        Assert.Equal(0, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));
    }

    [Fact]
    public async Task Twenty_physical_mutations_produce_twenty_version_bumps_and_complete_audit_chain()
    {
        await using var fixture = await Fixture.CreateAsync();
        var permissionIds = new List<Guid> { fixture.PermissionId };
        for (var index = 1; index < 10; index++)
            permissionIds.Add(await fixture.AddPermissionAsync(index));

        var expectedVersion = 0L;
        foreach (var permissionId in permissionIds)
        {
            var result = await fixture.Coordinator.ExecuteAsync(
                fixture.Request(ExplicitRoleGrantMutationV1.Grant, $"grant-{permissionId:N}", permissionId),
                CancellationToken.None);
            Assert.Equal(ExplicitRoleGrantProvisioningStatus.Applied, result.Status);
            Assert.Equal(++expectedVersion, result.AuthorizationVersion);
        }
        foreach (var permissionId in permissionIds)
        {
            var result = await fixture.Coordinator.ExecuteAsync(
                fixture.Request(ExplicitRoleGrantMutationV1.Revoke, $"revoke-{permissionId:N}", permissionId),
                CancellationToken.None);
            Assert.Equal(ExplicitRoleGrantProvisioningStatus.Applied, result.Status);
            Assert.Equal(++expectedVersion, result.AuthorizationVersion);
        }

        Assert.Equal(20, expectedVersion);
        Assert.Equal(0, await fixture.Count("rolePermissions"));
        Assert.Equal(20, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection));
        Assert.Equal(20, await fixture.Count(ExplicitRoleGrantProvisioningCoordinator.AuditsCollection));
        var version = await fixture.Database.GetCollection<BsonDocument>(ExplicitRoleGrantProvisioningCoordinator.VersionsCollection)
            .Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        Assert.Equal(20, version["Version"].AsInt64);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly MongoClient _client; private readonly string _name;
        public IMongoDatabase Database { get; } public RecordingProbe Probe { get; } = new();
        public ExplicitRoleGrantProvisioningCoordinator Coordinator { get; }
        public Guid TenantId { get; } = Guid.NewGuid(); public Guid ActorId { get; } = Guid.NewGuid(); public Guid RoleId { get; } = Guid.NewGuid(); public Guid PermissionId { get; } = Guid.NewGuid();
        private Fixture(MongoClient client, IMongoDatabase database, string name, ExplicitRoleGrantAuthorizationDecision decision)
        { _client = client; Database = database; _name = name; Coordinator = new(client, database, new FixedAuthorizer(decision), Probe); }
        public static async Task<Fixture> CreateAsync(ExplicitRoleGrantAuthorizationDecision decision = ExplicitRoleGrantAuthorizationDecision.Allowed)
        {
            var uri = Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? throw new InvalidOperationException("MONGO_TEST_URI required"); var url = new MongoUrl(uri);
            if (url.Servers.Any(x => x.Port is 27017 or 27018)) throw new InvalidOperationException("Protected Mongo ports are forbidden.");
            var policy = typeof(RoleAssignmentVersionRepository).Assembly.GetType("Diten.AuthService.Persistence.MongoGuidRepresentationPolicy", throwOnError: true)!;
            var createSettings = policy.GetMethod("CreateClientSettings", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            var client = new MongoClient((MongoClientSettings)createSettings.Invoke(null, [uri])!); var name = "diten_auth_b2a2_" + Guid.NewGuid().ToString("N"); var db = client.GetDatabase(name); await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            var fixture = new Fixture(client, db, name, decision); await fixture.SeedAsync(); return fixture;
        }
        private async Task SeedAsync()
        {
            await Database.GetCollection<Role>("roles").InsertOneAsync(new Role("Operator", "Operator", null, TenantId) { Id = RoleId });
            var permission = new Permission("management-governance", "decisions", "read", "read", null, "MOD-0007", PermissionScope.Tenant) { Id = PermissionId };
            await Database.GetCollection<Permission>("permissions").InsertOneAsync(permission);
            await Database.GetCollection<PermissionOwnerDocument>("s2sPermissionCatalogPermissionOwners").InsertOneAsync(new() { RegistrationId = Guid.NewGuid(), PermissionId = PermissionId, OwnerModuleId = "MOD-0007", ModuleEntitlementCode = "MOD-0007", PermissionKey = permission.Key });
        }
        public ExplicitRoleGrantProvisioningV1 Request(ExplicitRoleGrantMutationV1 mutation, string key, Guid? permissionId = null) => ExplicitRoleGrantProvisioningV1.Create(TenantId, ActorId, RoleId, permissionId ?? PermissionId, mutation, key, "trusted-test");
        public async Task<Guid> AddPermissionAsync(int index)
        {
            var id = Guid.NewGuid();
            var permission = new Permission("management-governance", "decisions", $"read-{index}", $"read-{index}", null, "MOD-0007", PermissionScope.Tenant) { Id = id };
            await Database.GetCollection<Permission>("permissions").InsertOneAsync(permission);
            await Database.GetCollection<PermissionOwnerDocument>("s2sPermissionCatalogPermissionOwners").InsertOneAsync(new()
                { RegistrationId = Guid.NewGuid(), PermissionId = id, OwnerModuleId = "MOD-0007", ModuleEntitlementCode = "MOD-0007", PermissionKey = permission.Key });
            return id;
        }
        public async Task<long> Count(string collection) => await Database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        public async Task<long> RoleFenceAsync() =>
            (await Database.GetCollection<Role>("roles").Find(role => role.Id == RoleId).SingleAsync()).ExplicitGrantValidationFence;
        public async Task<string> SnapshotAsync()
        {
            var names = new[] { "roles", "permissions", "rolePermissions", ExplicitRoleGrantProvisioningCoordinator.ReceiptsCollection, ExplicitRoleGrantProvisioningCoordinator.VersionsCollection, ExplicitRoleGrantProvisioningCoordinator.AuditsCollection };
            var result = new List<string>(); foreach (var name in names) { var docs = await Database.GetCollection<BsonDocument>(name).Find(FilterDefinition<BsonDocument>.Empty).Sort(new BsonDocument("_id", 1)).ToListAsync(); result.Add(name + string.Join('|', docs.Select(x => Convert.ToHexString(x.ToBson())))); } return string.Join('\n', result);
        }
        public async ValueTask DisposeAsync() => await _client.DropDatabaseAsync(_name);
    }
    private sealed class FixedAuthorizer(ExplicitRoleGrantAuthorizationDecision decision) : IExplicitRoleGrantProvisioningAuthorizer
    { public Task<ExplicitRoleGrantAuthorizationDecision> AuthorizeAsync(Guid tenantId, Guid authenticatedActorId, ExplicitRoleGrantMutationV1 mutation, string trustedProvenance, CancellationToken cancellationToken) => Task.FromResult(decision); }
    private sealed class RecordingProbe : IExplicitRoleGrantTransactionProbe
    {
        public ExplicitRoleGrantTransactionParticipant? FailAfter { get; set; }
        public ExplicitRoleGrantTransactionParticipant? TransientAfter { get; set; }
        public int TransientRemaining { get; set; }
        public Func<Task>? BeforeRetryBarrierAsyncAction { get; set; }
        public int UnknownBefore { get; set; } public bool UnknownAfter { get; set; }
        public int BodyCount { get; private set; }
        public int CommitCount { get; private set; }
        public int BarrierInvocationCount { get; private set; }
        private bool _afterThrown;
        public void BodyStarted() => BodyCount++;
        public Task AfterParticipantAsync(ExplicitRoleGrantTransactionParticipant participant, CancellationToken cancellationToken)
        {
            if (FailAfter == participant) throw new ExplicitRoleGrantInjectedFailureException();
            if (TransientAfter == participant && TransientRemaining > 0)
            {
                TransientRemaining--;
                var transient = new MongoException("simulated transient transaction");
                transient.AddErrorLabel("TransientTransactionError");
                throw transient;
            }
            return Task.CompletedTask;
        }
        public Task BeforeRetryBarrierAsync(CancellationToken cancellationToken)
        {
            BarrierInvocationCount++;
            return BeforeRetryBarrierAsyncAction?.Invoke() ?? Task.CompletedTask;
        }
        public ExplicitRoleGrantCommitDirective BeforeCommit(int attempt) { CommitCount++; return attempt <= UnknownBefore ? ExplicitRoleGrantCommitDirective.SimulateUnknownBeforeSend : ExplicitRoleGrantCommitDirective.Send; }
        public void AfterCommit(int attempt) { if (UnknownAfter && !_afterThrown) { _afterThrown = true; throw new ExplicitRoleGrantUnknownCommitException(true); } }
    }
}
