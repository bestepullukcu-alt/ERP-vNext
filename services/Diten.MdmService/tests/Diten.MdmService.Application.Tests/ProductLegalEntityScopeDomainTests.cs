using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeDomainTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ProductId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ActorId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GroupWide_is_explicit_empty_and_Scoped_is_sorted_unique_nonempty()
    {
        var groupWide = Create(ProductLegalEntityScopeMode.GroupWide, []);
        Assert.Empty(Assert.Single(groupWide.ScopePeriods).LegalEntityIds);

        var second = Guid.Parse("20000000-0000-0000-0000-000000000000");
        var first = Guid.Parse("10000000-0000-0000-0000-000000000000");
        var scoped = Create(ProductLegalEntityScopeMode.Scoped, [second, first]);
        Assert.Equal([first, second], Assert.Single(scoped.ScopePeriods).LegalEntityIds);
        Assert.IsAssignableFrom<IAuditIntentAggregate>(scoped);
        Assert.Empty(scoped.AuditIntents);
        Assert.Empty(scoped.AuditIntentReceipts);
    }

    [Fact]
    public void Snapshot_contract_rejects_invalid_mode_empty_duplicate_and_over_200_without_truncation()
    {
        Assert.Throws<ArgumentException>(() =>
            Create(ProductLegalEntityScopeMode.GroupWide, [Guid.NewGuid()]));
        Assert.Throws<ArgumentException>(() =>
            Create(ProductLegalEntityScopeMode.Scoped, []));
        var duplicate = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() =>
            Create(ProductLegalEntityScopeMode.Scoped, [duplicate, duplicate]));
        Assert.Throws<ArgumentException>(() =>
            Create(ProductLegalEntityScopeMode.Scoped, [Guid.Empty]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(
                ProductLegalEntityScopeMode.Scoped,
                Enumerable.Range(0, 201).Select(_ => Guid.NewGuid())));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create((ProductLegalEntityScopeMode)999, []));
    }

    [Fact]
    public void Replace_and_end_use_server_time_half_open_periods_and_expected_int_version()
    {
        var firstLegalEntity = Guid.NewGuid();
        var secondLegalEntity = Guid.NewGuid();
        var policy = Create(ProductLegalEntityScopeMode.Scoped, [firstLegalEntity]);

        policy.ReplaceCurrent(
            0,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.Scoped,
            [secondLegalEntity],
            ActorId,
            Now.AddHours(1));

        Assert.Equal(1, policy.Version);
        Assert.Equal(Now.AddHours(1), policy.ScopePeriods[0].EffectiveToUtc);
        Assert.Equal(policy.ScopePeriods[1].CommandId, policy.ScopePeriods[0].EndCommandId);
        Assert.Equal(ActorId, policy.ScopePeriods[0].EndedByActorId);
        Assert.Equal(Now.AddHours(1), policy.ScopePeriods[0].EndedAtUtc);
        Assert.Equal(Now.AddHours(1), policy.ScopePeriods[1].EffectiveFromUtc);
        Assert.Equal(secondLegalEntity, Assert.Single(policy.GetEffectivePeriod(Now.AddHours(1))!.LegalEntityIds));
        Assert.Throws<InvalidOperationException>(() => policy.ReplaceCurrent(
            0,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.GroupWide,
            [],
            ActorId,
            Now.AddHours(2)));

        var endCommandId = Guid.NewGuid();
        policy.EndCurrent(1, endCommandId, ActorId, Now.AddHours(2));
        Assert.Equal(2, policy.Version);
        Assert.Null(policy.GetEffectivePeriod(Now.AddHours(2)));
        Assert.Equal(endCommandId, policy.ScopePeriods[1].EndCommandId);
        Assert.Equal(ActorId, policy.ScopePeriods[1].EndedByActorId);
        Assert.Equal(Now.AddHours(2), policy.ScopePeriods[1].EndedAtUtc);
        Assert.Throws<InvalidOperationException>(() => policy.EndCurrent(
            2,
            endCommandId,
            ActorId,
            Now.AddHours(3)));
    }

    [Fact]
    public void At_most_100_periods_and_one_current_period_are_enforced()
    {
        var policy = Create(ProductLegalEntityScopeMode.GroupWide, []);
        for (var index = 1; index < ProductLegalEntityScopePolicy.MaximumPeriodsPerPolicy; index++)
        {
            policy.ReplaceCurrent(
                index - 1,
                Guid.NewGuid(),
                ProductLegalEntityScopeMode.GroupWide,
                [],
                ActorId,
                Now.AddMinutes(index));
        }

        Assert.Equal(100, policy.ScopePeriods.Count);
        Assert.Throws<InvalidOperationException>(() => policy.ReplaceCurrent(
            99,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.GroupWide,
            [],
            ActorId,
            Now.AddMinutes(100)));

        policy.ScopePeriods[0].EffectiveToUtc = null;
        Assert.Throws<InvalidOperationException>(() => policy.EnsureValid(Now.AddMinutes(100)));
    }

    [Fact]
    public void Complete_BSON_budget_is_fixed_and_over_limit_fails_before_transition()
    {
        var budget = ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes;
        ProductLegalEntityScopePolicy.EnsureSerializedBsonSizeWithinLimit(budget);
        Assert.Throws<InvalidOperationException>(() =>
            ProductLegalEntityScopePolicy.EnsureSerializedBsonSizeWithinLimit(budget + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProductLegalEntityScopePolicy.EnsureSerializedBsonSizeWithinLimit(0));

        // Production Mongo serialization over the complete persisted document is a B-step proof.
        // A exposes only the immutable foundation budget and a reusable post-serialization guard.
    }

    [Fact]
    public void Future_or_non_UTC_periods_and_duplicate_command_identity_fail_closed()
    {
        var policy = Create(ProductLegalEntityScopeMode.GroupWide, []);
        var commandId = Guid.NewGuid();
        policy.ReplaceCurrent(
            0,
            commandId,
            ProductLegalEntityScopeMode.GroupWide,
            [],
            ActorId,
            Now.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => policy.ReplaceCurrent(
            1,
            commandId,
            ProductLegalEntityScopeMode.GroupWide,
            [],
            ActorId,
            Now.AddHours(2)));
        Assert.Throws<ArgumentException>(() => ProductLegalEntityScopePolicy.Create(
            TenantId,
            ProductId,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.GroupWide,
            [],
            ActorId,
            Now.ToOffset(TimeSpan.FromHours(3))));
    }

    [Fact]
    public void Incomplete_or_duplicate_end_command_evidence_fails_closed()
    {
        var policy = Create(ProductLegalEntityScopeMode.GroupWide, []);
        var endCommandId = Guid.NewGuid();
        policy.EndCurrent(0, endCommandId, ActorId, Now.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => policy.ReplaceCurrent(
            1,
            endCommandId,
            ProductLegalEntityScopeMode.GroupWide,
            [],
            ActorId,
            Now.AddHours(2)));

        policy.ScopePeriods[0].EndedByActorId = null;
        Assert.Throws<InvalidOperationException>(() => policy.EnsureValid(Now.AddHours(2)));
    }

    [Fact]
    public void Future_end_facts_and_noncanonical_period_order_fail_closed()
    {
        var policy = Create(ProductLegalEntityScopeMode.GroupWide, []);
        policy.ReplaceCurrent(
            0,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.GroupWide,
            [],
            ActorId,
            Now.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => policy.EnsureValid(Now.AddMinutes(30)));

        policy.ScopePeriods.Reverse();
        Assert.Throws<InvalidOperationException>(() => policy.EnsureValid(Now.AddHours(2)));
    }

    private static ProductLegalEntityScopePolicy Create(
        ProductLegalEntityScopeMode mode,
        IEnumerable<Guid> legalEntityIds) => ProductLegalEntityScopePolicy.Create(
            TenantId,
            ProductId,
            Guid.NewGuid(),
            mode,
            legalEntityIds,
            ActorId,
            Now);
}
