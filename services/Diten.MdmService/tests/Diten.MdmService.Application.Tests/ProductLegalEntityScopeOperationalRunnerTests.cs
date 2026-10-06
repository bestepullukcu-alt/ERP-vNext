using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductLegalEntityScopes;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Tests.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX1 S8 — the operational runner's own tests (Pack:552-554, :800 asked for them; there were
/// none): the environment gate, the transitions end to end on a real Mongo (Activate → Suspend, B2), readiness before the
/// fence (B3), the same command over its own bound fence, the operator's abort, and the abandoned fence when the inventory
/// moves. Every transition's audit evidence is its own snapshot.
/// </summary>
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class ProductLegalEntityScopeOperationalRunnerTests
    : IClassFixture<AuditIntentTemporalMongoFixture>, IDisposable
{
    private readonly string? _previousMongoConnection;

    public ProductLegalEntityScopeOperationalRunnerTests(AuditIntentTemporalMongoFixture fixture)
    {
        _previousMongoConnection = Environment.GetEnvironmentVariable("MDM_TEST_MONGO");
        Environment.SetEnvironmentVariable("MDM_TEST_MONGO", fixture.ReplicaConnectionString);
    }

    public void Dispose() => Environment.SetEnvironmentVariable("MDM_TEST_MONGO", _previousMongoConnection);

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Test")]
    public async Task The_runner_refuses_every_environment_but_development(string environment)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var runner = Runner(scope, Bootstrap(scope, Guid.NewGuid(), Guid.NewGuid()), environment);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync());

        Assert.Equal("PRODUCT_SCOPE_OPERATIONAL_ENVIRONMENT_NOT_ALLOWED", refused.Message);
        Assert.Null(await scope.RolloutRepository().GetAsync());
    }

    [Fact]
    public async Task Activate_then_suspend_leaves_a_readable_rollout_whose_evidence_is_each_transitions_own_snapshot()
    {
        // B2: before, binding the Suspend's snapshot overwrote the Activate's evidence; the record became unreadable and
        // every product read and write of the tenant threw.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var actor = Guid.NewGuid();
        var rollout = await BootstrapAsync(scope, actor);

        var activate = Guid.NewGuid();
        await Runner(scope, Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, activate, actor)).RunAsync();
        var enforced = Assert.IsType<ProductLegalEntityScopeRolloutState>(await scope.RolloutRepository().GetAsync());
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Enforced, enforced.Mode);
        var activationEvidence = enforced.LastTransitionEvidenceHash;

        var suspend = Guid.NewGuid();
        await Runner(scope, Transition(scope, ProductLegalEntityScopeOperationalRunner.SuspendFailClosedAction, rollout.Id, 1, suspend, actor)).RunAsync();

        var suspended = Assert.IsType<ProductLegalEntityScopeRolloutState>(await scope.RolloutRepository().GetAsync());
        Assert.Equal(ProductLegalEntityScopeRolloutMode.FailClosedSuspended, suspended.Mode);
        Assert.Equal(2, suspended.Version);
        Assert.Null(suspended.ActiveFence);
        Assert.Equal(suspend, suspended.LastTransitionCommandId);
        Assert.Equal(suspended.LastInventorySnapshot!.SnapshotHash, suspended.LastTransitionEvidenceHash);
        Assert.NotEqual(activationEvidence, suspended.LastTransitionEvidenceHash);
        // One audit intent per transition, each carrying its own transition's evidence.
        Assert.Equal(
            [activationEvidence, suspended.LastTransitionEvidenceHash],
            suspended.AuditIntents.OrderBy(intent => intent.PostVersion).Select(intent => intent.EvidenceHash).ToArray());
        Assert.Equal(
            [ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated, ProductAuditOperation.ProductLegalEntityScopeEnforcementSuspended],
            suspended.AuditIntents.OrderBy(intent => intent.PostVersion).Select(intent => intent.Operation).ToArray());
    }

    [Fact]
    public async Task An_activation_with_a_product_without_scope_is_refused_before_any_fence_and_writes_stay_open()
    {
        // B3: before, the fence was taken first and stayed up when readiness failed — every product write, the policy
        // writes that would have made the tenant ready included, answered 409 for good.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var actor = Guid.NewGuid();
        var rollout = await BootstrapAsync(scope, actor);
        await InsertProductAsync(scope, Guid.NewGuid());
        try
        {
            var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => Runner(scope,
                Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, Guid.NewGuid(), actor)).RunAsync());

            Assert.Equal("PRODUCT_SCOPE_ACTIVATION_READINESS_NOT_SATISFIED", refused.Message);
            var after = Assert.IsType<ProductLegalEntityScopeRolloutState>(await scope.RolloutRepository().GetAsync());
            Assert.Null(after.ActiveFence);
            // Refused BEFORE any fence: none was ever taken (so none had to be abandoned) — not taken and then aborted.
            Assert.Null(after.LastFenceAbort);
            Assert.Equal(ProductLegalEntityScopeRolloutMode.Preparation, after.Mode);
            Assert.True((await scope.RolloutRepository().AcquireProductWriterLeaseAsync(Lease(Guid.NewGuid(), actor))).Acquired);
        }
        finally
        {
            await DeleteProductsAsync(scope);
        }
    }

    [Fact]
    public async Task An_activation_with_scope_audit_still_in_flight_is_refused_before_any_fence()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var actor = Guid.NewGuid();
        var rollout = await BootstrapAsync(scope, actor);
        var product = Guid.NewGuid();
        await InsertProductAsync(scope, product);
        try
        {
            // The product has its scope (nothing is missing) — but the policy's audit intent is not delivered yet.
            var now = DateTimeOffset.UtcNow;
            var policy = ProductLegalEntityScopePolicy.Create(
                scope.TenantId, product, Guid.NewGuid(), ProductLegalEntityScopeMode.GroupWide, [], actor, now);
            policy.AuditIntents.Add(ProductLegalEntityScopeAuditIntentFactory.Create(
                policy, ProductAuditOperation.ProductLegalEntityScopePolicyCreated, -1, 0, policy.CreationCommandId,
                actor, now, before: null, after: policy.GetEffectivePeriod(now)));
            Assert.True((await scope.PolicyRepository().CreateAsync(policy)).Succeeded);

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => Runner(scope,
                Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, Guid.NewGuid(), actor)).RunAsync());

            Assert.Equal("PRODUCT_SCOPE_ACTIVATION_READINESS_NOT_SATISFIED", refused.Message);
            Assert.Null((await scope.RolloutRepository().GetAsync())!.ActiveFence);
        }
        finally
        {
            await DeleteProductsAsync(scope);
        }
    }

    [Fact]
    public async Task A_transition_repeated_after_its_commit_answer_was_lost_finishes_over_its_own_bound_fence()
    {
        // B3: before, the repeat re-took its fence but binding accepted only a Closing fence — INVENTORY_DELTA forever.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var actor = Guid.NewGuid();
        var command = Guid.NewGuid();
        var rollout = await BootstrapAsync(scope, actor);
        var options = Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, command, actor);

        var lost = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runner(scope, options, decorate: inner => new CommitAnswerLost(inner)).RunAsync());
        Assert.Equal("PRODUCT_SCOPE_RECONCILIATION_REQUIRED", lost.Message);
        var fenced = Assert.IsType<ProductLegalEntityScopeRolloutState>(await scope.RolloutRepository().GetAsync());
        Assert.Equal(ProductLegalEntityScopeAdmissionState.Quiesced, fenced.ActiveFence!.State);
        Assert.NotNull(fenced.ActiveFence.PendingSnapshot);

        await Runner(scope, options).RunAsync();

        var enforced = Assert.IsType<ProductLegalEntityScopeRolloutState>(await scope.RolloutRepository().GetAsync());
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Enforced, enforced.Mode);
        Assert.Null(enforced.ActiveFence);
        Assert.Equal(fenced.ActiveFence.PendingSnapshot!.SnapshotHash, enforced.LastTransitionEvidenceHash);
    }

    [Fact]
    public async Task The_same_command_with_abort_lifts_its_fence_records_who_and_why_and_reopens_writes()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var actor = Guid.NewGuid();
        var command = Guid.NewGuid();
        var rollout = await BootstrapAsync(scope, actor);
        var options = Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, command, actor);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runner(scope, options, decorate: inner => new CommitAnswerLost(inner)).RunAsync());
        Assert.False((await scope.RolloutRepository().AcquireProductWriterLeaseAsync(Lease(Guid.NewGuid(), actor))).Acquired);

        // Another command cannot lift it.
        var foreign = Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, Guid.NewGuid(), actor);
        foreign.Abort = true;
        Assert.Equal("PRODUCT_SCOPE_ACTIVATION_FENCE_NOT_FOUND",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => Runner(scope, foreign).RunAsync())).Message);
        Assert.NotNull((await scope.RolloutRepository().GetAsync())!.ActiveFence);

        options.Abort = true;
        await Runner(scope, options).RunAsync();

        var reopened = Assert.IsType<ProductLegalEntityScopeRolloutState>(await scope.RolloutRepository().GetAsync());
        Assert.Null(reopened.ActiveFence);
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Preparation, reopened.Mode);
        Assert.Equal(command, reopened.LastFenceAbort!.CommandId);
        Assert.Equal(actor, reopened.LastFenceAbort.ActorId);
        Assert.Equal("OPERATOR_ABORT", reopened.LastFenceAbort.Cause);
        Assert.True((await scope.RolloutRepository().AcquireProductWriterLeaseAsync(Lease(Guid.NewGuid(), actor))).Acquired);
    }

    [Fact]
    public async Task An_inventory_that_moves_between_the_two_snapshots_abandons_the_fence_and_reopens_writes()
    {
        var actor = Guid.NewGuid();
        // A Preparation tenant cannot be suspended (its expected mode is Enforced): activate first, then let the suspension's
        // inventory move between its two snapshots.
        await using var second = await ProductScopeMongoScope.CreateAsync();
        var secondRollout = await BootstrapAsync(second, actor);
        await Runner(second, Transition(second, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, secondRollout.Id, 0, Guid.NewGuid(), actor)).RunAsync();
        try
        {
            var moved = await Assert.ThrowsAsync<InvalidOperationException>(() => Runner(second,
                Transition(second, ProductLegalEntityScopeOperationalRunner.SuspendFailClosedAction, secondRollout.Id, 1, Guid.NewGuid(), actor),
                decorateReadiness: inner => new MovesAfter(inner, second, captures: 1)).RunAsync());

            Assert.Equal("PRODUCT_SCOPE_INVENTORY_DELTA_DETECTED", moved.Message);
            var after = Assert.IsType<ProductLegalEntityScopeRolloutState>(await second.RolloutRepository().GetAsync());
            Assert.Null(after.ActiveFence);
            Assert.Equal(ProductLegalEntityScopeRolloutMode.Enforced, after.Mode);
            Assert.Equal("PRODUCT_SCOPE_INVENTORY_DELTA_DETECTED", after.LastFenceAbort!.Cause);
        }
        finally
        {
            await DeleteProductsAsync(second);
        }
    }

    // ── composition ─────────────────────────────────────────────────────────────────────────────────────────────

    private static ProductLegalEntityScopeOperationalRunner Runner(
        ProductScopeMongoScope scope,
        ProductLegalEntityScopeOperationalOptions options,
        string environment = "Development",
        Func<IProductLegalEntityScopeRolloutStateRepository, IProductLegalEntityScopeRolloutStateRepository>? decorate = null,
        Func<IProductLegalEntityScopeOperationalReadinessRepository, IProductLegalEntityScopeOperationalReadinessRepository>? decorateReadiness = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IProductLegalEntityScopeRolloutStateRepository>(provider =>
        {
            var inner = new ProductLegalEntityScopeRolloutStateRepository(scope.Database, provider.GetRequiredService<ITenantContext>());
            return decorate is null ? inner : decorate(inner);
        });
        services.AddScoped<IProductLegalEntityScopeOperationalReadinessRepository>(provider =>
        {
            var inner = new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, provider.GetRequiredService<ITenantContext>());
            return decorateReadiness is null ? inner : decorateReadiness(inner);
        });
        services.AddScoped<IGlobalProductRepository>(provider =>
            new GlobalProductRepository(scope.Database, provider.GetRequiredService<ITenantContext>()));
        var built = services.BuildServiceProvider();
        return new ProductLegalEntityScopeOperationalRunner(
            new HostEnvironment(environment), Options.Create(options), built.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
    }

    private static ProductLegalEntityScopeOperationalOptions Bootstrap(ProductScopeMongoScope scope, Guid actor, Guid command) => new()
    {
        Enabled = true, Action = ProductLegalEntityScopeOperationalRunner.BootstrapPreparationAction,
        TenantId = scope.TenantId, ActorId = actor, CommandId = command
    };

    private static ProductLegalEntityScopeOperationalOptions Transition(
        ProductScopeMongoScope scope, string action, Guid rolloutId, int expectedVersion, Guid command, Guid actor) => new()
    {
        Enabled = true, Action = action, TenantId = scope.TenantId, ActorId = actor, CommandId = command,
        ExpectedRolloutStateId = rolloutId, ExpectedRolloutVersion = expectedVersion, ReasonCode = "OWNER_APPROVED"
    };

    private static async Task<ProductLegalEntityScopeRolloutState> BootstrapAsync(ProductScopeMongoScope scope, Guid actor)
    {
        await Runner(scope, Bootstrap(scope, actor, Guid.NewGuid())).RunAsync();
        return Assert.IsType<ProductLegalEntityScopeRolloutState>(await scope.RolloutRepository().GetAsync());
    }

    private static ProductLegalEntityScopeWriterLease Lease(Guid product, Guid actor)
    {
        var now = DateTimeOffset.UtcNow;
        return new ProductLegalEntityScopeWriterLease
        {
            Token = Guid.NewGuid(), Generation = 1, CommandId = Guid.NewGuid(), ActorId = actor,
            MutationKind = "TestWrite", PayloadFingerprint = new string('A', 64), Owner = "Diten.MDM:TestWrite",
            AcquiredAtUtc = now, ExpiresAtUtc = now.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds),
            ObjectKeys = [$"GlobalProductId:{product:D}"]
        };
    }

    private static Task InsertProductAsync(ProductScopeMongoScope scope, Guid id) =>
        scope.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(new GlobalProduct
        {
            Id = id, TenantId = scope.TenantId, CanonicalCode = "GP-" + id.ToString("N")[..8].ToUpperInvariant(),
            GlobalProductName = "Runner test product", GlobalProductNameNormalized = "RUNNER TEST PRODUCT",
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow
        });

    private static Task DeleteProductsAsync(ProductScopeMongoScope scope) =>
        scope.Database.GetCollection<GlobalProduct>("mdm_global_products").DeleteManyAsync(item => item.TenantId == scope.TenantId);

    private sealed class HostEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Diten.MdmService.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>The commit's answer is lost: nothing reaches the store, the caller hears "ambiguous".</summary>
    private sealed class CommitAnswerLost(IProductLegalEntityScopeRolloutStateRepository inner)
        : DelegatingRolloutRepository(inner)
    {
        public override Task<ProductLegalEntityScopeRolloutStateWriteResult> CommitTransitionAsync(
            string fenceToken, ProductLegalEntityScopeRolloutState requested, int expectedVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProductLegalEntityScopeRolloutStateWriteResult(false, null, WriteOutcomeAmbiguous: true));
    }

    /// <summary>After <paramref name="captures"/> inventory snapshots, a product appears (the inventory moved).</summary>
    private sealed class MovesAfter(
        IProductLegalEntityScopeOperationalReadinessRepository inner, ProductScopeMongoScope scope, int captures)
        : IProductLegalEntityScopeOperationalReadinessRepository
    {
        private int _taken;

        public Task<ProductLegalEntityScopeOperationalReadiness> InspectAsync(int maximumSampleSize, CancellationToken cancellationToken = default)
            => inner.InspectAsync(maximumSampleSize, cancellationToken);

        public Task<ProductLegalEntityScopeObjectState> CaptureObjectStateHashAsync(IReadOnlyCollection<Guid> objectIds, CancellationToken cancellationToken = default)
            => inner.CaptureObjectStateHashAsync(objectIds, cancellationToken);

        public async Task<ProductLegalEntityScopeInventorySnapshot> CaptureInventorySnapshotAsync(
            ProductLegalEntityScopeInventorySnapshotRequest request, CancellationToken cancellationToken = default)
        {
            var snapshot = await inner.CaptureInventorySnapshotAsync(request, cancellationToken);
            if (++_taken == captures) await InsertProductAsync(scope, Guid.NewGuid());
            return snapshot;
        }
    }
}

