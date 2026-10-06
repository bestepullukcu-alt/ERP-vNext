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
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", beyond.FailureCode);
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

    [Theory]
    [InlineData("mdm_product_abbreviation_allocation_ledger")]
    [InlineData("mdm_product_abbreviation_history")]
    [InlineData("mdm_product_abbreviation_register")]
    [InlineData("mdm_product_legal_entity_scope_policies")]
    public async Task Every_clause_of_the_object_proof_query_uses_an_index(string collection)
    {
        // FIX2 (9) — measured with explain(): the proof of a write reads its objects by _id OR parent id. One clause
        // without an index turns the whole $or into a collection scan, on every product write.
        await using var harness = await Harness.CreateAsync();
        // The owners' own indexes, as production has them (each repository creates its indexes when built).
        _ = harness.Scope.PolicyRepository();
        _ = new ProductAbbreviationAllocationLedgerRepository(harness.Scope.Database, harness.Tenant);
        _ = new ProductAbbreviationHistoryRepository(harness.Scope.Database, harness.Tenant);
        _ = new ProductAbbreviationRegisterRepository(harness.Scope.Database, harness.Tenant);
        await new ProductLegalEntityScopeOperationalReadinessRepository(harness.Scope.Database, harness.Tenant)
            .CaptureObjectStateHashAsync([Guid.NewGuid()]);
        var fields = ProductLegalEntityScopeOperationalReadinessRepository.ObjectFields
            .Concat(ProductLegalEntityScopeOperationalReadinessRepository.Descendants)
            .Where(item => item.Collection == collection)
            .SelectMany(item => item.Item2).Distinct().ToArray();
        var ids = new BsonArray { new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) };
        // The planner chooses by a trial run: on an empty collection every plan ties. The tenant gets a few hundred
        // documents (none of them the written object), as a tenant in use has — then only a plan that reads by each
        // clause's own index reads nothing.
        var tenant = new BsonBinaryData(harness.Scope.TenantId, GuidRepresentation.Standard);
        await harness.Scope.Database.GetCollection<BsonDocument>(collection).InsertManyAsync(Enumerable.Range(0, 300).Select(_ =>
        {
            var document = new BsonDocument { { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) }, { "TenantId", tenant } };
            // The clause fields, and the fields the owners' unique indexes key on, each with its own value.
            foreach (var field in fields.Where(field => field != "_id")
                         .Concat(["CreationCommandId", "IdempotencyKey", "NormalizedAbbreviation", "AllocationIdempotencyKey"]))
                document[field] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
            return document;
        }));

        var explained = await harness.Scope.Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "explain", new BsonDocument
                {
                    { "find", collection },
                    { "filter", new BsonDocument
                        {
                            { "TenantId", new BsonBinaryData(harness.Scope.TenantId, GuidRepresentation.Standard) },
                            { "$or", new BsonArray(fields.Select(field => new BsonDocument(field, new BsonDocument("$in", ids)))) }
                        } }
                } },
            { "verbosity", "queryPlanner" }
        });
        var winning = explained["queryPlanner"]["winningPlan"].AsBsonDocument;
        var plan = winning.ToJson();
        var scans = IndexScans(winning).ToArray();

        await harness.Scope.Database.GetCollection<BsonDocument>(collection).DeleteManyAsync(new BsonDocument("TenantId", tenant));

        Assert.DoesNotContain("COLLSCAN", plan, StringComparison.Ordinal);
        Assert.NotEmpty(scans);
        // A scan of the tenant's whole range of another (TenantId, …) index is a collection scan in disguise: every index
        // scan must bound the key it seeks by — the first key after TenantId (keys after that, e.g. a date, may stay open).
        foreach (var scan in scans)
        {
            var seek = scan["indexBounds"].AsBsonDocument.Elements.SkipWhile(item => item.Name == "TenantId").First();
            Assert.DoesNotContain("MinKey", seek.Value.ToJson(), StringComparison.Ordinal);
        }
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

        public ProductLegalEntityScopeWriteFenceCoordinator Coordinator(IProductLegalEntityScopeRolloutStateRepository? rollouts = null) => new(
            rollouts ?? Rollouts,
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
