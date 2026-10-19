using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// MOD-0290-FU04 S4 FIX3 (minor: "the R2 fallback is sequential, up to 200 single reads") — measured on a counting policy
/// store: when the batch policy read fails, the batch is read again in groups of 20 and only the group that fails again
/// goes product by product. The decisions stay exactly the single path's (S4 FIX2 R2); the reads drop from 1 + 200 to
/// 1 + 10 + 20 for 200 products with one unreadable policy.
/// </summary>
public sealed class ProductScopeBatchFallbackTests
{
    [Theory]
    [InlineData(200, 1, 1 + 10, 20)]
    [InlineData(200, 2, 1 + 10, 40)] // two bad policies in two different groups
    [InlineData(20, 1, 1 + 1, 20)]
    [InlineData(5, 1, 1 + 1, 5)]
    public async Task S4_FIX3_A_failed_batch_is_read_again_in_groups_and_only_a_failing_group_goes_one_by_one(
        int products, int unreadable, int batchReads, int singleReads)
    {
        var tenant = new Tenant(Guid.NewGuid());
        var ids = Enumerable.Range(0, products).Select(_ => Guid.NewGuid()).ToArray();
        // One unreadable policy per group of 20, from the first group on.
        var broken = Enumerable.Range(0, unreadable).Select(i => ids[i * ProductLegalEntityScopeConsumerGuard.FallbackGroupSize]).ToHashSet();
        var store = new CountingPolicies(tenant.TenantId, ids, broken);
        var guard = new ProductLegalEntityScopeConsumerGuard(null!, store, null!, tenant);
        var context = Enforced(tenant.TenantId);

        var decisions = await guard.EvaluateManyAsync(context, ids);

        Assert.Equal(batchReads, store.BatchReads);
        Assert.Equal(singleReads, store.SingleReads);
        // The same decision per product as the single path gives it.
        store.Reset();
        foreach (var id in ids)
        {
            var single = await guard.EvaluateAsync(context, id);
            Assert.Equal((single.Allowed, single.Reason), (decisions[id].Allowed, decisions[id].Reason));
        }
        Assert.All(broken, id => Assert.False(decisions[id].Allowed));
        Assert.Contains(ids.Except(broken), id => decisions[id].Allowed);
    }

    // S4 FIX4: mixed policies in every group — group-wide, none, scoped to another legal entity, scoped to the subject's —
    // so a product decided with another product's policy answers differently from the single path.
    // The sabotage target "the group's id → policy mapping is shifted".
    [Fact]
    public async Task S4_FIX4_Mixed_policies_are_decided_per_product_in_the_group_fallback()
    {
        var tenant = new Tenant(Guid.NewGuid());
        var subjectLegalEntity = Guid.NewGuid();
        var ids = Enumerable.Range(0, 60).Select(_ => Guid.NewGuid()).ToArray();
        ProductLegalEntityScopePolicy? PolicyOf(int i) => (i % 4) switch
        {
            0 => Policy(tenant.TenantId, ids[i], ProductLegalEntityScopeMode.GroupWide, []),
            1 => null,
            2 => Policy(tenant.TenantId, ids[i], ProductLegalEntityScopeMode.Scoped, [Guid.NewGuid()]),
            _ => Policy(tenant.TenantId, ids[i], ProductLegalEntityScopeMode.Scoped, [subjectLegalEntity])
        };
        var policies = Enumerable.Range(0, ids.Length).Select(i => (ids[i], PolicyOf(i)))
            .Where(x => x.Item2 is not null).ToDictionary(x => x.Item1, x => x.Item2!);
        var store = new CountingPolicies(policies, [ids[5]]);
        var guard = new ProductLegalEntityScopeConsumerGuard(null!, store, null!, tenant);
        var context = Enforced(tenant.TenantId, subjectLegalEntity);

        var decisions = await guard.EvaluateManyAsync(context, ids);

        foreach (var id in ids)
        {
            var single = await guard.EvaluateAsync(context, id);
            Assert.Equal((id, single.Allowed, single.Reason), (id, decisions[id].Allowed, decisions[id].Reason));
        }
        // Every group of 20 holds both answers, so a shifted mapping cannot agree by chance.
        foreach (var group in ids.Chunk(ProductLegalEntityScopeConsumerGuard.FallbackGroupSize))
        {
            Assert.Contains(group, id => decisions[id].Allowed);
            Assert.Contains(group, id => !decisions[id].Allowed);
        }
    }

    [Fact]
    public async Task S4_FIX3_A_readable_batch_is_one_read()
    {
        var tenant = new Tenant(Guid.NewGuid());
        var ids = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToArray();
        var store = new CountingPolicies(tenant.TenantId, ids, []);

        await new ProductLegalEntityScopeConsumerGuard(null!, store, null!, tenant).EvaluateManyAsync(Enforced(tenant.TenantId), ids);

        Assert.Equal((1, 0), (store.BatchReads, store.SingleReads));
    }

    private static ProductLegalEntityScopePolicy Policy(Guid tenantId, Guid product, ProductLegalEntityScopeMode mode, Guid[] legalEntities) =>
        ProductLegalEntityScopePolicy.Create(tenantId, product, Guid.NewGuid(), mode, legalEntities, Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));

    private static ProductLegalEntityScopeConsumerContext Enforced(Guid tenantId, Guid? subjectLegalEntity = null)
    {
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(tenantId, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        return new(tenantId, ProductLegalEntityScopeRolloutMode.Enforced, rollout, DateTimeOffset.UtcNow,
            [subjectLegalEntity ?? Guid.NewGuid()]);
    }

    // Every product has a group-wide policy; a "broken" one cannot be read (as the strict store throws on a bad document).
    private sealed class CountingPolicies(Dictionary<Guid, ProductLegalEntityScopePolicy> policies, HashSet<Guid> broken)
        : IProductLegalEntityScopePolicyRepository
    {
        // Every product group-wide (FIX3's shape).
        public CountingPolicies(Guid tenantId, IEnumerable<Guid> ids, HashSet<Guid> broken)
            : this(ids.ToDictionary(id => id, id => Policy(tenantId, id, ProductLegalEntityScopeMode.GroupWide, [])), broken)
        {
        }

        private readonly Dictionary<Guid, ProductLegalEntityScopePolicy> _policies = policies;

        public int BatchReads { get; private set; }
        public int SingleReads { get; private set; }

        public void Reset() => (BatchReads, SingleReads) = (0, 0);

        public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(Guid globalProductId, CancellationToken cancellationToken = default)
        {
            SingleReads++;
            if (broken.Contains(globalProductId)) throw new FormatException("unreadable policy");
            return Task.FromResult(_policies.GetValueOrDefault(globalProductId));
        }

        public Task<IReadOnlyDictionary<Guid, ProductLegalEntityScopePolicy>> GetByGlobalProductIdsAsync(
            IReadOnlyCollection<Guid> globalProductIds, CancellationToken cancellationToken = default)
        {
            BatchReads++;
            if (globalProductIds.Any(broken.Contains)) throw new FormatException("unreadable policy in the batch");
            return Task.FromResult<IReadOnlyDictionary<Guid, ProductLegalEntityScopePolicy>>(
                globalProductIds.Where(_policies.ContainsKey).ToDictionary(id => id, id => _policies[id]));
        }

        public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(ProductLegalEntityScopePolicy policy, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(ProductLegalEntityScopePolicy policy, int expectedVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(IReadOnlyCollection<Guid> globalProductIds,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId { get; private set; } = id;
        public bool IsResolved => true;
        public void SetTenant(Guid value) => TenantId = value;
    }
}