/// <summary>Passes every call to the real rollout repository; a test overrides the one it bends.</summary>
internal class DelegatingRolloutRepository(IProductLegalEntityScopeRolloutStateRepository inner)
    : IProductLegalEntityScopeRolloutStateRepository
{
    public virtual Task<ProductLegalEntityScopeRolloutState?> GetAsync(CancellationToken cancellationToken = default) => inner.GetAsync(cancellationToken);
    public virtual Task<ProductLegalEntityScopeRolloutState?> GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default) => inner.GetByCreationCommandIdAsync(creationCommandId, cancellationToken);
    public virtual Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(ProductLegalEntityScopeRolloutState state, CancellationToken cancellationToken = default) => inner.CreateAsync(state, cancellationToken);
    public virtual Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(ProductLegalEntityScopeRolloutState state, int expectedVersion, CancellationToken cancellationToken = default) => inner.UpdateAsync(state, expectedVersion, cancellationToken);
    public virtual Task<ProductLegalEntityScopeWriterLeaseResult> AcquireWriterLeaseAsync(ProductLegalEntityScopeWriterLease requested, CancellationToken cancellationToken = default) => inner.AcquireWriterLeaseAsync(requested, cancellationToken);
    public virtual Task<bool> BindWriterLeaseBaselineAsync(Guid token, long generation, string preWriteStateHash, CancellationToken cancellationToken = default) => inner.BindWriterLeaseBaselineAsync(token, generation, preWriteStateHash, cancellationToken);
    public virtual Task<bool> ReleaseWriterLeaseAsync(Guid token, long generation, CancellationToken cancellationToken = default) => inner.ReleaseWriterLeaseAsync(token, generation, cancellationToken);
    public virtual Task<ProductLegalEntityScopeWriterLeaseResult> AcquireProductWriterLeaseAsync(ProductLegalEntityScopeWriterLease requested, CancellationToken cancellationToken = default) => inner.AcquireProductWriterLeaseAsync(requested, cancellationToken);
    public virtual Task<bool> BindProductWriterLeaseBaselineAsync(Guid token, long generation, string preWriteStateHash, CancellationToken cancellationToken = default) => inner.BindProductWriterLeaseBaselineAsync(token, generation, preWriteStateHash, cancellationToken);
    public virtual Task<bool> ReleaseProductWriterLeaseAsync(Guid token, long generation, CancellationToken cancellationToken = default) => inner.ReleaseProductWriterLeaseAsync(token, generation, cancellationToken);
    public virtual Task<bool> AbortFenceAsync(ProductLegalEntityScopeFenceAbort abort, CancellationToken cancellationToken = default) => inner.AbortFenceAsync(abort, cancellationToken);
    public virtual Task<ProductLegalEntityScopeFenceResult> AcquireFenceAsync(ProductLegalEntityScopeActivationFence requested, Guid expectedRolloutStateId, int expectedVersion, ProductLegalEntityScopeRolloutMode expectedMode, CancellationToken cancellationToken = default) => inner.AcquireFenceAsync(requested, expectedRolloutStateId, expectedVersion, expectedMode, cancellationToken);
    public virtual Task<bool> BindFenceSnapshotsAsync(string fenceToken, ProductLegalEntityScopeInventorySnapshot first, ProductLegalEntityScopeInventorySnapshot second, CancellationToken cancellationToken = default) => inner.BindFenceSnapshotsAsync(fenceToken, first, second, cancellationToken);
    public virtual Task<ProductLegalEntityScopeRolloutStateWriteResult> CommitTransitionAsync(string fenceToken, ProductLegalEntityScopeRolloutState requested, int expectedVersion, CancellationToken cancellationToken = default) => inner.CommitTransitionAsync(fenceToken, requested, expectedVersion, cancellationToken);
}
