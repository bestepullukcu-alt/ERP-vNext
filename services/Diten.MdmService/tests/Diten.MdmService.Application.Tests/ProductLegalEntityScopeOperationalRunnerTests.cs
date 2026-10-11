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
    // BL-595 FIX2: the shared helpers follow this class's own replica set, in-process (never the environment).
    private readonly IDisposable _ownedRedirect;

    public ProductLegalEntityScopeOperationalRunnerTests(AuditIntentTemporalMongoFixture fixture)
    {
        _ownedRedirect = MdmTestMongo.UseOwnedRedirect(fixture.ReplicaConnectionString);
    }

    public void Dispose() => _ownedRedirect.Dispose();

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

    // ── FIX2 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Activation_is_refused_while_a_named_debt_is_open_even_in_development()
    {
        // FIX2 (5) — CT decision: unguarded writers / unfenced recovery runners keep Enforced shut, in every environment.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var actor = Guid.NewGuid();
        var rollout = await BootstrapAsync(scope, actor);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => Runner(scope,
            Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, Guid.NewGuid(), actor),
            debt: ProductLegalEntityScopeActivationDebt.Instance).RunAsync());

        Assert.Equal("PRODUCT_SCOPE_ACTIVATION_BLOCKED_UNGUARDED_WRITERS", refused.Message);
        var after = Assert.IsType<ProductLegalEntityScopeRolloutState>(await scope.RolloutRepository().GetAsync());
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Preparation, after.Mode);
        Assert.Null(after.ActiveFence);
        Assert.Null(after.LastFenceAbort);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Either_debt_list_alone_keeps_activation_shut(bool writers, bool runners)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var actor = Guid.NewGuid();
        var rollout = await BootstrapAsync(scope, actor);
        var debt = new Debt(writers ? ["SomeUnguardedCommand"] : [], runners ? ["SomeUnfencedRecoveryRunner"] : []);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => Runner(scope,
            Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, Guid.NewGuid(), actor),
            debt: debt).RunAsync());

        Assert.Equal(ProductLegalEntityScopeActivationDebt.ActivationBlockedCode, refused.Message);
    }

    [Fact]
    public async Task With_both_debt_lists_empty_activation_goes_through()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var actor = Guid.NewGuid();
        var rollout = await BootstrapAsync(scope, actor);

        await Runner(scope, Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, Guid.NewGuid(), actor),
            debt: new Debt([], [])).RunAsync();

        Assert.Equal(ProductLegalEntityScopeRolloutMode.Enforced, (await scope.RolloutRepository().GetAsync())!.Mode);
    }

    [Fact]
    public async Task A_runner_composed_without_a_debt_registration_still_refuses_activation()
    {
        // Fail-closed: a composition that forgot the registration falls back to the production lists, never to "no debt".
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var actor = Guid.NewGuid();
        var rollout = await BootstrapAsync(scope, actor);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => Runner(scope,
            Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, Guid.NewGuid(), actor),
            registerDebt: false).RunAsync());

        Assert.Equal(ProductLegalEntityScopeActivationDebt.ActivationBlockedCode, refused.Message);
    }

    [Fact]
    public async Task Suspension_stays_open_while_the_debt_is_open()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var actor = Guid.NewGuid();
        var rollout = await BootstrapAsync(scope, actor);
        await Runner(scope, Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, Guid.NewGuid(), actor)).RunAsync();

        await Runner(scope, Transition(scope, ProductLegalEntityScopeOperationalRunner.SuspendFailClosedAction, rollout.Id, 1, Guid.NewGuid(), actor),
            debt: ProductLegalEntityScopeActivationDebt.Instance).RunAsync();

        Assert.Equal(ProductLegalEntityScopeRolloutMode.FailClosedSuspended, (await scope.RolloutRepository().GetAsync())!.Mode);
    }

    [Fact]
    public async Task Two_runs_of_the_same_transition_at_once_never_let_the_loser_abandon_the_winners_fence()
    {
        // FIX2 (12): before, the fence token was the same for every attempt of a command. Run A took the fence, run B
        // adopted it and bound it; A then failed its own bind and aborted "INVENTORY_DELTA" under the shared token —
        // lifting B's fence, and B's commit failed. Now B takes the fence over under its own token; A's abort is a no-op.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var actor = Guid.NewGuid();
        var rollout = await BootstrapAsync(scope, actor);
        var options = Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, Guid.NewGuid(), actor);
        var atBind = new Pause();
        var atCommit = new Pause();

        var a = Task.Run(() => Runner(scope, options, decorate: inner => new PauseAt(inner, bind: atBind)).RunAsync());
        await atBind.Reached.Task.WaitAsync(TimeSpan.FromSeconds(60));
        var b = Task.Run(() => Runner(scope, options, decorate: inner => new PauseAt(inner, commit: atCommit)).RunAsync());
        await atCommit.Reached.Task.WaitAsync(TimeSpan.FromSeconds(60));

        atBind.Go.SetResult();
        var loser = await Assert.ThrowsAsync<InvalidOperationException>(() => a.WaitAsync(TimeSpan.FromSeconds(60)));
        atCommit.Go.SetResult();
        await b.WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal("PRODUCT_SCOPE_INVENTORY_DELTA_DETECTED", loser.Message);
        var after = Assert.IsType<ProductLegalEntityScopeRolloutState>(await scope.RolloutRepository().GetAsync());
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Enforced, after.Mode);
        Assert.Null(after.ActiveFence);
        Assert.Null(after.LastFenceAbort);
    }

    [Fact]
    public async Task The_expired_leases_an_activation_fence_clears_are_counted()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var actor = Guid.NewGuid();
        var rollout = await BootstrapAsync(scope, actor);
        var stale = Lease(Guid.NewGuid(), actor);
        stale.AcquiredAtUtc = stale.AcquiredAtUtc.AddMinutes(-10);
        stale.ExpiresAtUtc = stale.AcquiredAtUtc.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds);
        await scope.RolloutCollection.UpdateOneAsync(item => item.TenantId == scope.TenantId,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Push(item => item.ProductWriterLeases, stale));

        await Runner(scope, Transition(scope, ProductLegalEntityScopeOperationalRunner.ActivateEnforcedAction, rollout.Id, 0, Guid.NewGuid(), actor)).RunAsync();

        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseCleared));
    }

    // ── composition ─────────────────────────────────────────────────────────────────────────────────────────────

    private static ProductLegalEntityScopeOperationalRunner Runner(
        ProductScopeMongoScope scope,
        ProductLegalEntityScopeOperationalOptions options,
        string environment = "Development",
        Func<IProductLegalEntityScopeRolloutStateRepository, IProductLegalEntityScopeRolloutStateRepository>? decorate = null,
        Func<IProductLegalEntityScopeOperationalReadinessRepository, IProductLegalEntityScopeOperationalReadinessRepository>? decorateReadiness = null,
        IProductLegalEntityScopeActivationDebt? debt = null,
        bool registerDebt = true)
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext, TenantContext>();
        // FIX2 (5): these tests exercise the transition itself — the debt gate is opened with a test double (both lists
        // empty) unless a test asks for the production lists or for no registration at all.
        if (registerDebt) services.AddSingleton(debt ?? new Debt([], []));
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

    private sealed class Debt(IReadOnlyCollection<string> writers, IReadOnlyCollection<string> runners) : IProductLegalEntityScopeActivationDebt
    {
        public IReadOnlyCollection<string> UnguardedWriters { get; } = writers;
        public IReadOnlyCollection<string> UnfencedRecoveryRunners { get; } = runners;
    }

    private sealed class Pause
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Go { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Holds a run just before it binds its snapshots, or just before it commits, until the test lets it go.</summary>
    private sealed class PauseAt(IProductLegalEntityScopeRolloutStateRepository inner, Pause? bind = null, Pause? commit = null)
        : DelegatingRolloutRepository(inner)
    {
        public override async Task<bool> BindFenceSnapshotsAsync(string fenceToken, ProductLegalEntityScopeInventorySnapshot first,
            ProductLegalEntityScopeInventorySnapshot second, CancellationToken cancellationToken = default)
        {
            if (bind is not null) { bind.Reached.TrySetResult(); await bind.Go.Task; }
            return await base.BindFenceSnapshotsAsync(fenceToken, first, second, cancellationToken);
        }

        public override async Task<ProductLegalEntityScopeRolloutStateWriteResult> CommitTransitionAsync(string fenceToken,
            ProductLegalEntityScopeRolloutState requested, int expectedVersion, CancellationToken cancellationToken = default)
        {
            if (commit is not null) { commit.Reached.TrySetResult(); await commit.Go.Task; }
            return await base.CommitTransitionAsync(fenceToken, requested, expectedVersion, cancellationToken);
        }
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
