using System.Security.Claims;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Infrastructure.Security;
using Diten.MdmService.Persistence.Repositories;
using Diten.MdmService.Application.Tests.Audit;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class ProductLegalEntityScopeWriteAdmissionMongoTests
    : IClassFixture<AuditIntentTemporalMongoFixture>, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
    // BL-595 FIX2: the shared helpers follow this class's own replica set, in-process (never the environment).
    private readonly IDisposable _ownedRedirect;

    public ProductLegalEntityScopeWriteAdmissionMongoTests(AuditIntentTemporalMongoFixture fixture)
    {
        _ownedRedirect = MdmTestMongo.UseOwnedRedirect(fixture.ReplicaConnectionString);
    }

    public void Dispose() =>
        _ownedRedirect.Dispose();

    [Fact]
    public async Task A_product_writer_elsewhere_in_the_tenant_does_not_invalidate_a_running_Replace()
    {
        // WP-MDM-SCOPE-ACCEPT-01 FIX2 (1): before, a product lease moved the shared WriterLeaseGeneration that the guarded
        // Replace conditions on — the Replace was rejected (409 PRODUCT_SCOPE_CONFLICT), its slot kept for 120 s, uncounted,
        // and every Replace of the tenant refused meanwhile. Product leases now number their generations on their own.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var otherProduct = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var writer = await scope.RolloutRepository().AcquireProductWriterLeaseAsync(new ProductLegalEntityScopeWriterLease
        {
            Token = Guid.NewGuid(), Generation = 1, CommandId = Guid.NewGuid(), ActorId = Guid.NewGuid(),
            MutationKind = "CreateGskuDraft", PayloadFingerprint = new string('C', 64), Owner = "Diten.MDM:CreateGskuDraft",
            AcquiredAtUtc = now, ExpiresAtUtc = now.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds),
            ObjectKeys = [$"GlobalProductId:{otherProduct:D}"]
        });
        Assert.True(writer.Acquired, writer.FailureCode);

        var result = await scenario.Repository.ReplaceAsync(scenario.Authority, scenario.Lease, scenario.Requested, 0);

        Assert.True(result.Succeeded,
            $"guarded result conflict={result.VersionConflict}, retain={result.LeaseRetentionRequired}, ambiguous={result.WriteOutcomeAmbiguous}");
    }

    [Theory]
    [InlineData("replace_ambiguous")]
    [InlineData("replace_rejected")]
    [InlineData("replace_failed")]
    public async Task A_replace_that_keeps_its_slot_says_why(string reason)
    {
        // FIX3 (2): every path that keeps the foreground slot is counted under its own name.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        ProductLegalEntityScopePolicyWriteResult result = reason switch
        {
            "replace_ambiguous" => new(false, null, WriteOutcomeAmbiguous: true),
            "replace_rejected" => new(false, null, VersionConflict: true, VerifiedZeroMutation: true, LeaseRetentionRequired: true),
            _ => new(false, null)
        };

        await CompleteForegroundReplaceAsync(scope, scenario, result);

        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseRetained, "reason", reason));
        Assert.Equal(scenario.Lease.Token, (await scope.RolloutRepository().GetAsync())!.ActiveWriterLease?.Token);
    }

    [Fact]
    public async Task A_replace_whose_slot_vanished_while_it_wrote_says_the_release_was_refused()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        await scope.RolloutCollection.UpdateOneAsync(item => item.TenantId == scope.TenantId,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(item => item.ActiveWriterLease, null));

        await CompleteForegroundReplaceAsync(scope, scenario, new(true, scenario.Requested));

        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseReleaseRefused, "by", "replace"));
    }

    [Theory]
    [InlineData("replace_authority_mismatch")]
    [InlineData("replace_baseline_conflict")]
    public async Task A_replace_refused_after_its_slot_was_taken_says_why(string reason)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var tenant = new TenantContext();
        tenant.SetTenant(scope.TenantId);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(
            new BentSlot(scope.RolloutRepository(), reason),
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(scenario.Lease.ActorId.ToString("D")), tenant, TimeProvider.System);

        var admission = await coordinator.EnterForegroundReplaceAsync(scenario.Authority, scenario.Command, CancellationToken.None);

        Assert.False(admission.IsAllowed);
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseRetained, "reason", reason));
    }

    /// <summary>The slot comes back bent: another command's (authority mismatch), or unbound with a refused binding.</summary>
    private sealed class BentSlot(IProductLegalEntityScopeRolloutStateRepository inner, string reason) : DelegatingRolloutRepository(inner)
    {
        public override async Task<ProductLegalEntityScopeWriterLeaseResult> AcquireWriterLeaseAsync(
            ProductLegalEntityScopeWriterLease requested, CancellationToken cancellationToken = default)
        {
            var result = await base.AcquireWriterLeaseAsync(requested, cancellationToken);
            var lease = CopyLease(result.Lease!);
            if (reason == "replace_authority_mismatch") lease.CommandId = Guid.NewGuid();
            else lease.PreWriteStateHash = null;
            return result with { Lease = lease };
        }

        public override Task<bool> BindWriterLeaseBaselineAsync(Guid token, long generation, string preWriteStateHash,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    [Fact]
    public async Task A_replace_aborted_as_a_transient_write_conflict_is_run_again_not_answered_409()
    {
        // FIX3 (11) / FIX4 (4): a REAL write conflict, deterministic — another writer changes the rollout record inside
        // the Replace's transaction snapshot (its first attempt only). The server aborts it as transient; it runs again.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var tenant = new TenantContext();
        tenant.SetTenant(scope.TenantId);
        var conflicts = 0;
        var session = new ProductLegalEntityScopeGuardedWriteSession(scope.Database, tenant);
        session.AfterRolloutRead = async () =>
        {
            if (conflicts++ == 0) Assert.True((await scope.RolloutRepository().AcquireProductWriterLeaseAsync(OtherProductLease())).Acquired);
        };

        var result = await session.ReplaceAsync(scenario.Authority, scenario.Lease, scenario.Requested, 0);

        Assert.True(result.Succeeded, $"conflict={result.VersionConflict}, ambiguous={result.WriteOutcomeAmbiguous}, exhausted={result.TransientRetriesExhausted}");
        Assert.True(conflicts >= 2);
    }

    [Fact]
    public async Task A_replace_whose_commit_landed_but_answered_a_transient_error_is_not_run_again()
    {
        // FIX4 (4a): before, the transient label was retried even after the commit was sent — the re-run found its own
        // write (version moved) and answered 409 although the policy HAD changed.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var tenant = new TenantContext();
        tenant.SetTenant(scope.TenantId);
        var commits = 0;
        var session = new ProductLegalEntityScopeGuardedWriteSession(scope.Database, tenant, async (handle, cancellationToken) =>
        {
            commits++;
            await handle.CommitTransactionAsync(cancellationToken);
            var lost = new MongoException("commit landed, answer lost");
            lost.AddErrorLabel("TransientTransactionError");
            throw lost;
        });

        var result = await session.ReplaceAsync(scenario.Authority, scenario.Lease, scenario.Requested, 0);

        Assert.True(result.Succeeded, $"conflict={result.VersionConflict}, ambiguous={result.WriteOutcomeAmbiguous}");
        Assert.Equal(1, commits);
        var persisted = await scope.PolicyRepository().GetByGlobalProductIdAsync(scenario.Initial.GlobalProductId);
        Assert.Equal(1, persisted!.Version);
    }

    [Fact]
    public async Task A_replace_whose_commit_answer_is_a_transient_error_and_did_not_land_is_ambiguous_not_409()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var tenant = new TenantContext();
        tenant.SetTenant(scope.TenantId);
        var session = new ProductLegalEntityScopeGuardedWriteSession(scope.Database, tenant, (_, _) =>
        {
            var lost = new MongoException("commit refused");
            lost.AddErrorLabel("TransientTransactionError");
            throw lost;
        });

        var result = await session.ReplaceAsync(scenario.Authority, scenario.Lease, scenario.Requested, 0);

        Assert.False(result.Succeeded);
        Assert.True(result.WriteOutcomeAmbiguous);
    }

    private static ProductLegalEntityScopeWriterLease OtherProductLease()
    {
        var now = DateTimeOffset.UtcNow;
        return new ProductLegalEntityScopeWriterLease
        {
            Token = Guid.NewGuid(), Generation = 1, CommandId = Guid.NewGuid(), ActorId = Guid.NewGuid(),
            MutationKind = "Other", PayloadFingerprint = new string('F', 64), Owner = "Diten.MDM:Other",
            AcquiredAtUtc = now, ExpiresAtUtc = now.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds),
            ObjectKeys = [$"GlobalProductId:{Guid.NewGuid():D}"]
        };
    }

    // ── FIX4: the Replace handler, real, end to end ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("capture", "replace_admission_exception")]
    [InlineData("bind", "replace_admission_exception")]
    [InlineData("write", "replace_exception")]
    public async Task A_replace_that_fails_after_taking_its_slot_keeps_it_and_says_why(string where, string reason)
    {
        // FIX4 (1): before, these exits left the tenant-wide slot held ~125 s without a trace.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var world = await ReplaceWorld.CreateAsync(scope);

        await Assert.ThrowsAsync<TimeoutException>(() => world.RunAsync(
            rollouts: where == "bind" ? new BindThrows(world.Rollouts) : null,
            readiness: where == "capture" ? new ThrowingReadiness() : null,
            policies: where == "write" ? new ReplaceThrows(world.Policies, new TimeoutException("store lost")) : null));

        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseRetained, "reason", reason));
        Assert.NotNull((await world.Rollouts.GetAsync())!.ActiveWriterLease);
    }

    [Fact]
    public async Task A_replace_refused_by_the_bson_budget_before_any_write_releases_its_slot()
    {
        // FIX4 (1): nothing was written (the budget is checked on the projection) — the slot is released, principle A.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var world = await ReplaceWorld.CreateAsync(scope);

        var response = await world.RunAsync(policies: new ReplaceThrows(world.Policies,
            new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_BSON_LIMIT_EXCEEDED")));

        Assert.Equal((409, "PRODUCT_SCOPE_BSON_LIMIT_EXCEEDED"), (response.StatusCode, response.Errors?.FirstOrDefault()));
        Assert.Null((await world.Rollouts.GetAsync())!.ActiveWriterLease);
    }

    [Fact]
    public async Task A_replace_whose_slot_lost_every_round_answers_the_coordinators_503()
    {
        // FIX4 (5): before, the handler re-wrapped every refusal as 409.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var world = await ReplaceWorld.CreateAsync(scope);
        var losing = new ProductLegalEntityScopeRolloutStateRepository(scope.Database, world.Tenant) { LeaseAttempts = 1 };
        losing.BeforeLeaseWrite = async _ => Assert.True((await scope.RolloutRepository().AcquireProductWriterLeaseAsync(OtherProductLease())).Acquired);

        var response = await world.RunAsync(rollouts: losing);

        Assert.Equal((503, "PRODUCT_SCOPE_WRITER_LEASE_CONTENDED"), (response.StatusCode, response.Errors?.FirstOrDefault()));
    }

    [Fact]
    public async Task A_replace_still_aborted_as_transient_after_every_retry_answers_a_named_503_and_frees_its_slot()
    {
        // FIX4 (4b): before, the exhausted retries fell into the generic refusal: an uncounted 409 PRODUCT_SCOPE_CONFLICT.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var world = await ReplaceWorld.CreateAsync(scope);
        world.Session.AfterRolloutRead = async () =>
            Assert.True((await scope.RolloutRepository().AcquireProductWriterLeaseAsync(OtherProductLease())).Acquired);

        var response = await world.RunAsync();

        Assert.Equal((503, "PRODUCT_SCOPE_REPLACE_CONTENDED"), (response.StatusCode, response.Errors?.FirstOrDefault()));
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.ReplaceTransientExhausted));
        Assert.Null((await world.Rollouts.GetAsync())!.ActiveWriterLease);
        Assert.Equal(0, (await world.Policies.GetByGlobalProductIdAsync(world.ProductId))!.Version);
    }

    [Fact]
    public async Task A_replace_through_the_handler_succeeds_and_frees_its_slot()
    {
        // The baseline of the handler world: the happy path releases the slot.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var world = await ReplaceWorld.CreateAsync(scope);

        var response = await world.RunAsync();

        Assert.True(response.IsSuccessful, string.Join(",", response.Errors ?? []));
        Assert.Null((await world.Rollouts.GetAsync())!.ActiveWriterLease);
    }

    private sealed class ReplaceWorld
    {
        private ReplaceWorld(ProductScopeMongoScope scope, Guid productId)
        {
            Scope = scope;
            ProductId = productId;
            Tenant = new TenantContext();
            Tenant.SetTenant(scope.TenantId);
            Rollouts = scope.RolloutRepository();
            Session = new ProductLegalEntityScopeGuardedWriteSession(scope.Database, Tenant);
            Policies = new ProductLegalEntityScopePolicyRepository(scope.Database, Tenant, Session);
        }

        public ProductScopeMongoScope Scope { get; }
        public Guid ProductId { get; }
        public TenantContext Tenant { get; }
        public Guid Subject { get; } = Guid.NewGuid();
        public ProductLegalEntityScopeRolloutStateRepository Rollouts { get; }
        public ProductLegalEntityScopeGuardedWriteSession Session { get; }
        public ProductLegalEntityScopePolicyRepository Policies { get; }

        public static async Task<ReplaceWorld> CreateAsync(ProductScopeMongoScope scope)
        {
            var productId = Guid.NewGuid();
            Assert.True((await scope.PolicyRepository().CreateAsync(
                ProductLegalEntityScopeMongoTests.CreatePolicy(scope.TenantId, productId, Guid.NewGuid()))).Succeeded);
            Assert.True((await scope.RolloutRepository().CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
                scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now))).Succeeded);
            return new ReplaceWorld(scope, productId);
        }

        public Task<Diten.Shared.Core.Response<ProductLegalEntityScopeModels.PolicyDto>> RunAsync(
            IProductLegalEntityScopeRolloutStateRepository? rollouts = null,
            IProductLegalEntityScopeOperationalReadinessRepository? readiness = null,
            IProductLegalEntityScopePolicyRepository? policies = null)
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("actor_type", "tenant_user"),
                    new Claim("tenant_id", Scope.TenantId.ToString("D")),
                    new Claim("sub", Subject.ToString("D")),
                    new Claim("permission", "mdm.product-legal-entity-scopes.replace")
                ], "test"))
            };
            context.Request.Headers["X-Tenant-Id"] = Scope.TenantId.ToString("D");
            var rolloutRepository = rollouts ?? Rollouts;
            var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(rolloutRepository,
                readiness ?? new ProductLegalEntityScopeOperationalReadinessRepository(Scope.Database, Tenant),
                new Actor(Subject.ToString("D")), Tenant, TimeProvider.System);
            var handler = new Features.ProductLegalEntityScopes.Handlers.CommandHandlers.ReplaceProductLegalEntityScopePolicyHandler(
                policies ?? Policies, rolloutRepository, null!, null!,
                new ProductLegalEntityScopeWriterAuthorityProvider(new HttpContextAccessor { HttpContext = context }, Tenant),
                coordinator);
            return handler.Handle(new ReplaceProductLegalEntityScopePolicyCommand(ProductId, Guid.NewGuid(),
                new() { ExpectedVersion = 0, Mode = ProductLegalEntityScopeMode.GroupWide }), CancellationToken.None);
        }
    }

    private sealed class BindThrows(IProductLegalEntityScopeRolloutStateRepository inner) : DelegatingRolloutRepository(inner)
    {
        public override Task<bool> BindWriterLeaseBaselineAsync(Guid token, long generation, string preWriteStateHash,
            CancellationToken cancellationToken = default) => throw new TimeoutException("store lost");
    }

    private sealed class ThrowingReadiness : IProductLegalEntityScopeOperationalReadinessRepository
    {
        public Task<ProductLegalEntityScopeOperationalReadiness> InspectAsync(int maximumSampleSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopeInventorySnapshot> CaptureInventorySnapshotAsync(ProductLegalEntityScopeInventorySnapshotRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopeObjectState> CaptureObjectStateHashAsync(IReadOnlyCollection<Guid> objectIds, CancellationToken cancellationToken = default)
            => throw new TimeoutException("store lost");
    }

    private sealed class ReplaceThrows(IProductLegalEntityScopePolicyRepository inner, Exception failure) : IProductLegalEntityScopePolicyRepository
    {
        public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(Guid globalProductId, CancellationToken cancellationToken = default) => inner.GetByGlobalProductIdAsync(globalProductId, cancellationToken);
        public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default) => inner.GetByCreationCommandIdAsync(creationCommandId, cancellationToken);
        public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(ProductLegalEntityScopePolicy policy, CancellationToken cancellationToken = default) => inner.CreateAsync(policy, cancellationToken);
        public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(ProductLegalEntityScopePolicy policy, int expectedVersion, CancellationToken cancellationToken = default) => inner.UpdateAsync(policy, expectedVersion, cancellationToken);
        public Task<ProductLegalEntityScopePolicyWriteResult> ReplaceAsync(ProductLegalEntityScopeVerifiedWriterAuthority authority, ProductLegalEntityScopeWriterLease lease,
            ProductLegalEntityScopePolicy requestedPolicy, int expectedVersion, CancellationToken cancellationToken = default) => throw failure;
        public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(IReadOnlyCollection<Guid> globalProductIds, CancellationToken cancellationToken = default) => inner.GetConfiguredGlobalProductIdsAsync(globalProductIds, cancellationToken);
    }

    // BL-602 (WP-CT-LANE-DEBT-01) — THE LEASE TRAFFIC IS STAGED, NOT RACED. This test used to run an unbounded loop acquiring
    // leases for other products while one Replace ran, and expect success. Measured (b7b53e01c + exhaustion flags in the
    // message, ct-heavy, own CPU load): idle 10/10 green; under load 18/20 green, both reds `exhausted=True` (conflict=False,
    // retain=False) — the loop rewrote the tenant's rollout record faster than the five transient attempts could commit,
    // and the production rule answered exactly as designed (FIX4 4b: nothing written, named, counted, 503). Not a starvation
    // of real traffic (a product writer's lease is one command, not a tight loop), so the production budget is unchanged;
    // the two outcomes are pinned deterministically instead, with the same hook as
    // A_replace_aborted_as_a_transient_write_conflict_is_run_again_not_answered_409: another product's lease is acquired
    // INSIDE the Replace's transaction snapshot, a real WriteConflict on the rollout record.
    [Fact]
    public async Task A_replace_under_constant_lease_traffic_on_other_products_succeeds()
    {
        // Every attempt but the last meets another product's lease: the Replace still lands, on its last attempt.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var tenant = new TenantContext();
        tenant.SetTenant(scope.TenantId);
        var attempts = 0;
        var session = new ProductLegalEntityScopeGuardedWriteSession(scope.Database, tenant);
        session.AfterRolloutRead = async () =>
        {
            if (++attempts < ProductLegalEntityScopeGuardedWriteSession.MaximumTransientAttempts)
                Assert.True((await scope.RolloutRepository().AcquireProductWriterLeaseAsync(OtherProductLease())).Acquired);
        };

        var result = await session.ReplaceAsync(scenario.Authority, scenario.Lease, scenario.Requested, 0);

        Assert.True(result.Succeeded, $"conflict={result.VersionConflict}, retain={result.LeaseRetentionRequired}, exhausted={result.TransientRetriesExhausted}, ambiguous={result.WriteOutcomeAmbiguous}");
        Assert.Equal(ProductLegalEntityScopeGuardedWriteSession.MaximumTransientAttempts, attempts);
        Assert.Equal(1, (await scope.PolicyRepository().GetByGlobalProductIdAsync(scenario.Initial.GlobalProductId))!.Version);
    }

    [Fact]
    public async Task A_replace_meeting_other_products_lease_traffic_on_every_attempt_writes_nothing_and_says_exhausted()
    {
        // The other outcome of the same traffic (BL-602's two measured reds): every attempt conflicts, the budget is used
        // up — nothing written, the named "exhausted" answer (503 upstream), never a 409 conflict, never a kept slot.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var tenant = new TenantContext();
        tenant.SetTenant(scope.TenantId);
        var attempts = 0;
        var session = new ProductLegalEntityScopeGuardedWriteSession(scope.Database, tenant);
        session.AfterRolloutRead = async () =>
        {
            attempts++;
            Assert.True((await scope.RolloutRepository().AcquireProductWriterLeaseAsync(OtherProductLease())).Acquired);
        };

        var result = await session.ReplaceAsync(scenario.Authority, scenario.Lease, scenario.Requested, 0);

        Assert.False(result.Succeeded);
        Assert.True(result.TransientRetriesExhausted && result.VerifiedZeroMutation, $"exhausted={result.TransientRetriesExhausted}, zero={result.VerifiedZeroMutation}");
        Assert.False(result.VersionConflict);
        Assert.False(result.LeaseRetentionRequired);
        Assert.Equal(ProductLegalEntityScopeGuardedWriteSession.MaximumTransientAttempts, attempts);
        Assert.Equal(0, (await scope.PolicyRepository().GetByGlobalProductIdAsync(scenario.Initial.GlobalProductId))!.Version);
    }

    [Fact]
    public async Task GuardedWrite_ExactHumanLeaseAndPreparation_AtomicallyReplacesAndPreservesUnknownBson()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var rawPolicies = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_policies");
        await rawPolicies.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", scenario.Initial.Id),
            Builders<BsonDocument>.Update.Set("FutureA0Field", "preserve"));
        var result = await scenario.Repository.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.True(
            result.Succeeded,
            $"guarded result conflict={result.VersionConflict}, ambiguous={result.WriteOutcomeAmbiguous}, zero={result.VerifiedZeroMutation}");
        Assert.False(result.WriteOutcomeAmbiguous);
        Assert.Equal(1, result.Policy!.Version);
        Assert.Equal(2, result.Policy.ScopePeriods.Count);
        Assert.Equal(2, result.Policy.AuditIntents.Count);
        var persisted = await rawPolicies.Find(
            Builders<BsonDocument>.Filter.Eq("_id", scenario.Initial.Id)).SingleAsync();
        Assert.Equal("preserve", persisted["FutureA0Field"].AsString);
        Assert.Equal(1, persisted[nameof(ProductLegalEntityScopePolicy.Version)].AsInt32);
        Assert.Equal(2, persisted[nameof(ProductLegalEntityScopePolicy.AuditIntents)].AsBsonArray.Count);
        var rollout = await scope.RolloutRepository().GetAsync();
        Assert.NotNull(rollout!.ActiveWriterLease);
        Assert.True(await scope.RolloutRepository().ReleaseWriterLeaseAsync(
            scenario.Lease.Token,
            scenario.Lease.Generation));
    }

    [Fact]
    public async Task GuardedWrite_CommitResponseLostAfterRealCommit_RecoversExactReadbackAndReplayCannotDuplicate()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        await SetLeaseWindowAsync(
            scope,
            scenario.Lease,
            DateTimeOffset.UtcNow.AddSeconds(-119));
        var commitAttempts = 0;
        var guarded = new ProductLegalEntityScopeGuardedWriteSession(
            scope.Database,
            new ProductScopeMongoScope.TenantContext(scope.TenantId),
            async (session, cancellationToken) =>
            {
                commitAttempts++;
                await session.CommitTransactionAsync(cancellationToken);
                var remaining = scenario.Lease.ExpiresAtUtc - DateTimeOffset.UtcNow;
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining.Add(TimeSpan.FromMilliseconds(100)), cancellationToken);
                throw new ProductLegalEntityScopeCommitResponseLostException();
            });

        var recovered = await guarded.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.True(recovered.Succeeded);
        Assert.False(recovered.WriteOutcomeAmbiguous);
        Assert.True(scenario.Lease.ExpiresAtUtc < DateTimeOffset.UtcNow);
        Assert.Equal(1, commitAttempts);
        var persistedBeforeReplay = Assert.IsType<ProductLegalEntityScopePolicy>(
            await scope.PolicyRepository().GetByGlobalProductIdAsync(
                scenario.Initial.GlobalProductId));
        Assert.Equal(1, persistedBeforeReplay.Version);
        Assert.Equal(2, persistedBeforeReplay.ScopePeriods.Count);
        Assert.Equal(2, persistedBeforeReplay.AuditIntents.Count);

        var replay = await guarded.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.False(replay.Succeeded);
        Assert.True(replay.VerifiedZeroMutation);
        Assert.Equal(1, commitAttempts);
        var persistedAfterReplay = Assert.IsType<ProductLegalEntityScopePolicy>(
            await scope.PolicyRepository().GetByGlobalProductIdAsync(
                scenario.Initial.GlobalProductId));
        Assert.Equal(persistedBeforeReplay.Version, persistedAfterReplay.Version);
        Assert.Equal(
            persistedBeforeReplay.ScopePeriods.Count,
            persistedAfterReplay.ScopePeriods.Count);
        Assert.Equal(
            persistedBeforeReplay.AuditIntents.Count,
            persistedAfterReplay.AuditIntents.Count);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("drift")]
    public async Task GuardedWrite_CommitResponseLostWithoutExactOutcome_RemainsAmbiguousAndRetainsLease(
        string outcome)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var rawPolicies = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_policies");
        var guarded = new ProductLegalEntityScopeGuardedWriteSession(
            scope.Database,
            new ProductScopeMongoScope.TenantContext(scope.TenantId),
            async (session, cancellationToken) =>
            {
                await session.CommitTransactionAsync(cancellationToken);
                var filter = Builders<BsonDocument>.Filter.Eq(
                    "_id",
                    scenario.Initial.Id);
                if (outcome == "missing")
                    await rawPolicies.DeleteOneAsync(filter, cancellationToken);
                else
                    await rawPolicies.UpdateOneAsync(
                        filter,
                        Builders<BsonDocument>.Update.Set(
                            nameof(ProductLegalEntityScopePolicy.Version),
                            99),
                        cancellationToken: cancellationToken);
                throw new ProductLegalEntityScopeCommitResponseLostException();
            });

        var result = await guarded.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.False(result.Succeeded);
        Assert.True(result.WriteOutcomeAmbiguous);
        var rollout = Assert.IsType<ProductLegalEntityScopeRolloutState>(
            await scope.RolloutRepository().GetAsync());
        Assert.Equal(scenario.Lease.Token, rollout.ActiveWriterLease!.Token);
        Assert.Equal(scenario.Lease.Generation, rollout.ActiveWriterLease.Generation);
    }

    [Fact]
    public async Task WriterLease_InitiallyExpiredExactIdentity_UnappliedReplaceFailsWithZeroEffectsAndRetainsLease()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        await SetLeaseWindowAsync(
            scope,
            scenario.Lease,
            DateTimeOffset.UtcNow.AddSeconds(
                -ProductLegalEntityScopeWriterLease.DurationSeconds - 1));

        var result = await scenario.Repository.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);
        var persisted = Assert.IsType<ProductLegalEntityScopePolicy>(
            await scope.PolicyRepository().GetByGlobalProductIdAsync(
                scenario.Initial.GlobalProductId));
        var rollout = Assert.IsType<ProductLegalEntityScopeRolloutState>(
            await scope.RolloutRepository().GetAsync());

        Assert.True(
            !result.Succeeded
            && result.VerifiedZeroMutation
            && result.LeaseRetentionRequired
            && persisted.Version == 0
            && persisted.ScopePeriods.Count == 1
            && persisted.AuditIntents.Count == 1
            && rollout.ActiveWriterLease?.Token == scenario.Lease.Token
            && rollout.ActiveWriterLease.Generation == scenario.Lease.Generation,
            $"expired exact lease outcome: succeeded={result.Succeeded}, " +
            $"zero={result.VerifiedZeroMutation}, version={persisted.Version}, " +
            $"periods={persisted.ScopePeriods.Count}, audits={persisted.AuditIntents.Count}, " +
            $"leaseRetained={rollout.ActiveWriterLease is not null}");
        await CompleteForegroundReplaceAndAssertLeaseRetainedAsync(
            scope,
            scenario,
            result);
    }

    [Fact]
    public async Task WriterLease_ValidAtEntryButExpiredBeforePhysicalMongoUpdate_FailsWithZeroEffectsAndRetainsLease()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        await SetLeaseWindowAsync(
            scope,
            scenario.Lease,
            DateTimeOffset.UtcNow.AddSeconds(-119));
        var updateStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var allowUpdate = new ManualResetEventSlim(false);
        var intercepted = 0;
        var settings = MongoClientSettings.FromConnectionString(
            MdmTestMongo.ConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(command =>
        {
            if (command.CommandName == "update"
                && command.Command.TryGetValue("update", out var collection)
                && collection.IsString
                && collection.AsString == "mdm_product_legal_entity_scope_rollout_states"
                && Interlocked.CompareExchange(ref intercepted, 1, 0) == 0)
            {
                updateStarted.TrySetResult();
                allowUpdate.Wait(TimeSpan.FromSeconds(10));
            }
        });
        var guarded = new ProductLegalEntityScopeGuardedWriteSession(
            new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName),
            new ProductScopeMongoScope.TenantContext(scope.TenantId));

        var operation = guarded.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);
        try
        {
            await updateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var remaining = scenario.Lease.ExpiresAtUtc - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining.Add(TimeSpan.FromMilliseconds(100)));
        }
        finally
        {
            allowUpdate.Set();
        }

        var result = await operation;
        var persisted = Assert.IsType<ProductLegalEntityScopePolicy>(
            await scope.PolicyRepository().GetByGlobalProductIdAsync(
                scenario.Initial.GlobalProductId));
        var rollout = Assert.IsType<ProductLegalEntityScopeRolloutState>(
            await scope.RolloutRepository().GetAsync());
        Assert.True(scenario.Lease.ExpiresAtUtc < DateTimeOffset.UtcNow);
        Assert.True(
            !result.Succeeded
            && result.VerifiedZeroMutation
            && result.LeaseRetentionRequired
            && persisted.Version == 0
            && persisted.ScopePeriods.Count == 1
            && persisted.AuditIntents.Count == 1
            && rollout.ActiveWriterLease?.Token == scenario.Lease.Token
            && rollout.ActiveWriterLease.Generation == scenario.Lease.Generation,
            $"mid-flight expiry outcome: succeeded={result.Succeeded}, " +
            $"zero={result.VerifiedZeroMutation}, version={persisted.Version}, " +
            $"periods={persisted.ScopePeriods.Count}, audits={persisted.AuditIntents.Count}, " +
            $"leaseRetained={rollout.ActiveWriterLease is not null}");
        await CompleteForegroundReplaceAndAssertLeaseRetainedAsync(
            scope,
            scenario,
            result);
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("actor")]
    [InlineData("tenant")]
    public async Task WriterLease_ExpiredIdentityDrift_IsRejectedWithoutMutationAndRetainsOriginalLease(
        string drift)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        await SetLeaseWindowAsync(
            scope,
            scenario.Lease,
            DateTimeOffset.UtcNow.AddSeconds(
                -ProductLegalEntityScopeWriterLease.DurationSeconds - 1));
        var authority = scenario.Authority;
        var tenantId = scope.TenantId;
        if (drift == "payload")
        {
            var period = Assert.Single(
                scenario.Requested.ScopePeriods,
                item => item.CommandId == scenario.Lease.CommandId);
            period.Mode = ProductLegalEntityScopeMode.Scoped;
            period.LegalEntityIds = [Guid.NewGuid()];
        }
        else if (drift == "actor")
        {
            authority = await IssueAuthorityAsync(
                scope.TenantId,
                Guid.NewGuid(),
                scenario.Initial.Id,
                ProductLegalEntityScopeMutationIdentity.Create(scenario.Command));
        }
        else
        {
            tenantId = Guid.NewGuid();
            authority = await IssueAuthorityAsync(
                tenantId,
                scenario.Lease.ActorId,
                scenario.Initial.Id,
                ProductLegalEntityScopeMutationIdentity.Create(scenario.Command));
        }
        var guarded = new ProductLegalEntityScopeGuardedWriteSession(
            scope.Database,
            new ProductScopeMongoScope.TenantContext(tenantId));

        var result = await guarded.ReplaceAsync(
            authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.False(result.Succeeded);
        Assert.True(result.VerifiedZeroMutation);
        var persisted = Assert.IsType<ProductLegalEntityScopePolicy>(
            await scope.PolicyRepository().GetByGlobalProductIdAsync(
                scenario.Initial.GlobalProductId));
        Assert.Equal(0, persisted.Version);
        Assert.Single(persisted.ScopePeriods);
        Assert.Single(persisted.AuditIntents);
        var rollout = Assert.IsType<ProductLegalEntityScopeRolloutState>(
            await scope.RolloutRepository().GetAsync());
        Assert.Equal(scenario.Lease.Token, rollout.ActiveWriterLease!.Token);
        Assert.Equal(scenario.Lease.Generation, rollout.ActiveWriterLease.Generation);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    public async Task WriterLease_MissingOrMalformedExpiry_FailsClosedWithoutMutationAndCompletionRetainsLease(
        string expiryShape)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var rollout = Assert.IsType<ProductLegalEntityScopeRolloutState>(
            await scope.RolloutRepository().GetAsync());
        var rawRollouts = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_rollout_states");
        var rolloutFilter = Builders<BsonDocument>.Filter.Eq(
            "_id",
            new BsonBinaryData(rollout.Id, GuidRepresentation.Standard));
        var expiryPath = "ActiveWriterLease.ExpiresAtUtc";
        var expiryUpdate = expiryShape == "missing"
            ? Builders<BsonDocument>.Update.Unset(expiryPath)
            : Builders<BsonDocument>.Update.Set(expiryPath, "invalid-expiry");
        var update = await rawRollouts.UpdateOneAsync(rolloutFilter, expiryUpdate);
        Assert.Equal(1, update.ModifiedCount);

        var result = await scenario.Repository.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);
        await CompleteForegroundReplaceAsync(scope, scenario, result);

        var persistedPolicy = Assert.IsType<ProductLegalEntityScopePolicy>(
            await scope.PolicyRepository().GetByGlobalProductIdAsync(
                scenario.Initial.GlobalProductId));
        var persistedRollout = Assert.IsType<BsonDocument>(
            await rawRollouts.Find(rolloutFilter).FirstOrDefaultAsync());
        var persistedLease = persistedRollout["ActiveWriterLease"].AsBsonDocument;
        Assert.False(result.Succeeded);
        Assert.True(result.VerifiedZeroMutation);
        Assert.True(result.LeaseRetentionRequired);
        Assert.Equal(0, persistedPolicy.Version);
        Assert.Single(persistedPolicy.ScopePeriods);
        Assert.Single(persistedPolicy.AuditIntents);
        Assert.Equal(
            new BsonBinaryData(scenario.Lease.Token, GuidRepresentation.Standard),
            persistedLease["Token"]);
        Assert.Equal(scenario.Lease.Generation, persistedLease["Generation"].ToInt64());
        Assert.Equal(expiryShape != "missing", persistedLease.Contains("ExpiresAtUtc"));
    }

    [Fact]
    public async Task GuardedWrite_DirectRepositoryBypass_DeniesReplaceWithoutPolicyOrAuditMutation()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);

        var result = await scenario.Repository.UpdateAsync(scenario.Requested, 0);

        Assert.False(result.Succeeded);
        Assert.True(result.VersionConflict);
        Assert.True(result.VerifiedZeroMutation);
        var persisted = await scope.PolicyRepository().GetByGlobalProductIdAsync(
            scenario.Initial.GlobalProductId);
        Assert.Equal(0, persisted!.Version);
        Assert.Single(persisted.ScopePeriods);
        Assert.Single(persisted.AuditIntents);
    }

    [Fact]
    public async Task GuardedWrite_PreparationChangesAfterLease_RollsBackPolicyAndAudit()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        await scope.RolloutCollection.UpdateOneAsync(
            item => item.TenantId == scope.TenantId,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(
                item => item.Mode,
                ProductLegalEntityScopeRolloutMode.Enforced));

        var result = await scenario.Repository.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.False(result.Succeeded);
        Assert.True(result.VerifiedZeroMutation);
        var persisted = await scope.PolicyRepository().GetByGlobalProductIdAsync(
            scenario.Initial.GlobalProductId);
        Assert.Equal(0, persisted!.Version);
        Assert.Single(persisted.ScopePeriods);
        Assert.Single(persisted.AuditIntents);
        Assert.Equal(
            ProductLegalEntityScopeRolloutMode.Enforced,
            (await scope.RolloutRepository().GetAsync())!.Mode);
    }

    [Fact]
    public async Task GuardedWrite_PolicyCasFailureAfterRolloutGuard_RollsBackGuardPolicyAndAudit()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var rawPolicies = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_policies");
        var rawRollout = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_rollout_states");
        await rawPolicies.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", scenario.Initial.Id),
            Builders<BsonDocument>.Update.Set(
                nameof(ProductLegalEntityScopePolicy.Version),
                7));
        var tenantFilter = Builders<BsonDocument>.Filter.Eq(
            nameof(ProductLegalEntityScopeRolloutState.TenantId),
            scope.TenantId);
        var policyFilter = Builders<BsonDocument>.Filter.Eq(
            "_id",
            scenario.Initial.Id);
        var rolloutBefore = await rawRollout.Find(tenantFilter).SingleAsync();
        var policyBefore = await rawPolicies.Find(policyFilter).SingleAsync();

        var result = await scenario.Repository.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.False(result.Succeeded);
        Assert.True(result.VerifiedZeroMutation);
        var rolloutAfter = await rawRollout.Find(tenantFilter).SingleAsync();
        var policyAfter = await rawPolicies.Find(policyFilter).SingleAsync();
        Assert.True(rolloutBefore.Equals(rolloutAfter));
        Assert.True(policyBefore.Equals(policyAfter));
    }

    [Fact]
    public async Task GuardedWrite_WrongTokenGenerationOrMutation_RollsBackAllEffects()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var staleLease = CopyLease(scenario.Lease);
        staleLease.Generation++;

        var stale = await scenario.Repository.ReplaceAsync(
            scenario.Authority,
            staleLease,
            scenario.Requested,
            0);
        var persisted = await scope.PolicyRepository().GetByGlobalProductIdAsync(
            scenario.Initial.GlobalProductId);

        Assert.False(stale.Succeeded);
        Assert.True(stale.VerifiedZeroMutation);
        Assert.Equal(0, persisted!.Version);
        Assert.Single(persisted.AuditIntents);
        Assert.NotNull((await scope.RolloutRepository().GetAsync())!.ActiveWriterLease);
    }

    [Fact]
    public async Task GuardedWrite_TwoContenders_CommitExactlyOneMutationAndOneAudit()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);

        var results = await Task.WhenAll(
            scenario.Repository.ReplaceAsync(
                scenario.Authority, scenario.Lease, scenario.Requested, 0),
            scenario.Repository.ReplaceAsync(
                scenario.Authority, scenario.Lease, scenario.Requested, 0));

        var persisted = await scope.PolicyRepository().GetByGlobalProductIdAsync(
            scenario.Initial.GlobalProductId);
        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.Equal(1, results.Count(result => !result.Succeeded));
        Assert.Equal(1, persisted!.Version);
        Assert.Equal(2, persisted.ScopePeriods.Count);
        Assert.Equal(2, persisted.AuditIntents.Count);
    }

    [Fact]
    public async Task GuardedWrite_CompleteDocumentOverOneMiB_FailsBeforeMutationAndPreservesUnknownBson()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var rawPolicies = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_policies");
        await rawPolicies.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", scenario.Initial.Id),
            Builders<BsonDocument>.Update.Set(
                "FutureOversizedField",
                new string('X', ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scenario.Repository.ReplaceAsync(
                scenario.Authority,
                scenario.Lease,
                scenario.Requested,
                0));

        var persisted = await rawPolicies.Find(
            Builders<BsonDocument>.Filter.Eq("_id", scenario.Initial.Id)).SingleAsync();
        Assert.Equal(0, persisted[nameof(ProductLegalEntityScopePolicy.Version)].AsInt32);
        Assert.Equal(
            ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes,
            persisted["FutureOversizedField"].AsString.Length);
        Assert.Single(persisted[nameof(ProductLegalEntityScopePolicy.AuditIntents)].AsBsonArray);
    }

    [Fact]
    public async Task Same_command_replays_exact_lease_and_stale_generation_cannot_release_no_ABA()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.RolloutRepository();
        var state = ProductLegalEntityScopeRolloutState.CreatePreparation(scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True((await repository.CreateAsync(state)).Succeeded);
        var requested = Lease(Guid.NewGuid(), Guid.NewGuid(), "CreateLskuDraft", "A");

        var first = await repository.AcquireWriterLeaseAsync(requested);
        var replay = await repository.AcquireWriterLeaseAsync(Lease(requested.CommandId, requested.ActorId, requested.MutationKind, "A"));
        Assert.True(first.Acquired);
        Assert.Equal(first.Lease!.Token, replay.Lease!.Token);
        Assert.Equal(first.Lease.Generation, replay.Lease.Generation);
        Assert.False(await repository.ReleaseWriterLeaseAsync(first.Lease.Token, first.Lease.Generation));
        Assert.True(await repository.BindWriterLeaseBaselineAsync(first.Lease.Token, first.Lease.Generation, new string('B', 64)));
        Assert.False(await repository.ReleaseWriterLeaseAsync(Guid.NewGuid(), first.Lease.Generation));
        Assert.False(await repository.ReleaseWriterLeaseAsync(first.Lease.Token, first.Lease.Generation + 1));
        Assert.True(await repository.ReleaseWriterLeaseAsync(first.Lease.Token, first.Lease.Generation));

        var second = await repository.AcquireWriterLeaseAsync(Lease(Guid.NewGuid(), Guid.NewGuid(), "CreateLskuDraft", "C"));
        Assert.True(second.Acquired);
        Assert.True(second.Lease!.Generation > first.Lease.Generation);
        Assert.False(await repository.ReleaseWriterLeaseAsync(first.Lease.Token, first.Lease.Generation));
    }

    [Fact]
    public async Task Pre_H1b_rollout_without_writer_fields_acquires_generation_one_without_migration()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.RolloutRepository();
        var state = ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True((await repository.CreateAsync(state)).Succeeded);
        await scope.RolloutCollection.UpdateOneAsync(
            item => item.TenantId == scope.TenantId && item.Id == state.Id,
            Builders<ProductLegalEntityScopeRolloutState>.Update
                .Unset(nameof(ProductLegalEntityScopeRolloutState.WriterLeaseGeneration))
                .Unset(nameof(ProductLegalEntityScopeRolloutState.ActiveWriterLease))
                .Unset(nameof(ProductLegalEntityScopeRolloutState.ActiveFence)));

        var acquired = await repository.AcquireWriterLeaseAsync(
            Lease(Guid.NewGuid(), Guid.NewGuid(), "RequestProductAbbreviationAllocation", "L"));

        Assert.True(acquired.Acquired);
        Assert.NotNull(acquired.Lease);
        Assert.Equal(1, acquired.Lease.Generation);
        var persisted = await scope.RolloutCollection
            .Find(item => item.TenantId == scope.TenantId && item.Id == state.Id)
            .SingleAsync();
        Assert.Equal(1, persisted.WriterLeaseGeneration);
        Assert.Equal(acquired.Lease.Token, persisted.ActiveWriterLease!.Token);
    }

    [Fact]
    public async Task Tenant_B_cannot_observe_or_release_tenant_A_lease_and_payload_drift_is_denied()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var tenantA = scope.RolloutRepository();
        Assert.True((await tenantA.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now))).Succeeded);
        var command = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var acquired = await tenantA.AcquireWriterLeaseAsync(Lease(command, actor, "CreateFinishedGoodDraft", "A"));
        var drift = await tenantA.AcquireWriterLeaseAsync(Lease(command, actor, "CreateFinishedGoodDraft", "B"));
        var tenantB = scope.RolloutRepository(Guid.NewGuid());

        Assert.False(drift.Acquired);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", drift.FailureCode);
        Assert.Null(await tenantB.GetAsync());
        Assert.False(await tenantB.ReleaseWriterLeaseAsync(acquired.Lease!.Token, acquired.Lease.Generation));
    }

    [Fact]
    public async Task Coordinator_zero_delta_releases_while_202_exception_and_cancellation_retain()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var tenant = new TenantContext(); tenant.SetTenant(scope.TenantId);
        var repository = scope.RolloutRepository();
        Assert.True((await repository.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now))).Succeeded);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(repository,
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(Guid.NewGuid().ToString("D")), tenant, new FixedClock());

        // WP-MDM-SCOPE-ACCEPT-01 FIX1 B4: a product writer's lease lives in the per-object list (ProductWriterLeases).
        var first = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
        await coordinator.CompleteAsync(first, false, 409, false, CancellationToken.None);
        Assert.Empty((await repository.GetAsync())!.ProductWriterLeases);

        foreach (var (status, exceptional) in new[] { (202, false), (500, true), (499, true) })
        {
            var admission = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
            await coordinator.CompleteAsync(admission, false, status, exceptional, CancellationToken.None);
            var lease = Assert.Single((await repository.GetAsync())!.ProductWriterLeases);
            Assert.True(await repository.ReleaseProductWriterLeaseAsync(lease.Token, lease.Generation));
        }
    }

    [Fact]
    public async Task Coordinator_ignores_delivery_owned_progress_but_retains_for_partial_immutable_intent()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var tenant = new TenantContext(); tenant.SetTenant(scope.TenantId);
        var repository = scope.RolloutRepository();
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True((await repository.CreateAsync(rollout)).Succeeded);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(repository,
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(Guid.NewGuid().ToString("D")), tenant, new FixedClock());
        var collection = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_rollout_states");

        var deliveryOnly = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", rollout.Id),
            Builders<BsonDocument>.Update.Push("AuditIntentReceipts", new BsonDocument
            {
                { "IntentId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "CentralAcknowledgement", "receipt-progress" }
            }));
        await coordinator.CompleteAsync(deliveryOnly, false, 409, false, CancellationToken.None);
        Assert.Empty((await repository.GetAsync())!.ProductWriterLeases);

        // FIX1 B4: what proves a write changed something is its OWN object (here the LSKU's GSKU), not the tenant.
        var partialCommand = Command(Guid.NewGuid());
        var partialMutation = await coordinator.EnterAsync(partialCommand, CancellationToken.None);
        await scope.Database.GetCollection<BsonDocument>("mdm_lskus").InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
            { "TenantId", new BsonBinaryData(scope.TenantId, GuidRepresentation.Standard) },
            { "GskuId", new BsonBinaryData(partialCommand.Request.GskuId, GuidRepresentation.Standard) },
            { "IsDeleted", false }
        });
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", rollout.Id),
            Builders<BsonDocument>.Update.Push("AuditIntents", new BsonDocument
            {
                { "IntentId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "TenantId", new BsonBinaryData(scope.TenantId, GuidRepresentation.Standard) },
                { "AggregateType", (int)AuditAggregateType.ProductLegalEntityScopeRolloutState },
                { "AggregateId", new BsonBinaryData(rollout.Id, GuidRepresentation.Standard) },
                { "SourceService", AuditIntentContract.SourceService },
                { "CommandId", Guid.NewGuid().ToString("D") }, { "EvidenceHash", new string('A', 64) }
            }));
        await coordinator.CompleteAsync(partialMutation, false, 409, false, CancellationToken.None);
        Assert.Single((await repository.GetAsync())!.ProductWriterLeases);
        await scope.Database.GetCollection<BsonDocument>("mdm_lskus").DeleteManyAsync(
            new BsonDocument("TenantId", new BsonBinaryData(scope.TenantId, GuidRepresentation.Standard)));
    }

    [Fact]
    public async Task Coordinator_allows_only_exact_GSKU_facade_inner_nesting()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var tenant = new TenantContext(); tenant.SetTenant(scope.TenantId);
        var repository = scope.RolloutRepository();
        Assert.True((await repository.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now))).Succeeded);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(repository,
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(Guid.NewGuid().ToString("D")), tenant, new FixedClock());
        var productId = Guid.NewGuid();
        const string operation = "exact-operation";
        var facade = new CreateFirstGskuDraftFacadeCommand(
            new ProductItemSkuMasterModels.CreateFirstGskuDraftFacadeRequest
            {
                GlobalProductId = productId, PackQuantity = 10, PackUomCode = "EA"
            }, operation);
        var inner = new CreateFirstGskuDraftCommand(new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
        {
            GlobalProductId = productId, PackQuantity = 10, PackUomCode = "EA",
            CreationCommandId = $"GSKU:{operation.ToUpperInvariant()}"
        });

        var parent = await coordinator.EnterAsync(facade, CancellationToken.None);
        Assert.True(parent.OwnsLease);
        using (coordinator.Activate(parent))
        {
            var exactChild = await coordinator.EnterAsync(inner, CancellationToken.None);
            var unrelated = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
            Assert.True(exactChild.IsAllowed); Assert.False(exactChild.OwnsLease);
            Assert.False(unrelated.IsAllowed);
            Assert.Equal("PRODUCT_SCOPE_NESTED_MUTATION_NOT_AUTHORIZED", unrelated.FailureCode);
            await coordinator.CompleteAsync(exactChild, true, 201, false, CancellationToken.None);
        }
        await coordinator.CompleteAsync(parent, true, 201, false, CancellationToken.None);
        Assert.Null((await repository.GetAsync())!.ActiveWriterLease);

        var directInner = await coordinator.EnterAsync(inner, CancellationToken.None);
        Assert.True(directInner.OwnsLease);
        await coordinator.CompleteAsync(directInner, true, 201, false, CancellationToken.None);
    }

    private static async Task<GuardedReplaceScenario> CreateGuardedReplaceScenarioAsync(
        ProductScopeMongoScope scope)
    {
        var repository = scope.PolicyRepository();
        var productId = Guid.NewGuid();
        var initial = ProductLegalEntityScopeMongoTests.CreatePolicy(
            scope.TenantId,
            productId,
            Guid.NewGuid());
        Assert.True((await repository.CreateAsync(initial)).Succeeded);
        var rolloutRepository = scope.RolloutRepository();
        Assert.True((await rolloutRepository.CreateAsync(
            ProductLegalEntityScopeRolloutState.CreatePreparation(
                scope.TenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Now))).Succeeded);

        var subjectId = Guid.NewGuid();
        var command = new ReplaceProductLegalEntityScopePolicyCommand(
            productId,
            Guid.NewGuid(),
            new()
            {
                ExpectedVersion = 0,
                Mode = ProductLegalEntityScopeMode.GroupWide
            });
        var mutation = ProductLegalEntityScopeMutationIdentity.Create(command);
        var authority = await IssueAuthorityAsync(
            scope.TenantId,
            subjectId,
            initial.Id,
            mutation);
        var leaseAcquiredAtUtc = DateTimeOffset.UtcNow;
        var leaseRequest = new ProductLegalEntityScopeWriterLease
        {
            Token = Guid.NewGuid(),
            Generation = 1,
            CommandId = mutation.CommandId,
            ActorId = subjectId,
            MutationKind = mutation.Kind,
            PayloadFingerprint = mutation.PayloadFingerprint,
            Owner = "Diten.MDM:ForegroundProductLegalEntityScopeReplace",
            AcquiredAtUtc = leaseAcquiredAtUtc,
            ExpiresAtUtc = leaseAcquiredAtUtc.AddSeconds(
                ProductLegalEntityScopeWriterLease.DurationSeconds),
            // As the coordinator stamps a foreground Replace: keyed by its product and its command (FIX1 B4).
            ObjectKeys = [.. mutation.ObjectKeys]
        };
        var acquired = await rolloutRepository.AcquireWriterLeaseAsync(leaseRequest);
        Assert.True(acquired.Acquired);
        Assert.True(await rolloutRepository.BindWriterLeaseBaselineAsync(
            acquired.Lease!.Token,
            acquired.Lease.Generation,
            new string('B', 64)));
        var replay = await rolloutRepository.AcquireWriterLeaseAsync(leaseRequest);
        var lease = Assert.IsType<ProductLegalEntityScopeWriterLease>(replay.Lease);
        Assert.True(lease.BaselineBound);

        var requested = Assert.IsType<ProductLegalEntityScopePolicy>(
            await repository.GetByGlobalProductIdAsync(productId));
        var changedAt = DateTimeOffset.UtcNow.AddMilliseconds(-1);
        requested.ReplaceCurrent(
            0,
            command.CommandId,
            ProductLegalEntityScopeMode.GroupWide,
            [],
            subjectId,
            changedAt);
        ProductLegalEntityScopeMongoTests.AddPolicyUpdateAuditIntent(
            requested,
            ProductAuditOperation.ProductLegalEntityScopePolicyReplaced,
            0);
        return new GuardedReplaceScenario(
            repository,
            initial,
            requested,
            authority,
            lease,
            command);
    }

    private static async Task SetLeaseWindowAsync(
        ProductScopeMongoScope scope,
        ProductLegalEntityScopeWriterLease lease,
        DateTimeOffset acquiredAtUtc)
    {
        var acquiredTicks = acquiredAtUtc.UtcDateTime.Ticks;
        acquiredAtUtc = new DateTimeOffset(
            acquiredTicks - acquiredTicks % TimeSpan.TicksPerMillisecond,
            TimeSpan.Zero);
        lease.AcquiredAtUtc = acquiredAtUtc;
        lease.ExpiresAtUtc = acquiredAtUtc.AddSeconds(
            ProductLegalEntityScopeWriterLease.DurationSeconds);
        var update = await scope.RolloutCollection.UpdateOneAsync(
            item => item.TenantId == scope.TenantId,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(
                item => item.ActiveWriterLease,
                lease));
        Assert.Equal(1, update.ModifiedCount);
    }

    private static async Task CompleteForegroundReplaceAndAssertLeaseRetainedAsync(
        ProductScopeMongoScope scope,
        GuardedReplaceScenario scenario,
        ProductLegalEntityScopePolicyWriteResult result)
    {
        await CompleteForegroundReplaceAsync(scope, scenario, result);

        var rollout = Assert.IsType<ProductLegalEntityScopeRolloutState>(
            await scope.RolloutRepository().GetAsync());
        Assert.Equal(scenario.Lease.Token, rollout.ActiveWriterLease?.Token);
        Assert.Equal(scenario.Lease.Generation, rollout.ActiveWriterLease?.Generation);
    }

    private static async Task CompleteForegroundReplaceAsync(
        ProductScopeMongoScope scope,
        GuardedReplaceScenario scenario,
        ProductLegalEntityScopePolicyWriteResult result)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(scope.TenantId);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(
            scope.RolloutRepository(),
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(scenario.Lease.ActorId.ToString("D")),
            tenant,
            TimeProvider.System);
        var admission = new ProductLegalEntityScopeWriteAdmission(
            true,
            true,
            false,
            null,
            new ProductLegalEntityScopeWriteFenceCoordinator.LeaseContext(
                scope.TenantId,
                scenario.Lease,
                scenario.Command));

        await coordinator.CompleteForegroundReplaceAsync(
            admission,
            result,
            CancellationToken.None);
    }

    private static async Task<ProductLegalEntityScopeVerifiedWriterAuthority> IssueAuthorityAsync(
        Guid tenantId,
        Guid subjectId,
        Guid aggregateId,
        ProductLegalEntityScopeMutationIdentity mutation)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("actor_type", "tenant_user"),
                new Claim("tenant_id", tenantId.ToString("D")),
                new Claim("sub", subjectId.ToString("D")),
                new Claim("permission", "mdm.product-legal-entity-scopes.replace")
            ], "test"))
        };
        context.Request.Headers["X-Tenant-Id"] = tenantId.ToString("D");
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var provider = new ProductLegalEntityScopeWriterAuthorityProvider(
            new HttpContextAccessor { HttpContext = context },
            tenant);
        return Assert.IsType<ProductLegalEntityScopeVerifiedWriterAuthority>(
            await provider.ResolveForegroundReplaceAsync(aggregateId, mutation));
    }

    internal static ProductLegalEntityScopeWriterLease CopyLease(
        ProductLegalEntityScopeWriterLease source) => new()
        {
            Token = source.Token,
            Generation = source.Generation,
            CommandId = source.CommandId,
            ActorId = source.ActorId,
            MutationKind = source.MutationKind,
            PayloadFingerprint = source.PayloadFingerprint,
            Owner = source.Owner,
            AcquiredAtUtc = source.AcquiredAtUtc,
            ExpiresAtUtc = source.ExpiresAtUtc,
            PreWriteStateHash = source.PreWriteStateHash
        };

    private sealed record GuardedReplaceScenario(
        ProductLegalEntityScopePolicyRepository Repository,
        ProductLegalEntityScopePolicy Initial,
        ProductLegalEntityScopePolicy Requested,
        ProductLegalEntityScopeVerifiedWriterAuthority Authority,
        ProductLegalEntityScopeWriterLease Lease,
        ReplaceProductLegalEntityScopePolicyCommand Command);

    private static CreateLskuDraftCommand Command(Guid identity) => new(new ProductItemSkuMasterModels.CreateLskuDraftRequest
    {
        GskuId = Guid.NewGuid(), MarketCode = "TR", IdempotencyKey = identity.ToString("D")
    });
    private sealed class Actor(string actorId) : IProductIdentityActorContext { public string ActorId => actorId; }
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
    }


    private static ProductLegalEntityScopeWriterLease Lease(
        Guid command,
        Guid actor,
        string kind,
        string fingerprintSeed)
    {
        var acquiredAtUtc = DateTimeOffset.UtcNow;
        return new()
        {
            Token = Guid.NewGuid(), Generation = 1, CommandId = command, ActorId = actor,
            MutationKind = kind, PayloadFingerprint = new string(fingerprintSeed[0], 64),
            Owner = $"Diten.MDM:{kind}", AcquiredAtUtc = acquiredAtUtc,
            ExpiresAtUtc = acquiredAtUtc.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds)
        };
    }
}
