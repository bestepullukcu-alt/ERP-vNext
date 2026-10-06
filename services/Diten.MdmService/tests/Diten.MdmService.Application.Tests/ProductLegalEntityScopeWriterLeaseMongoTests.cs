using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Tests.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX1 B4 (and B2's 503, B3's quiet workers) on a real Mongo — the write admission of every
/// product writer through the coordinator, as the pipeline behavior drives it.
/// </summary>
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class ProductLegalEntityScopeWriterLeaseMongoTests
    : IClassFixture<AuditIntentTemporalMongoFixture>, IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);
    private readonly string? _previousMongoConnection;

    public ProductLegalEntityScopeWriterLeaseMongoTests(AuditIntentTemporalMongoFixture fixture)
    {
        _previousMongoConnection = Environment.GetEnvironmentVariable("MDM_TEST_MONGO");
        Environment.SetEnvironmentVariable("MDM_TEST_MONGO", fixture.ReplicaConnectionString);
    }

    public void Dispose() => Environment.SetEnvironmentVariable("MDM_TEST_MONGO", _previousMongoConnection);

    [Fact]
    public async Task A_write_answered_202_is_finished_by_the_same_command_even_after_its_product_moved()
    {
        // Before: the repeat compared a hash of the WHOLE tenant (11 collections) with the first attempt's — the first
        // attempt's own partial write (a consumed reservation) made it differ: PAYLOAD_DRIFT, and every product write of
        // the tenant refused for good.
        await using var harness = await Harness.CreateAsync();
        var product = Guid.NewGuid();
        var write = new TestWrite(product, Guid.NewGuid());

        var first = await harness.Coordinator().EnterAsync(write, default);
        Assert.True(first.IsAllowed);
        await harness.Coordinator().CompleteAsync(first, false, 202, false, default);
        await harness.InsertProductAsync(product);   // the first attempt's own unfinished write

        var repeat = await harness.Coordinator().EnterAsync(write, default);

        Assert.True(repeat.IsAllowed, repeat.FailureCode);
        Assert.True(repeat.OwnsLease);
        await harness.Coordinator().CompleteAsync(repeat, true, 200, false, default);
        Assert.Empty((await harness.Rollouts.GetAsync())!.ProductWriterLeases);
    }

    [Fact]
    public async Task An_expired_lease_of_another_command_is_taken_over_and_an_unexpired_one_is_not()
    {
        await using var harness = await Harness.CreateAsync();
        var product = Guid.NewGuid();
        var held = await harness.Coordinator().EnterAsync(new TestWrite(product, Guid.NewGuid()), default);
        await harness.Coordinator().CompleteAsync(held, false, 202, false, default);

        harness.Clock.Now = Start.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds - 1);
        var early = await harness.Coordinator().EnterAsync(new TestWrite(product, Guid.NewGuid()), default);
        Assert.False(early.IsAllowed);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", early.FailureCode);

        harness.Clock.Now = Start.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds + 1);
        var takeover = new TestWrite(product, Guid.NewGuid());
        var late = await harness.Coordinator().EnterAsync(takeover, default);

        Assert.True(late.IsAllowed, late.FailureCode);
        var lease = Assert.Single((await harness.Rollouts.GetAsync())!.ProductWriterLeases);
        Assert.Equal(takeover.CommandId, lease.CommandId);
    }

    [Fact]
    public async Task Writers_of_different_products_proceed_side_by_side_and_one_product_admits_one_writer()
    {
        // Before: one lease per tenant — the second concurrent user anywhere in the tenant was refused (409).
        await using var harness = await Harness.CreateAsync();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var onA = await harness.Coordinator().EnterAsync(new TestWrite(a, Guid.NewGuid()), default);
        var onB = await harness.Coordinator().EnterAsync(new TestWrite(b, Guid.NewGuid()), default);
        var secondOnA = await harness.Coordinator().EnterAsync(new TestWrite(a, Guid.NewGuid()), default);

        Assert.True(onA.IsAllowed, onA.FailureCode);
        Assert.True(onB.IsAllowed, onB.FailureCode);
        Assert.False(secondOnA.IsAllowed);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", secondOnA.FailureCode);
        Assert.Equal(2, (await harness.Rollouts.GetAsync())!.ProductWriterLeases.Count);
    }

    [Fact]
    public async Task A_failed_write_that_changed_nothing_frees_its_product_and_one_that_changed_it_keeps_the_lease()
    {
        await using var harness = await Harness.CreateAsync();
        var product = Guid.NewGuid();
        var unchanged = await harness.Coordinator().EnterAsync(new TestWrite(product, Guid.NewGuid()), default);
        await harness.Coordinator().CompleteAsync(unchanged, false, 409, false, default);
        Assert.Empty((await harness.Rollouts.GetAsync())!.ProductWriterLeases);

        var changing = await harness.Coordinator().EnterAsync(new TestWrite(product, Guid.NewGuid()), default);
        await harness.InsertProductAsync(product);
        await harness.Coordinator().CompleteAsync(changing, false, 409, false, default);

        Assert.Single((await harness.Rollouts.GetAsync())!.ProductWriterLeases);
    }

    [Fact]
    public async Task A_fence_waits_for_a_running_writer_and_clears_an_expired_one()
    {
        await using var harness = await Harness.CreateAsync();
        var writer = await harness.Coordinator().EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);
        Assert.True(writer.IsAllowed);
        var rollout = (await harness.Rollouts.GetAsync())!;

        var blocked = await harness.Rollouts.AcquireFenceAsync(Fence(Start.AddSeconds(5)), rollout.Id, rollout.Version, ProductLegalEntityScopeRolloutMode.Preparation);
        Assert.False(blocked.Acquired);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_ACTIVE", blocked.FailureCode);

        var fenced = await harness.Rollouts.AcquireFenceAsync(
            Fence(Start.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds + 1)), rollout.Id, rollout.Version,
            ProductLegalEntityScopeRolloutMode.Preparation);
        Assert.True(fenced.Acquired, fenced.FailureCode);
        Assert.Empty((await harness.Rollouts.GetAsync())!.ProductWriterLeases);
    }

    [Fact]
    public async Task A_rollout_written_before_FIX1_without_the_new_fields_admits_writers_and_a_fence()
    {
        // CT audit (BL-529 FIX6): a condition on a field a record does not have must still match it. A record written
        // before FIX1 has no ProductWriterLeases, no ProductWriterLeaseRevision, no LastFenceAbort.
        await using var harness = await Harness.CreateAsync();
        await harness.UnsetNewFieldsAsync();

        var lease = await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()));
        Assert.True(lease.Acquired, lease.FailureCode);
        Assert.True(await harness.Rollouts.ReleaseProductWriterLeaseAsync(lease.Lease!.Token, lease.Lease.Generation));

        await harness.UnsetNewFieldsAsync();
        var rollout = (await harness.Rollouts.GetAsync())!;
        var fenced = await harness.Rollouts.AcquireFenceAsync(Fence(Start), rollout.Id, rollout.Version, ProductLegalEntityScopeRolloutMode.Preparation);
        Assert.True(fenced.Acquired, fenced.FailureCode);

        await harness.UnsetNewFieldsAsync();
        var snapshot = Snapshot();
        Assert.True(await harness.Rollouts.BindFenceSnapshotsAsync(fenced.Fence!.Token, snapshot, snapshot));
    }

    [Fact]
    public async Task A_suspended_tenant_gives_no_writer_lease_of_either_kind()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SetModeAsync(ProductLegalEntityScopeRolloutMode.FailClosedSuspended);

        var product = await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()));
        var slot = await harness.Rollouts.AcquireWriterLeaseAsync(harness.Lease(Guid.NewGuid()));

        Assert.Equal("PRODUCT_SCOPE_FAIL_CLOSED_SUSPENDED", product.FailureCode);
        Assert.Equal("PRODUCT_SCOPE_FAIL_CLOSED_SUSPENDED", slot.FailureCode);
    }

    [Fact]
    public async Task A_rollout_record_that_cannot_be_read_refuses_writes_and_reads_with_a_coded_503()
    {
        // B2: before, the repository's refusal escaped both the guard and the admission as an unhandled exception.
        await using var harness = await Harness.CreateAsync();
        await harness.BreakRolloutAsync();

        var admission = await harness.Coordinator().EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);
        var read = await harness.Guard().ResolveContextAsync(ProductLegalEntityScopeConsumerGuard.GlobalProductReadPermission);

        Assert.False(admission.IsAllowed);
        Assert.Equal(503, admission.StatusCode);
        Assert.Equal("PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID", admission.FailureCode);
        Assert.False(read.IsSuccessful);
        Assert.Equal(503, read.StatusCode);
        Assert.Equal("PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID", read.FailureCode);
    }

    [Fact]
    public async Task Recovery_workers_do_not_write_while_a_fence_is_up()
    {
        // B3: the workers looked only at Mode == Preparation and could move the inventory between the fence's snapshots.
        await using var harness = await Harness.CreateAsync();
        var background = new ProductIdentityBackgroundScope(harness.Rollouts, harness.Scope.PolicyRepository(), harness.Tenant, harness.Clock);
        Assert.Equal(ProductIdentityBackgroundScope.TenantMode.Everything, await background.ResolveAsync(default));

        var rollout = (await harness.Rollouts.GetAsync())!;
        Assert.True((await harness.Rollouts.AcquireFenceAsync(Fence(Start), rollout.Id, rollout.Version, ProductLegalEntityScopeRolloutMode.Preparation)).Acquired);

        Assert.Equal(ProductIdentityBackgroundScope.TenantMode.Nothing, await background.ResolveAsync(default));
    }

    [Fact]
    public async Task The_guard_reads_now_from_its_time_provider()
    {
        // S12: DateTimeOffset.UtcNow → TimeProvider.
        await using var harness = await Harness.CreateAsync();
        harness.Clock.Now = new DateTimeOffset(2031, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var context = await harness.Guard().ResolveContextAsync(ProductLegalEntityScopeConsumerGuard.GlobalProductReadPermission);

        Assert.Equal(harness.Clock.Now, context.Context!.ServerNowUtc);
    }

    private static ProductLegalEntityScopeActivationFence Fence(DateTimeOffset acquiredAt) => new()
    {
        Token = new string('C', 64), State = ProductLegalEntityScopeAdmissionState.Closing, Action = "ActivateEnforced",
        CommandId = Guid.NewGuid(), ActorId = Guid.NewGuid(), ReasonCode = "OWNER_APPROVED", AcquiredAtUtc = acquiredAt
    };

    private static ProductLegalEntityScopeInventorySnapshot Snapshot()
    {
        var payload = $"schemaVersion=1\nobservedUtcTicks={Start.UtcTicks}\nmarker=L\n";
        static string Sha(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
        return new ProductLegalEntityScopeInventorySnapshot
        {
            ObservedAtUtc = Start, CanonicalPayload = payload,
            StableFactsHash = Sha("schemaVersion=1\nmarker=L\n"), SnapshotHash = Sha(payload)
        };
    }

    /// <summary>A product writer as the pipeline sees one: its command id and the product it touches.</summary>
    public sealed record TestWrite(Guid GlobalProductId, Guid CommandId) : IProductLegalEntityScopeInventoryMutation;

    private sealed class Harness : IAsyncDisposable
    {
        private Harness(ProductScopeMongoScope scope)
        {
            Scope = scope;
            Tenant = new TenantContext();
            Tenant.SetTenant(scope.TenantId);
            Rollouts = scope.RolloutRepository();
        }

        public ProductScopeMongoScope Scope { get; }
        public TenantContext Tenant { get; }
        public ManualClock Clock { get; } = new(Start);
        public ProductLegalEntityScopeRolloutStateRepository Rollouts { get; }
        public Guid Actor { get; } = Guid.NewGuid();

        public static async Task<Harness> CreateAsync()
        {
            var harness = new Harness(await ProductScopeMongoScope.CreateAsync());
            var created = await harness.Rollouts.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
                harness.Scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Start));
            Assert.True(created.Succeeded);
            return harness;
        }

        public ProductLegalEntityScopeWriteFenceCoordinator Coordinator() => new(
            Rollouts,
            new ProductLegalEntityScopeOperationalReadinessRepository(Scope.Database, Tenant),
            new Actor(Actor.ToString("D")),
            Tenant,
            Clock);

        public ProductLegalEntityScopeConsumerGuard Guard() => new(
            Rollouts, Scope.PolicyRepository(),
            new ProductLegalEntityScopeCandidateFacade(new NoProvider(), new InMemoryLegalEntityRepository(Scope.TenantId, []), Tenant, new Actor(Actor.ToString("D"))),
            Tenant, Clock);

        public ProductLegalEntityScopeWriterLease Lease(Guid product) => new()
        {
            Token = Guid.NewGuid(), Generation = 1, CommandId = Guid.NewGuid(), ActorId = Actor,
            MutationKind = "TestWrite", PayloadFingerprint = new string('B', 64), Owner = "Diten.MDM:TestWrite",
            AcquiredAtUtc = Clock.Now, ExpiresAtUtc = Clock.Now.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds),
            ObjectKeys = [$"GlobalProductId:{product:D}"]
        };

        public Task InsertProductAsync(Guid id) =>
            Scope.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(new GlobalProduct
            {
                Id = id, TenantId = Scope.TenantId, CanonicalCode = "GP-" + id.ToString("N")[..8].ToUpperInvariant(),
                GlobalProductName = "Lease test product", GlobalProductNameNormalized = "LEASE TEST PRODUCT",
                CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.Draft, CreatedAt = Start
            });

        public Task UnsetNewFieldsAsync() =>
            Scope.Database.GetCollection<BsonDocument>("mdm_product_legal_entity_scope_rollout_states").UpdateOneAsync(
                new BsonDocument("TenantId", new BsonBinaryData(Scope.TenantId, GuidRepresentation.Standard)),
                new BsonDocument("$unset", new BsonDocument
                {
                    { "ProductWriterLeases", "" }, { "ProductWriterLeaseRevision", "" }, { "LastFenceAbort", "" }
                }));

        public Task SetModeAsync(ProductLegalEntityScopeRolloutMode mode) =>
            Scope.RolloutCollection.UpdateOneAsync(item => item.TenantId == Scope.TenantId,
                Builders<ProductLegalEntityScopeRolloutState>.Update.Set(item => item.Mode, mode));

        /// <summary>A transition record without its evidence: what the B2 defect left behind.</summary>
        public Task BreakRolloutAsync() =>
            Scope.Database.GetCollection<BsonDocument>("mdm_product_legal_entity_scope_rollout_states").UpdateOneAsync(
                new BsonDocument("TenantId", new BsonBinaryData(Scope.TenantId, GuidRepresentation.Standard)),
                new BsonDocument("$set", new BsonDocument("LastTransitionAction", "SuspendFailClosed")));

        public async ValueTask DisposeAsync()
        {
            await Scope.Database.GetCollection<GlobalProduct>("mdm_global_products").DeleteManyAsync(item => item.TenantId == Scope.TenantId);
            await Scope.DisposeAsync();
        }
    }

    private sealed class Actor(string id) : IProductIdentityActorContext
    {
        public string ActorId { get; } = id;
    }

    private sealed class NoProvider : Diten.MdmService.Application.Contracts.Authorization.ITrustedLegalEntityScopeProvider
    {
        public Task<Diten.MdmService.Application.Contracts.Authorization.TrustedLegalEntityScopeProviderResult> ResolveAsync(
            Guid expectedTenantId, Guid expectedSubjectId, string moduleCode, string permissionKey, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("A Preparation tenant never asks the provider.");
    }
}

/// <summary>A clock a test moves.</summary>
internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
}
