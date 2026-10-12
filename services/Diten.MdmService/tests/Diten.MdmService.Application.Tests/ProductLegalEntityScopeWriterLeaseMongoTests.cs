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

    /// <summary>Seconds after a lease's start at which another command may take it: expiry plus the clock-drift margin.</summary>
    private const int Gone = ProductLegalEntityScopeWriterLease.DurationSeconds + ProductLegalEntityScopeWriterLease.ClockSkewSeconds + 1;
    // BL-595 FIX2: the shared helpers follow this class's own replica set, in-process (never the environment).
    private readonly IDisposable _ownedRedirect;

    public ProductLegalEntityScopeWriterLeaseMongoTests(AuditIntentTemporalMongoFixture fixture)
    {
        _ownedRedirect = MdmTestMongo.UseOwnedRedirect(fixture.ReplicaConnectionString);
    }

    public void Dispose() => _ownedRedirect.Dispose();

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

        harness.Clock.Now = Start.AddSeconds(Gone);
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
            Fence(Start.AddSeconds(Gone)), rollout.Id, rollout.Version,
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

    // ── FIX2 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_repeat_whose_lease_ran_out_takes_a_fresh_lease_and_another_writer_is_then_refused()
    {
        // FIX2 (2): before, the repeat got its EXPIRED lease back (no renewal, no new baseline) while another writer could
        // take that same expired lease over — both wrote the same product.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var product = Guid.NewGuid();
        var c = new TestWrite(product, Guid.NewGuid());
        var first = await harness.Coordinator().EnterAsync(c, default);
        await harness.Coordinator().CompleteAsync(first, false, 202, false, default);

        harness.Clock.Now = Start.AddSeconds(130);
        var repeat = await harness.Coordinator().EnterAsync(c, default);
        var other = await harness.Coordinator().EnterAsync(new TestWrite(product, Guid.NewGuid()), default);

        Assert.True(repeat.IsAllowed, repeat.FailureCode);
        Assert.NotEqual(first.Context!.Lease.Token, repeat.Context!.Lease.Token);
        Assert.True(repeat.Context.Lease.Generation > first.Context.Lease.Generation);
        Assert.Equal(harness.Clock.Now.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds), repeat.Context.Lease.ExpiresAtUtc);
        Assert.False(other.IsAllowed);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", other.FailureCode);
        Assert.Equal(c.CommandId, Assert.Single((await harness.Rollouts.GetAsync())!.ProductWriterLeases).CommandId);
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseRenewed, "by", "writer"));
    }

    [Fact]
    public async Task A_repeat_whose_lease_was_taken_over_is_refused()
    {
        await using var harness = await Harness.CreateAsync();
        var product = Guid.NewGuid();
        var c = new TestWrite(product, Guid.NewGuid());
        var first = await harness.Coordinator().EnterAsync(c, default);
        await harness.Coordinator().CompleteAsync(first, false, 202, false, default);

        harness.Clock.Now = Start.AddSeconds(130);
        var other = new TestWrite(product, Guid.NewGuid());
        var takeover = await harness.Coordinator().EnterAsync(other, default);
        var repeat = await harness.Coordinator().EnterAsync(c, default);

        Assert.True(takeover.IsAllowed, takeover.FailureCode);
        Assert.False(repeat.IsAllowed);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", repeat.FailureCode);
        Assert.Equal(other.CommandId, Assert.Single((await harness.Rollouts.GetAsync())!.ProductWriterLeases).CommandId);
    }

    [Fact]
    public async Task The_foreground_slot_gives_a_command_whose_lease_ran_out_a_fresh_generation()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Rollouts.AcquireWriterLeaseAsync(harness.Lease(Guid.NewGuid()));
        Assert.True(first.Acquired, first.FailureCode);

        harness.Clock.Now = Start.AddSeconds(130);
        var again = await harness.Rollouts.AcquireWriterLeaseAsync(harness.SameCommand(first.Lease!));

        Assert.True(again.Acquired, again.FailureCode);
        Assert.True(again.Renewed);
        Assert.NotEqual(first.Lease!.Token, again.Lease!.Token);
        Assert.True(again.Lease.Generation > first.Lease.Generation);
    }

    [Fact]
    public async Task A_lease_within_the_clock_drift_margin_is_not_taken_and_one_past_it_is()
    {
        // FIX2 (6): another instance's clock may run a few seconds ahead; a lease is taken only after expiry PLUS the margin.
        await using var harness = await Harness.CreateAsync();
        var product = Guid.NewGuid();
        Assert.True((await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(product))).Acquired);

        harness.Clock.Now = Start.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds + ProductLegalEntityScopeWriterLease.ClockSkewSeconds - 1);
        var early = await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(product));
        harness.Clock.Now = Start.AddSeconds(Gone);
        var late = await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(product));

        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", early.FailureCode);
        Assert.True(late.Acquired, late.FailureCode);
        Assert.Equal(1, late.TakenOver);
    }

    [Fact]
    public async Task A_lease_stored_with_another_duration_is_still_read()
    {
        // FIX2 (6): before, EnsureValid demanded expiry == start + DurationSeconds — changing the constant would have made
        // every rollout record holding an older lease unreadable (503 for the tenant).
        await using var harness = await Harness.CreateAsync();
        var lease = harness.Lease(Guid.NewGuid());
        lease.ExpiresAtUtc = lease.AcquiredAtUtc.AddSeconds(300);
        await harness.Scope.RolloutCollection.UpdateOneAsync(item => item.TenantId == harness.Scope.TenantId,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(item => item.ProductWriterLeases, [lease]));

        var read = await harness.Rollouts.GetAsync();

        Assert.Equal(lease.ExpiresAtUtc, Assert.Single(read!.ProductWriterLeases).ExpiresAtUtc);
    }

    [Fact]
    public async Task Eight_writers_of_eight_products_at_once_are_all_admitted_and_eight_of_one_product_admit_exactly_one()
    {
        // FIX2 (7) and CT sabotage SC2: concurrent acquisitions. Before, three optimistic rounds — losers of a busy tenant
        // heard the same 409 as a real conflict; and nothing measured that two writers of one product cannot both win.
        await using var harness = await Harness.CreateAsync();
        var many = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            Task.Run(() => harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid())))));
        Assert.All(many, result => Assert.True(result.Acquired, result.FailureCode));

        var product = Guid.NewGuid();
        var one = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            Task.Run(() => harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(product)))));

        Assert.Equal(1, one.Count(result => result.Acquired));
        Assert.All(one.Where(result => !result.Acquired),
            result => Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", result.FailureCode));
        var leases = (await harness.Rollouts.GetAsync())!.ProductWriterLeases;
        Assert.Equal(9, leases.Count);
        Assert.Single(leases, lease => lease.ObjectKeys.Contains($"GlobalProductId:{product:D}"));
    }

    [Fact]
    public async Task A_thousand_failed_creates_leave_a_bounded_lease_list()
    {
        // FIX2 (5): a create names no object; its failure keeps a lease keyed only by its command. Before, nobody ever met
        // such a lease — the list grew with every failure and every acquisition rewrote all of it.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        for (var i = 0; i < 1000; i++)
        {
            harness.Clock.Now = Start.AddSeconds(i);
            var admission = await harness.Coordinator().EnterAsync(new TestCreate(Guid.NewGuid()), default);
            Assert.True(admission.IsAllowed, admission.FailureCode);
            await harness.Coordinator().CompleteAsync(admission, false, 409, false, default);
        }

        var held = (await harness.Rollouts.GetAsync())!.ProductWriterLeases.Count;
        Assert.InRange(held, 1, ProductLegalEntityScopeWriterLease.DurationSeconds + ProductLegalEntityScopeWriterLease.ClockSkewSeconds + 1);
        Assert.True(metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseSwept) >= 1000 - held);
    }

    [Fact]
    public async Task A_tenant_holds_at_most_the_maximum_number_of_running_product_leases()
    {
        await using var harness = await Harness.CreateAsync();
        for (var i = 0; i < ProductLegalEntityScopeRolloutState.MaximumProductWriterLeases; i++)
            Assert.True((await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()))).Acquired);

        var beyond = await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()));

        Assert.False(beyond.Acquired);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_CAPACITY_EXCEEDED", beyond.FailureCode);
        Assert.Equal(ProductLegalEntityScopeRolloutState.MaximumProductWriterLeases,
            (await harness.Rollouts.GetAsync())!.ProductWriterLeases.Count);
    }

    [Fact]
    public async Task A_failed_write_that_half_added_a_child_below_its_product_keeps_its_lease()
    {
        // FIX2 (8): a GSKU carries no GlobalProductId — before, the proof of CreateFirstGskuDraft(P) did not see a GSKU
        // half-added under P's revision, called the write "unchanged" and freed the lease.
        await using var harness = await Harness.CreateAsync();
        var product = Guid.NewGuid();
        var revision = Guid.NewGuid();
        await harness.InsertProductAsync(product);
        await harness.InsertRawAsync("mdm_product_definition_revisions", revision, ("GlobalProductId", product));
        var write = await harness.Coordinator().EnterAsync(new TestWrite(product, Guid.NewGuid()), default);
        Assert.NotNull(write.Context!.Lease.PreWriteStateHash);

        await harness.InsertRawAsync("mdm_gskus", Guid.NewGuid(), ("ProductDefinitionRevisionId", revision));
        await harness.Coordinator().CompleteAsync(write, false, 409, false, default);

        Assert.Single((await harness.Rollouts.GetAsync())!.ProductWriterLeases);
    }

    [Fact]
    public async Task A_refused_admission_that_keeps_its_lease_is_counted()
    {
        // FIX2 (3): before, a refusal after the lease was taken (here: the baseline could not be bound) left the lease
        // behind without a trace.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var coordinator = harness.Coordinator(new BindRefused(harness.Rollouts));

        var refused = await coordinator.EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);

        Assert.False(refused.IsAllowed);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_BASELINE_CONFLICT", refused.FailureCode);
        Assert.Single((await harness.Rollouts.GetAsync())!.ProductWriterLeases);
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseRetained, "reason", "baseline_conflict"));
    }

    [Fact]
    public async Task A_writer_that_died_between_taking_its_lease_and_binding_it_is_taken_over_and_counted()
    {
        // Test gap: a crash between acquire and bind leaves a lease without a baseline; its exit is the expiry.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var product = Guid.NewGuid();
        var orphan = await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(product));
        Assert.True(orphan.Acquired);

        harness.Clock.Now = Start.AddSeconds(Gone);
        var next = await harness.Coordinator().EnterAsync(new TestWrite(product, Guid.NewGuid()), default);

        Assert.True(next.IsAllowed, next.FailureCode);
        Assert.DoesNotContain((await harness.Rollouts.GetAsync())!.ProductWriterLeases, lease => lease.Token == orphan.Lease!.Token);
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseTakenOver, "by", "writer"));
    }

    [Fact]
    public async Task A_release_with_a_wrong_token_or_an_old_generation_frees_nothing()
    {
        await using var harness = await Harness.CreateAsync();
        var held = await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()));
        var lease = held.Lease!;

        Assert.False(await harness.Rollouts.ReleaseProductWriterLeaseAsync(Guid.NewGuid(), lease.Generation));
        Assert.False(await harness.Rollouts.ReleaseProductWriterLeaseAsync(lease.Token, lease.Generation + 1));
        Assert.Single((await harness.Rollouts.GetAsync())!.ProductWriterLeases);
        Assert.True(await harness.Rollouts.ReleaseProductWriterLeaseAsync(lease.Token, lease.Generation));
    }

    [Fact]
    public async Task While_a_fence_is_up_a_product_writer_is_told_the_fence_is_active()
    {
        // CT sabotage SC4: the early "fence active" answer was removed and nothing turned red — the writer then heard
        // "lease unavailable", and the screen said the wrong thing.
        await using var harness = await Harness.CreateAsync();
        var rollout = (await harness.Rollouts.GetAsync())!;
        Assert.True((await harness.Rollouts.AcquireFenceAsync(Fence(Start), rollout.Id, rollout.Version, ProductLegalEntityScopeRolloutMode.Preparation)).Acquired);

        var direct = await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()));
        var slot = await harness.Rollouts.AcquireWriterLeaseAsync(harness.Lease(Guid.NewGuid()));
        var admission = await harness.Coordinator().EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);

        Assert.Equal("PRODUCT_SCOPE_WRITE_FENCE_ACTIVE", direct.FailureCode);
        Assert.Equal("PRODUCT_SCOPE_WRITE_FENCE_ACTIVE", slot.FailureCode);
        Assert.Equal("PRODUCT_SCOPE_WRITE_FENCE_ACTIVE", admission.FailureCode);
        Assert.Equal(409, admission.StatusCode);
    }

    [Fact]
    public async Task A_fence_reports_the_expired_leases_it_clears()
    {
        await using var harness = await Harness.CreateAsync();
        Assert.True((await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()))).Acquired);
        Assert.True((await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()))).Acquired);
        var rollout = (await harness.Rollouts.GetAsync())!;

        var fenced = await harness.Rollouts.AcquireFenceAsync(Fence(Start.AddSeconds(Gone)), rollout.Id, rollout.Version,
            ProductLegalEntityScopeRolloutMode.Preparation);

        Assert.True(fenced.Acquired, fenced.FailureCode);
        Assert.Equal(2, fenced.ClearedLeases);
    }

    [Fact]
    public async Task The_hold_gauges_show_a_fence_left_up_and_the_leases_held()
    {
        // FIX2 (3): a fence has no expiry — before, "a fence has been up for N minutes" was visible nowhere.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var database = await HoldDatabaseAsync(harness);
        try
        {
            var rollouts = new ProductLegalEntityScopeRolloutStateRepository(database, harness.Tenant);
            Assert.True((await rollouts.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
                harness.Scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Start))).Succeeded);
            Assert.True((await rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()))).Acquired);
            var stale = harness.Lease(Guid.NewGuid());
            stale.AcquiredAtUtc = Start.AddMinutes(-10);
            stale.ExpiresAtUtc = stale.AcquiredAtUtc.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds);
            await database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states")
                .UpdateOneAsync(item => item.TenantId == harness.Scope.TenantId,
                    Builders<ProductLegalEntityScopeRolloutState>.Update.Push(item => item.ProductWriterLeases, stale)
                        .Set(item => item.ActiveFence, Fence(Start)));

            var holds = await new ProductLegalEntityScopeHoldInventoryRepository(database).ReadAsync(Start.AddMinutes(30));
            ProductLegalEntityScopeRolloutMetrics.RecordHolds(new(holds.OldestFenceAcquiredAtUtc, holds.FencesUp,
                holds.RunningLeases, holds.ExpiredLeases), Start.AddMinutes(30));

            Assert.Equal(30, metrics.Observe(ProductLegalEntityScopeRolloutMetrics.FenceAgeMinutes));
            Assert.Equal(1, metrics.Observe(ProductLegalEntityScopeRolloutMetrics.FencesUp));
            Assert.Equal(2, metrics.Observe(ProductLegalEntityScopeRolloutMetrics.LeasesHeld, "state", "expired"));
        }
        finally
        {
            ProductLegalEntityScopeRolloutMetrics.RecordHolds(ProductLegalEntityScopeRolloutMetrics.HoldSnapshot.Empty, Start);
            await harness.Scope.Database.Client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName);
        }
    }

    [Fact]
    public async Task Create_replace_and_end_on_a_rollout_record_that_cannot_be_read_answer_the_coded_503()
    {
        // FIX2 (11): before, the scope commands read the record unguarded — a generic 500.
        await using var harness = await Harness.CreateAsync();
        await harness.BreakRolloutAsync();
        var product = Guid.NewGuid();

        var create = await new Features.ProductLegalEntityScopes.Handlers.CommandHandlers.CreateProductLegalEntityScopePolicyHandler(
                null!, harness.Rollouts, null!, null!, null!, harness.Tenant, new Actor(harness.Actor.ToString("D")), harness.Clock)
            .Handle(new(product, Guid.NewGuid(), new() { Mode = ProductLegalEntityScopeMode.GroupWide }), default);
        var replace = await new Features.ProductLegalEntityScopes.Handlers.CommandHandlers.ReplaceProductLegalEntityScopePolicyHandler(
                null!, harness.Rollouts, null!, null!, null!, null!)
            .Handle(new(product, Guid.NewGuid(), new() { ExpectedVersion = 0, Mode = ProductLegalEntityScopeMode.GroupWide }), default);
        var end = await new Features.ProductLegalEntityScopes.Handlers.CommandHandlers.EndProductLegalEntityScopePolicyHandler(
                null!, harness.Rollouts, new Actor(harness.Actor.ToString("D")))
            .Handle(new(product, Guid.NewGuid(), new() { ExpectedVersion = 0 }), default);
        var slot = await harness.Coordinator().EnterAsync(new TestWrite(product, Guid.NewGuid()), default);

        foreach (var (status, code) in new[] { (create.StatusCode, create.Errors?.FirstOrDefault()),
                     (replace.StatusCode, replace.Errors?.FirstOrDefault()), (end.StatusCode, end.Errors?.FirstOrDefault()),
                     (slot.StatusCode, slot.FailureCode) })
        {
            Assert.Equal(503, status);
            Assert.Equal("PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID", code);
        }
    }

    /// <summary>
    /// FIX2 (9) / FIX3 (8) — the index each clause of the proof's queries must be read by, named. The proof reads its
    /// objects by _id OR parent id, sorted by _id (as production does), and expands the hierarchy by parent id. One clause
    /// without its index turns the whole $or into a scan of the tenant.
    /// </summary>
    private static readonly Dictionary<(string Collection, string Field), string[]> ExpectedIndexes = new()
    {
        [("mdm_product_abbreviation_allocation_ledger", "GlobalProductId")] = ["ix_mdm_product_abbreviation_ledger_tenant_product_scope_proof"],
        [("mdm_product_abbreviation_allocation_ledger", "RegisterEntryId")] = ["ix_mdm_product_abbreviation_ledger_tenant_entry_scope_proof"],
        [("mdm_product_abbreviation_history", "GlobalProductId")] = ["ix_mdm_product_abbreviation_history_tenant_product_scope_proof"],
        [("mdm_product_abbreviation_history", "RegisterEntryId")] = ["ix_mdm_product_abbreviation_history_tenant_entry"],
        [("mdm_product_abbreviation_register", "GlobalProductId")] = ["ix_mdm_product_abbreviation_register_tenant_product_scope_proof"],
        [("mdm_product_legal_entity_scope_policies", "GlobalProductId")] = ["ux_mdm_product_legal_entity_scope_policies_tenant_product"],
        [("mdm_product_definition_revisions", "GlobalProductId")] = ["ix_mdm_product_definition_revisions_tenant_parent", "ux_mdm_product_definition_revisions_tenant_parent_identifier"],
        [("mdm_gskus", "ProductDefinitionRevisionId")] = ["ix_mdm_gskus_tenant_revision"],
        [("mdm_gskus", "CodeReservationId")] = ["ux_mdm_gskus_tenant_reservation"],
        [("mdm_lskus", "GskuId")] = ["ux_mdm_lskus_tenant_gsku_market"],
        [("mdm_finished_goods", "GskuId")] = ["ix_mdm_finished_goods_tenant_gsku"]
    };

    [Theory]
    [InlineData("mdm_product_abbreviation_allocation_ledger")]
    [InlineData("mdm_product_abbreviation_history")]
    [InlineData("mdm_product_abbreviation_register")]
    [InlineData("mdm_product_legal_entity_scope_policies")]
    [InlineData("mdm_product_definition_revisions")]
    [InlineData("mdm_gskus")]
    [InlineData("mdm_lskus")]
    [InlineData("mdm_finished_goods")]
    public async Task Every_clause_of_the_object_proof_query_uses_an_index(string collection)
    {
        await using var harness = await Harness.CreateAsync();
        // The owners' own indexes, as production has them (each repository creates its indexes when built; BL-598: the
        // policy store prepares its own once per process, before its first write — awaited here), and the proof's own —
        // built as the service's startup builds them (FIX3 (8)).
        var policies = harness.Scope.PolicyRepository() as Diten.MdmService.Persistence.Repositories.ProductLegalEntityScopePolicyRepository;
        await policies!.WhenIndexesReadyAsync();
        _ = new ProductAbbreviationAllocationLedgerRepository(harness.Scope.Database, harness.Tenant);
        _ = new ProductAbbreviationHistoryRepository(harness.Scope.Database, harness.Tenant);
        _ = new ProductAbbreviationRegisterRepository(harness.Scope.Database, harness.Tenant);
        _ = new ProductDefinitionRevisionRepository(harness.Scope.Database, harness.Tenant);
        _ = new GskuRepository(harness.Scope.Database, harness.Tenant);
        _ = new LskuRepository(harness.Scope.Database, harness.Tenant);
        _ = new FinishedGoodRepository(harness.Scope.Database, harness.Tenant);
        await ProductLegalEntityScopeOperationalReadinessRepository.EnsureObjectIndexesAsync(harness.Scope.Database);

        var proof = ProductLegalEntityScopeOperationalReadinessRepository.ObjectFields
            .Where(item => item.Collection == collection).SelectMany(item => item.Fields).Distinct().ToArray();
        var expansion = ProductLegalEntityScopeOperationalReadinessRepository.Descendants
            .Where(item => item.Collection == collection).SelectMany(item => item.ParentFields).Distinct().ToArray();
        var ids = new BsonArray { new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) };
        // The planner chooses by a trial run: on an empty collection every plan ties. The tenant gets a few hundred
        // documents (none of them the written object), as a tenant in use has.
        var tenant = new BsonBinaryData(harness.Scope.TenantId, GuidRepresentation.Standard);
        await harness.Scope.Database.GetCollection<BsonDocument>(collection).InsertManyAsync(Enumerable.Range(0, 300).Select(_ =>
        {
            var document = new BsonDocument { { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) }, { "TenantId", tenant } };
            // The clause fields, and the fields the owners' unique indexes key on, each with its own value.
            foreach (var field in proof.Concat(expansion).Where(field => field != "_id")
                         .Concat(["CreationCommandId", "IdempotencyKey", "NormalizedAbbreviation", "AllocationIdempotencyKey", "CodeReservationId"]).Distinct())
                document[field] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
            document["CanonicalCode"] = "C-" + Guid.NewGuid().ToString("N");
            return document;
        }));
        try
        {
            if (proof.Length > 0)
                AssertIndexed(collection, proof, await ExplainAsync(harness, collection, proof, ids, tenant, sortById: true));
            if (expansion.Length > 0)
                AssertIndexed(collection, expansion, await ExplainAsync(harness, collection, expansion, ids, tenant, sortById: false));
            Assert.True(proof.Length + expansion.Length > 0);
        }
        finally
        {
            await harness.Scope.Database.GetCollection<BsonDocument>(collection).DeleteManyAsync(new BsonDocument("TenantId", tenant));
        }
    }

    private static async Task<BsonDocument> ExplainAsync(
        Harness harness, string collection, string[] fields, BsonArray ids, BsonBinaryData tenant, bool sortById)
    {
        var find = new BsonDocument
        {
            { "find", collection },
            { "filter", new BsonDocument
                {
                    { "TenantId", tenant },
                    { "$or", new BsonArray(fields.Select(field => new BsonDocument(field, new BsonDocument("$in", ids)))) }
                } }
        };
        if (sortById) find["sort"] = new BsonDocument("_id", 1);
        var explained = await harness.Scope.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument { { "explain", find }, { "verbosity", "queryPlanner" } });
        return explained["queryPlanner"]["winningPlan"].AsBsonDocument;
    }

    private static void AssertIndexed(string collection, string[] fields, BsonDocument winning)
    {
        var plan = winning.ToJson();
        var scans = IndexScans(winning).ToArray();
        Assert.DoesNotContain("COLLSCAN", plan, StringComparison.Ordinal);
        Assert.NotEmpty(scans);
        // A scan of the tenant's whole range of another (TenantId, …) index is a collection scan in disguise: every index
        // scan must bound the key it seeks by — the first key after TenantId (keys after that, e.g. a date, may stay open).
        foreach (var scan in scans)
        {
            var seek = scan["indexBounds"].AsBsonDocument.Elements.SkipWhile(item => item.Name == "TenantId").First();
            Assert.DoesNotContain("MinKey", seek.Value.ToJson(), StringComparison.Ordinal);
        }
        var used = scans.Select(scan => scan["indexName"].AsString).ToHashSet(StringComparer.Ordinal);
        foreach (var field in fields.Where(field => field != "_id"))
            Assert.True(ExpectedIndexes[(collection, field)].Any(used.Contains),
                $"{collection}.{field}: expected one of [{string.Join(", ", ExpectedIndexes[(collection, field)])}], plan used [{string.Join(", ", used)}]");
    }

    private static IEnumerable<BsonDocument> IndexScans(BsonDocument stage)
    {
        if (stage.GetValue("stage", "").AsString == "IXSCAN") yield return stage;
        if (stage.TryGetValue("inputStage", out var input) && input.IsBsonDocument)
            foreach (var scan in IndexScans(input.AsBsonDocument)) yield return scan;
        if (stage.TryGetValue("inputStages", out var inputs) && inputs.IsBsonArray)
            foreach (var child in inputs.AsBsonArray.Where(item => item.IsBsonDocument))
                foreach (var scan in IndexScans(child.AsBsonDocument)) yield return scan;
    }

    [Fact]
    public async Task The_hold_inventory_counts_a_rollout_written_before_FIX1()
    {
        // CT audit rule: a record without ProductWriterLeases / ProductWriterLeaseRevision / LastFenceAbort (written before
        // FIX1) holding a foreground lease must still be counted — the filter is "$ne null" / "size > 0", never Eq(false).
        await using var harness = await Harness.CreateAsync();
        var database = await HoldDatabaseAsync(harness);
        try
        {
            var rollouts = new ProductLegalEntityScopeRolloutStateRepository(database, harness.Tenant);
            Assert.True((await rollouts.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
                harness.Scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Start))).Succeeded);
            Assert.True((await rollouts.AcquireWriterLeaseAsync(harness.Lease(Guid.NewGuid()))).Acquired);
            await database.GetCollection<BsonDocument>("mdm_product_legal_entity_scope_rollout_states").UpdateOneAsync(
                new BsonDocument("TenantId", new BsonBinaryData(harness.Scope.TenantId, GuidRepresentation.Standard)),
                new BsonDocument("$unset", new BsonDocument
                {
                    { "ProductWriterLeases", "" }, { "ProductWriterLeaseRevision", "" }, { "LastFenceAbort", "" }
                }));

            var holds = await new ProductLegalEntityScopeHoldInventoryRepository(database).ReadAsync(Start.AddSeconds(1));

            Assert.Equal(new ProductLegalEntityScopeHoldInventory(null, 0, 1, 0), holds);
        }
        finally
        {
            await harness.Scope.Database.Client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName);
        }
    }

    // ── FIX3 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_baseline_bound_while_another_writer_had_read_the_list_survives_that_writers_write()
    {
        // FIX3 (1): before, the binding did not move the revision — Y, who had read the list before X bound, wrote the
        // stale list back and X's baseline was gone (a half-written product could later pass as clean).
        await using var harness = await Harness.CreateAsync();
        var x = await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()));
        var hash = new string('D', 64);
        var bound = false;
        harness.Rollouts.BeforeLeaseWrite = async _ =>
        {
            if (bound) return;
            bound = true;
            Assert.True(await harness.Scope.RolloutRepository().BindProductWriterLeaseBaselineAsync(x.Lease!.Token, x.Lease.Generation, hash));
        };

        var y = await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()));

        Assert.True(y.Acquired, y.FailureCode);
        Assert.Equal(hash, Assert.Single((await harness.Rollouts.GetAsync())!.ProductWriterLeases,
            lease => lease.Token == x.Lease!.Token).PreWriteStateHash);
    }

    [Fact]
    public async Task A_product_held_by_the_foreground_slot_refuses_a_product_writer()
    {
        // FIX3 (4a)
        await using var harness = await Harness.CreateAsync();
        var product = Guid.NewGuid();
        Assert.True((await harness.Rollouts.AcquireWriterLeaseAsync(harness.Lease(product))).Acquired);

        var writer = await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(product));

        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", writer.FailureCode);
    }

    [Fact]
    public async Task A_product_held_by_a_product_writer_refuses_the_foreground_slot()
    {
        // FIX3 (4b)
        await using var harness = await Harness.CreateAsync();
        var product = Guid.NewGuid();
        Assert.True((await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(product))).Acquired);

        var slot = await harness.Rollouts.AcquireWriterLeaseAsync(harness.Lease(product));

        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", slot.FailureCode);
    }

    [Theory]
    [InlineData("product")]
    [InlineData("slot")]
    public async Task A_slot_and_a_product_writer_of_one_product_that_both_read_before_writing_admit_exactly_one(string first)
    {
        // FIX3 (4c): both read the record before either writes; the one that writes second must see the first.
        await using var harness = await Harness.CreateAsync();
        var product = Guid.NewGuid();
        var other = harness.Scope.RolloutRepository();
        ProductLegalEntityScopeWriterLeaseResult? interleaved = null;
        harness.Rollouts.BeforeLeaseWrite = async kind =>
        {
            if (interleaved is not null) return;
            interleaved = kind == "product"
                ? await other.AcquireWriterLeaseAsync(harness.Lease(product))
                : await other.AcquireProductWriterLeaseAsync(harness.Lease(product));
        };

        var mine = first == "product"
            ? await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(product))
            : await harness.Rollouts.AcquireWriterLeaseAsync(harness.Lease(product));

        Assert.True(interleaved!.Acquired, interleaved.FailureCode);
        Assert.False(mine.Acquired);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", mine.FailureCode);   // FIX4 K2: the loser's code, pinned
        var rollout = (await harness.Rollouts.GetAsync())!;
        Assert.Equal(1, (rollout.ActiveWriterLease is null ? 0 : 1) + rollout.ProductWriterLeases.Count);
    }

    [Fact]
    public async Task Failed_creates_cannot_refuse_the_writer_of_an_unrelated_product_and_their_own_bucket_counts_its_overflow()
    {
        // FIX3 (3): before, 256 failed creates (leases naming no object) filled the one ceiling, and every product writer
        // of the tenant heard "this product is busy" — uncounted.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        for (var i = 0; i < ProductLegalEntityScopeRolloutState.MaximumObjectlessWriterLeases; i++)
        {
            var create = await harness.Coordinator().EnterAsync(new TestCreate(Guid.NewGuid()), default);
            Assert.True(create.IsAllowed, $"{i}: {create.FailureCode}");
            await harness.Coordinator().CompleteAsync(create, false, 409, false, default);
        }

        var product = await harness.Coordinator().EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);
        var overflow = await harness.Coordinator().EnterAsync(new TestCreate(Guid.NewGuid()), default);

        Assert.True(product.IsAllowed, product.FailureCode);
        Assert.False(overflow.IsAllowed);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_CAPACITY_EXCEEDED", overflow.FailureCode);
        Assert.Equal(503, overflow.StatusCode);
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseCapacityExceeded, "bucket", "objectless"));
    }

    [Theory]
    [InlineData("exception")]
    [InlineData("ambiguous")]
    [InlineData("failed_unverifiable")]
    [InlineData("failed_changed")]
    [InlineData("admission_exception")]
    public async Task A_writer_that_keeps_its_lease_says_why(string reason)
    {
        // FIX3 (2): one case per retained reason, driven through the coordinator as the pipeline drives it.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var product = Guid.NewGuid();
        switch (reason)
        {
            case "exception":
            case "ambiguous":
            {
                var write = await harness.Coordinator().EnterAsync(new TestWrite(product, Guid.NewGuid()), default);
                await harness.Coordinator().CompleteAsync(write, false, reason == "ambiguous" ? 202 : 500, reason == "exception", default);
                break;
            }
            case "failed_unverifiable":
            {
                var create = await harness.Coordinator().EnterAsync(new TestCreate(Guid.NewGuid()), default);
                await harness.Coordinator().CompleteAsync(create, false, 409, false, default);
                break;
            }
            case "failed_changed":
            {
                var write = await harness.Coordinator().EnterAsync(new TestWrite(product, Guid.NewGuid()), default);
                await harness.InsertProductAsync(product);
                await harness.Coordinator().CompleteAsync(write, false, 409, false, default);
                break;
            }
            default:
            {
                // FIX3 (2a): the lease is taken, then the proof read fails — no admission ever exists.
                var coordinator = harness.Coordinator(readiness: new ThrowingReadiness());
                await Assert.ThrowsAsync<TimeoutException>(() =>
                    coordinator.EnterAsync(new TestWrite(product, Guid.NewGuid()), default));
                break;
            }
        }

        Assert.Single((await harness.Rollouts.GetAsync())!.ProductWriterLeases);
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseRetained, "reason", reason));
    }

    [Fact]
    public async Task A_write_whose_lease_vanished_while_it_ran_says_the_release_was_refused()
    {
        // FIX3 (2b): before, the refused release after a success was dropped silently.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var write = await harness.Coordinator().EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);
        await harness.Scope.RolloutCollection.UpdateOneAsync(item => item.TenantId == harness.Scope.TenantId,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(item => item.ProductWriterLeases, []));

        await harness.Coordinator().CompleteAsync(write, true, 200, false, default);

        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseReleaseRefused, "by", "writer"));
    }

    [Fact]
    public async Task A_write_admitted_without_a_proof_of_its_objects_is_counted()
    {
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();

        var create = await harness.Coordinator().EnterAsync(new TestCreate(Guid.NewGuid()), default);

        Assert.True(create.IsAllowed);
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.BaselineUnverifiable));
    }

    [Fact]
    public async Task An_acquisition_that_loses_every_round_answers_contended_503_and_is_counted()
    {
        // FIX3 (10): the code, its counter and its status were untested; contention is the server's condition — 503.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        // Every round, just before this writer writes, another writer changes the list: each round is lost.
        var rollouts = new ProductLegalEntityScopeRolloutStateRepository(harness.Scope.Database, harness.Tenant) { LeaseAttempts = 2 };
        var other = harness.Scope.RolloutRepository();
        rollouts.BeforeLeaseWrite = async _ => Assert.True((await other.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()))).Acquired);

        var admission = await harness.Coordinator(rollouts).EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);

        Assert.False(admission.IsAllowed);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_CONTENDED", admission.FailureCode);
        Assert.Equal(503, admission.StatusCode);
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseContended, "by", "writer"));
    }

    [Fact]
    public async Task A_rollout_record_the_serializer_cannot_map_answers_503_on_writes_and_on_the_completeness_read()
    {
        // FIX3 (12): before, only InvalidOperationException was mapped — a BSON mapping failure was a 500, and the
        // completeness read was not guarded at all.
        await using var harness = await Harness.CreateAsync();
        await harness.Scope.Database.GetCollection<BsonDocument>("mdm_product_legal_entity_scope_rollout_states").UpdateOneAsync(
            new BsonDocument("TenantId", new BsonBinaryData(harness.Scope.TenantId, GuidRepresentation.Standard)),
            new BsonDocument("$set", new BsonDocument("Mode", new BsonDocument("not", "a mode"))));

        var write = await harness.Coordinator().EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);
        var completeness = await new Features.ProductLegalEntityScopes.Handlers.QueryHandlers.GetProductLegalEntityScopeCompletenessHandler(
                null!, null!, harness.Rollouts, harness.Clock)
            .Handle(new Features.ProductLegalEntityScopes.Queries.GetProductLegalEntityScopeCompletenessQuery(), default);

        Assert.Equal((503, "PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID"), (write.StatusCode, write.FailureCode));
        Assert.Equal((503, "PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID"), (completeness.StatusCode, completeness.Errors?.FirstOrDefault()));
    }

    [Fact]
    public async Task A_store_that_fails_the_rollout_read_answers_503_on_writes_and_on_the_completeness_read()
    {
        // FIX3 (12) / FIX4 (6): the store itself failing the read (a MongoException — connection, timeout) answers its
        // own coded 503 — "store unavailable", not "the rollout record is broken".
        await using var harness = await Harness.CreateAsync();
        var failing = new ReadFails(harness.Rollouts);

        var write = await harness.Coordinator(failing).EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);
        var completeness = await new Features.ProductLegalEntityScopes.Handlers.QueryHandlers.GetProductLegalEntityScopeCompletenessHandler(
                null!, null!, failing, harness.Clock)
            .Handle(new Features.ProductLegalEntityScopes.Queries.GetProductLegalEntityScopeCompletenessQuery(), default);

        Assert.Equal((503, "PRODUCT_SCOPE_ROLLOUT_STORE_UNAVAILABLE"), (write.StatusCode, write.FailureCode));
        Assert.Equal((503, "PRODUCT_SCOPE_ROLLOUT_STORE_UNAVAILABLE"), (completeness.StatusCode, completeness.Errors?.FirstOrDefault()));
    }

    private sealed class ReadFails(IProductLegalEntityScopeRolloutStateRepository inner) : DelegatingRolloutRepository(inner)
    {
        public override Task<ProductLegalEntityScopeRolloutState?> GetAsync(CancellationToken cancellationToken = default)
            => throw new MongoException("simulated store failure");
    }

    [Fact]
    public async Task The_completeness_read_on_a_record_that_cannot_be_read_answers_the_coded_503()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.BreakRolloutAsync();

        var completeness = await new Features.ProductLegalEntityScopes.Handlers.QueryHandlers.GetProductLegalEntityScopeCompletenessHandler(
                null!, null!, harness.Rollouts, harness.Clock)
            .Handle(new Features.ProductLegalEntityScopes.Queries.GetProductLegalEntityScopeCompletenessQuery(), default);

        Assert.Equal((503, "PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID"), (completeness.StatusCode, completeness.Errors?.FirstOrDefault()));
    }

    [Fact]
    public async Task A_fence_waits_out_the_clock_drift_margin_of_a_lease()
    {
        // FIX3 (14): the margin was proven only on product acquisition — here on the fence.
        await using var harness = await Harness.CreateAsync();
        Assert.True((await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()))).Acquired);
        var rollout = (await harness.Rollouts.GetAsync())!;

        var early = await harness.Rollouts.AcquireFenceAsync(Fence(Start.AddSeconds(Gone - 2)), rollout.Id, rollout.Version, ProductLegalEntityScopeRolloutMode.Preparation);
        var late = await harness.Rollouts.AcquireFenceAsync(Fence(Start.AddSeconds(Gone)), rollout.Id, rollout.Version, ProductLegalEntityScopeRolloutMode.Preparation);

        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_ACTIVE", early.FailureCode);
        Assert.True(late.Acquired, late.FailureCode);
    }

    [Fact]
    public async Task The_foreground_slot_waits_out_the_clock_drift_margin_then_is_taken_over_and_the_old_command_is_refused()
    {
        // FIX3 (14): skew, takeover and the refused repeat after a takeover — on the slot.
        await using var harness = await Harness.CreateAsync();
        var held = await harness.Rollouts.AcquireWriterLeaseAsync(harness.Lease(Guid.NewGuid()));
        Assert.True(held.Acquired);

        harness.Clock.Now = Start.AddSeconds(Gone - 2);
        var early = await harness.Rollouts.AcquireWriterLeaseAsync(harness.Lease(Guid.NewGuid()));
        harness.Clock.Now = Start.AddSeconds(Gone);
        var takeover = await harness.Rollouts.AcquireWriterLeaseAsync(harness.Lease(Guid.NewGuid()));
        var repeat = await harness.Rollouts.AcquireWriterLeaseAsync(harness.SameCommand(held.Lease!));

        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", early.FailureCode);
        Assert.True(takeover.Acquired, takeover.FailureCode);
        Assert.Equal(1, takeover.TakenOver);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", repeat.FailureCode);
    }

    [Fact]
    public async Task The_gauge_worker_counts_a_failed_refresh_and_keeps_the_last_values_whose_age_then_shows()
    {
        // FIX3 (7): before, a read failure kept the last values with no sign that they were stale.
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        // The age gauge reads the real clock (it reports staleness as an operator sees it): the last good refresh was ten
        // minutes ago.
        var observed = DateTimeOffset.UtcNow.AddMinutes(-10);
        ProductLegalEntityScopeRolloutMetrics.RecordHolds(new(null, 0, 3, 1), observed);
        try
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
                .AddScoped<IProductLegalEntityScopeHoldInventoryRepository, ThrowingHolds>(services);
            using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
            var worker = new Diten.MdmService.Api.Services.ProductLegalEntityScopes.ProductLegalEntityScopeHoldGaugeWorker(
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(provider),
                new ManualClock(observed.AddMinutes(5)),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.MdmService.Api.Services.ProductLegalEntityScopes.ProductLegalEntityScopeHoldGaugeWorker>.Instance);

            await worker.RefreshAsync(default);

            Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.HoldRefreshFailed));
            Assert.Equal(observed, ProductLegalEntityScopeRolloutMetrics.HoldsObservedAtUtc);
            Assert.Equal(3, metrics.Observe(ProductLegalEntityScopeRolloutMetrics.LeasesHeld, "state", "running"));
            Assert.True(metrics.Observe(ProductLegalEntityScopeRolloutMetrics.HoldsObservedAgeSeconds) > 0);
        }
        finally
        {
            ProductLegalEntityScopeRolloutMetrics.RecordHolds(ProductLegalEntityScopeRolloutMetrics.HoldSnapshot.Empty, Start);
        }
    }

    private sealed class ThrowingHolds : IProductLegalEntityScopeHoldInventoryRepository
    {
        public Task<ProductLegalEntityScopeHoldInventory> ReadAsync(DateTimeOffset serverNowUtc, CancellationToken cancellationToken = default)
            => throw new TimeoutException("store unavailable");
    }

    /// <summary>The proof of the write's objects cannot be read (the store timed out).</summary>
    private sealed class ThrowingReadiness : IProductLegalEntityScopeOperationalReadinessRepository
    {
        public Task<ProductLegalEntityScopeOperationalReadiness> InspectAsync(int maximumSampleSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopeInventorySnapshot> CaptureInventorySnapshotAsync(ProductLegalEntityScopeInventorySnapshotRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopeObjectState> CaptureObjectStateHashAsync(IReadOnlyCollection<Guid> objectIds, CancellationToken cancellationToken = default)
            => throw new TimeoutException("store unavailable");
    }

    // ── FIX4 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_lease_write_that_landed_but_lost_its_answer_is_ambiguous_kept_and_counted()
    {
        // FIX4 (2): before, the lost answer of a landed write was "the rollout record is broken" (503 ROLLOUT_INVALID),
        // and the lease it left in the list was counted nowhere.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        harness.Rollouts.LeaseWriteExecutor = async write =>
        {
            await write();
            throw new MongoException("connection lost after the write");
        };

        var admission = await harness.Coordinator().EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);

        Assert.Equal((503, "PRODUCT_SCOPE_WRITER_LEASE_AMBIGUOUS"), (admission.StatusCode, admission.FailureCode));
        Assert.Single((await harness.Scope.RolloutRepository().GetAsync())!.ProductWriterLeases);
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseRetained, "reason", "acquire_ambiguous"));
        Assert.Equal(0, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.RolloutUnreadable));
    }

    [Fact]
    public async Task An_invalid_lease_request_a_store_timeout_and_a_lost_write_answer_three_different_ways()
    {
        // FIX4 (6): before, all three were "503 ROLLOUT_INVALID, unreadable" (and the timeout a 500).
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();

        harness.Clock.Now = new DateTimeOffset(2026, 10, 7, 11, 0, 0, TimeSpan.FromHours(2));   // a defect: not UTC
        var invalid = await harness.Coordinator().EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);
        harness.Clock.Now = Start;
        var timeout = await harness.Coordinator(new ReadTimesOut(harness.Rollouts)).EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);
        harness.Rollouts.LeaseWriteExecutor = async write => { await write(); throw new TimeoutException("answer lost"); };
        var lost = await harness.Coordinator().EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);

        Assert.Equal((500, "PRODUCT_SCOPE_WRITER_LEASE_REQUEST_INVALID"), (invalid.StatusCode, invalid.FailureCode));
        Assert.Equal((503, "PRODUCT_SCOPE_ROLLOUT_STORE_UNAVAILABLE"), (timeout.StatusCode, timeout.FailureCode));
        Assert.Equal((503, "PRODUCT_SCOPE_WRITER_LEASE_AMBIGUOUS"), (lost.StatusCode, lost.FailureCode));
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseRequestInvalid));
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.RolloutStoreUnavailable, "surface", "write_admission"));
        Assert.Equal(0, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.RolloutUnreadable));
    }

    private sealed class ReadTimesOut(IProductLegalEntityScopeRolloutStateRepository inner) : DelegatingRolloutRepository(inner)
    {
        public override Task<ProductLegalEntityScopeRolloutState?> GetAsync(CancellationToken cancellationToken = default)
            => throw new TimeoutException("server selection timed out");
    }

    [Fact]
    public async Task A_binding_that_throws_after_the_lease_was_taken_keeps_the_lease_and_counts_it()
    {
        // FIX4 K2: the binding branch of FIX3 (2a) was untested.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            harness.Coordinator(new ProductBindThrows(harness.Rollouts)).EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default));

        Assert.Single((await harness.Rollouts.GetAsync())!.ProductWriterLeases);
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseRetained, "reason", "admission_exception"));
    }

    private sealed class ProductBindThrows(IProductLegalEntityScopeRolloutStateRepository inner) : DelegatingRolloutRepository(inner)
    {
        public override Task<bool> BindProductWriterLeaseBaselineAsync(Guid token, long generation, string preWriteStateHash,
            CancellationToken cancellationToken = default) => throw new TimeoutException("store lost");
    }

    [Fact]
    public async Task A_failed_write_that_changed_nothing_but_whose_lease_vanished_says_the_release_was_refused()
    {
        // FIX4 K2: the release_refused of the unchanged path was untested.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var write = await harness.Coordinator().EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);
        await harness.Scope.RolloutCollection.UpdateOneAsync(item => item.TenantId == harness.Scope.TenantId,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(item => item.ProductWriterLeases, []));

        await harness.Coordinator().CompleteAsync(write, false, 409, false, default);

        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseReleaseRefused, "by", "writer"));
    }

    [Fact]
    public async Task A_full_product_bucket_answers_capacity_503_and_counts_the_product_bucket()
    {
        // FIX4 K2: the bucket=product capacity counter was untested.
        await using var harness = await Harness.CreateAsync();
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        for (var i = 0; i < ProductLegalEntityScopeRolloutState.MaximumProductWriterLeases; i++)
            Assert.True((await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()))).Acquired);

        var admission = await harness.Coordinator().EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);

        Assert.Equal((503, "PRODUCT_SCOPE_WRITER_LEASE_CAPACITY_EXCEEDED"), (admission.StatusCode, admission.FailureCode));
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.LeaseCapacityExceeded, "bucket", "product"));
    }

    [Fact]
    public async Task A_release_between_a_fences_read_and_its_write_does_not_make_the_fence_conflict()
    {
        // FIX4 K3: before, any binding or release in that window answered FENCE_CONFLICT.
        await using var harness = await Harness.CreateAsync();
        var stale = await harness.Rollouts.AcquireProductWriterLeaseAsync(harness.Lease(Guid.NewGuid()));
        var rollout = (await harness.Rollouts.GetAsync())!;
        var released = false;
        harness.Rollouts.BeforeLeaseWrite = async kind =>
        {
            if (kind != "fence" || released) return;
            released = true;
            Assert.True(await harness.Scope.RolloutRepository().ReleaseProductWriterLeaseAsync(stale.Lease!.Token, stale.Lease.Generation));
        };

        var fenced = await harness.Rollouts.AcquireFenceAsync(Fence(Start.AddSeconds(Gone)), rollout.Id, rollout.Version,
            ProductLegalEntityScopeRolloutMode.Preparation);

        Assert.True(released);
        Assert.True(fenced.Acquired, fenced.FailureCode);
    }

    [Fact]
    public async Task A_client_that_cancels_after_its_write_landed_still_frees_the_lease()
    {
        // FIX4 K5: the release runs on its own bounded token — the request's cancellation no longer leaves it held.
        await using var harness = await Harness.CreateAsync();
        var write = await harness.Coordinator().EnterAsync(new TestWrite(Guid.NewGuid(), Guid.NewGuid()), default);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await harness.Coordinator().CompleteAsync(write, true, 200, false, cancelled.Token);

        Assert.Empty((await harness.Rollouts.GetAsync())!.ProductWriterLeases);
    }

    [Fact]
    public async Task The_proof_index_build_never_fails_the_start_and_is_retried_until_it_succeeds()
    {
        // FIX4 (3): before, a server-selection TimeoutException (the driver's own) escaped StartAsync and the host did
        // not start; a failed build was never retried; nothing showed that the indexes were missing.
        using var metrics = new ProductLegalEntityScopeMetricRecorder();
        var attempts = 0;
        var initializer = new Diten.MdmService.Api.Services.ProductLegalEntityScopes.ProductLegalEntityScopeProofIndexInitializer(
            _ => ++attempts == 1 ? throw new TimeoutException("server selection timed out") : Task.CompletedTask,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.MdmService.Api.Services.ProductLegalEntityScopes.ProductLegalEntityScopeProofIndexInitializer>.Instance)
        {
            RetryInterval = TimeSpan.FromMilliseconds(20)
        };

        await initializer.StartAsync(default);
        await initializer.Running.WaitAsync(TimeSpan.FromSeconds(10));
        await initializer.StopAsync(default);

        Assert.Equal(2, attempts);
        Assert.Equal(1, metrics.Sum(ProductLegalEntityScopeRolloutMetrics.ProofIndexBuildFailed, "error", "TimeoutException"));
        Assert.True(ProductLegalEntityScopeRolloutMetrics.ProofIndexesBuilt);
    }

    [Fact]
    public async Task A_slow_proof_index_build_holds_the_start_only_for_its_bounded_wait()
    {
        var release = new TaskCompletionSource();
        var initializer = new Diten.MdmService.Api.Services.ProductLegalEntityScopes.ProductLegalEntityScopeProofIndexInitializer(
            async cancellationToken => await release.Task.WaitAsync(cancellationToken),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.MdmService.Api.Services.ProductLegalEntityScopes.ProductLegalEntityScopeProofIndexInitializer>.Instance)
        {
            StartWait = TimeSpan.FromMilliseconds(100)
        };

        var started = initializer.StartAsync(default);
        var finished = await Task.WhenAny(started, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(started, finished);
        Assert.False(ProductLegalEntityScopeRolloutMetrics.ProofIndexesBuilt);
        release.SetResult();
        await initializer.Running.WaitAsync(TimeSpan.FromSeconds(10));
        await initializer.StopAsync(default);
        Assert.True(ProductLegalEntityScopeRolloutMetrics.ProofIndexesBuilt);
    }

    [Fact]
    public async Task The_proof_index_initializer_builds_the_named_indexes_on_the_real_database()
    {
        await using var harness = await Harness.CreateAsync();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped(services, _ => harness.Scope.Database);
        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        var initializer = new Diten.MdmService.Api.Services.ProductLegalEntityScopes.ProductLegalEntityScopeProofIndexInitializer(
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(provider),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.MdmService.Api.Services.ProductLegalEntityScopes.ProductLegalEntityScopeProofIndexInitializer>.Instance);

        await initializer.StartAsync(default);
        await initializer.Running.WaitAsync(TimeSpan.FromSeconds(30));
        await initializer.StopAsync(default);

        foreach (var (collection, name, _) in ProductLegalEntityScopeOperationalReadinessRepository.ObjectIndexes)
        {
            var names = (await (await harness.Scope.Database.GetCollection<BsonDocument>(collection).Indexes.ListAsync()).ToListAsync())
                .Select(index => index["name"].AsString).ToArray();
            Assert.Contains(name, names);
        }
    }

    /// <summary>
    /// The hold inventory reads every tenant, so its tests need a database no other test writes: one FIXED name (the
    /// architecture guard forbids a database per run), emptied first and dropped after. Both tests are in this class's
    /// collection, never side by side.
    /// </summary>
    private const string HoldDatabaseName = "diten_mdm_product_scope_hold_itest";

    private static async Task<IMongoDatabase> HoldDatabaseAsync(Harness harness)
    {
        var database = harness.Scope.Database.Client.GetDatabase(HoldDatabaseName);
        await database.GetCollection<BsonDocument>("mdm_product_legal_entity_scope_rollout_states")
            .DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        return database;
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

    /// <summary>A create as the pipeline sees one: it names no existing object, only its own command.</summary>
    public sealed record TestCreate(Guid CommandId) : IProductLegalEntityScopeInventoryMutation;

    /// <summary>The baseline cannot be bound (the lease was taken meanwhile): the refusal keeps the lease.</summary>
    private sealed class BindRefused(IProductLegalEntityScopeRolloutStateRepository inner) : DelegatingRolloutRepository(inner)
    {
        public override Task<bool> BindProductWriterLeaseBaselineAsync(Guid token, long generation, string preWriteStateHash, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

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

        public ProductLegalEntityScopeWriteFenceCoordinator Coordinator(
            IProductLegalEntityScopeRolloutStateRepository? rollouts = null,
            IProductLegalEntityScopeOperationalReadinessRepository? readiness = null) => new(
            rollouts ?? Rollouts,
            readiness ?? new ProductLegalEntityScopeOperationalReadinessRepository(Scope.Database, Tenant),
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

        public ProductLegalEntityScopeWriterLease SameCommand(ProductLegalEntityScopeWriterLease lease) => new()
        {
            Token = Guid.NewGuid(), Generation = 1, CommandId = lease.CommandId, ActorId = lease.ActorId,
            MutationKind = lease.MutationKind, PayloadFingerprint = lease.PayloadFingerprint, Owner = lease.Owner,
            AcquiredAtUtc = Clock.Now, ExpiresAtUtc = Clock.Now.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds),
            ObjectKeys = [.. lease.ObjectKeys]
        };

        public Task InsertRawAsync(string collection, Guid id, params (string Field, Guid Value)[] fields)
        {
            var document = new BsonDocument
            {
                { "_id", new BsonBinaryData(id, GuidRepresentation.Standard) },
                { "TenantId", new BsonBinaryData(Scope.TenantId, GuidRepresentation.Standard) }
            };
            foreach (var (field, value) in fields) document[field] = new BsonBinaryData(value, GuidRepresentation.Standard);
            return Scope.Database.GetCollection<BsonDocument>(collection).InsertOneAsync(document);
        }

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
            foreach (var collection in new[] { "mdm_product_definition_revisions", "mdm_gskus" })
                await Scope.Database.GetCollection<BsonDocument>(collection).DeleteManyAsync(
                    new BsonDocument("TenantId", new BsonBinaryData(Scope.TenantId, GuidRepresentation.Standard)));
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
