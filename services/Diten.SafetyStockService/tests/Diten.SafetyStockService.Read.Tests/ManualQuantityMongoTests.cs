using Diten.SafetyStockService.Application;
using Diten.SafetyStockService.Domain;
using Diten.SafetyStockService.Persistence;
using MongoDB.Driver;
using Xunit;

namespace Diten.SafetyStockService.Read.Tests;

public sealed class ManualQuantityMongoTests
{
    private static readonly ActorSeriesScope Scope = new(new HashSet<SeriesKey>(), true);
    private static readonly Guid Sku = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Warehouse = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [PolicyMutationMongoFact]
    public async Task ManualAmount_RequiresIndependentApprovalAndHasOneEffectiveRecord()
    {
        var context = PolicyMutationMongoTests.Context();
        var store = new ManualQuantityMongoStore(context, new PolicyAuditAppender(context));
        var (tenant, company, author, reviewer, policy) = await ApprovedPolicyAsync(context);
        var create = Create(tenant, company, author, policy, 12m);
        var first = await store.ExecuteAsync(create, Scope, "EA", default);
        Assert.Equal(ManualQuantityStatus.Created, first.Status);
        Assert.Equal(first, await store.ExecuteAsync(create, Scope, "EA", default));

        var submit = Transition(ManualQuantityOperation.Submit, author, first.Value!, tenant, company);
        var submitted = await store.ExecuteAsync(submit, Scope, "EA", default);
        Assert.Equal("InReview", submitted.Value?.State);
        Assert.Equal(ManualQuantityStatus.Conflict,
            (await store.ExecuteAsync(Transition(ManualQuantityOperation.Approve, author,
                submitted.Value!, tenant, company), Scope, "EA", default)).Status);
        var approved = await store.ExecuteAsync(Transition(ManualQuantityOperation.Approve, reviewer,
            submitted.Value!, tenant, company), Scope, "EA", default);
        Assert.Equal("Approved", approved.Value?.State);

        var found = await store.ReadEffectiveAsync(new(tenant, company, reviewer, Sku, Warehouse,
            new DateOnly(2026, 10, 19)), Scope, "EA", default);
        Assert.Equal(ManualQuantityStatus.Found, found.Status);
        Assert.Equal(12m, found.Value?.Quantity);
        Assert.Equal(ManualQuantityStatus.Found,
            (await store.ReadEffectiveAsync(new(tenant, company, reviewer, Sku, Warehouse,
                new DateOnly(2026, 10, 21)), Scope, "EA", default)).Status);
        Assert.Equal(3, await context.Audit.CountDocumentsAsync(a => a.TenantId == tenant.ToString("D") &&
            a.RevisionId == first.Value!.Id.ToString("D")));

        var second = await store.ExecuteAsync(Create(tenant, company, author, policy, 14m), Scope, "EA", default);
        Assert.Equal(ManualQuantityStatus.Created, second.Status);
        var secondSubmitted = await store.ExecuteAsync(Transition(ManualQuantityOperation.Submit, author,
            second.Value!, tenant, company), Scope, "EA", default);
        Assert.Equal(ManualQuantityStatus.Conflict,
            (await store.ExecuteAsync(Transition(ManualQuantityOperation.Approve, reviewer,
                secondSubmitted.Value!, tenant, company), Scope, "EA", default)).Status);
        Assert.Equal(1, await context.ManualQuantities.CountDocumentsAsync(q =>
            q.TenantId == tenant.ToString("D") && q.State == "Approved"));
    }

    [PolicyMutationMongoTheory]
    [InlineData(ManualQuantityOperation.Create)]
    [InlineData(ManualQuantityOperation.Submit)]
    [InlineData(ManualQuantityOperation.Approve)]
    public async Task AuditFailure_RollsBackQuantityAndPolicyLock(ManualQuantityOperation failed)
    {
        var context = PolicyMutationMongoTests.Context();
        var normal = new ManualQuantityMongoStore(context, new PolicyAuditAppender(context));
        var broken = new ManualQuantityMongoStore(context, new ThrowingAudit());
        var (tenant, company, author, reviewer, policy) = await ApprovedPolicyAsync(context);
        ManualQuantityCommand command;
        ManualQuantityReceipt? prior = null;
        if (failed == ManualQuantityOperation.Create)
            command = Create(tenant, company, author, policy, 10m);
        else
        {
            prior = (await normal.ExecuteAsync(Create(tenant, company, author, policy, 10m),
                Scope, "EA", default)).Value!;
            if (failed == ManualQuantityOperation.Approve)
                prior = (await normal.ExecuteAsync(Transition(ManualQuantityOperation.Submit, author,
                    prior, tenant, company), Scope, "EA", default)).Value!;
            command = Transition(failed, failed == ManualQuantityOperation.Approve ? reviewer : author,
                prior, tenant, company);
        }
        var beforeCount = await context.ManualQuantities.CountDocumentsAsync(q => q.TenantId == tenant.ToString("D"));
        var beforeAudit = await context.Audit.CountDocumentsAsync(a => a.TenantId == tenant.ToString("D"));
        var beforeVersion = (await context.Policies.Find(p => p.Id == policy.PolicyId.ToString("D")).FirstAsync()).Version;
        Assert.Equal(ManualQuantityStatus.Unavailable,
            (await broken.ExecuteAsync(command, Scope, "EA", default)).Status);
        Assert.Equal(beforeCount, await context.ManualQuantities.CountDocumentsAsync(q => q.TenantId == tenant.ToString("D")));
        Assert.Equal(beforeAudit, await context.Audit.CountDocumentsAsync(a => a.TenantId == tenant.ToString("D")));
        Assert.Equal(beforeVersion,
            (await context.Policies.Find(p => p.Id == policy.PolicyId.ToString("D")).FirstAsync()).Version);
        if (prior is not null)
            Assert.Equal(prior.State, (await context.ManualQuantities.Find(q =>
                q.Id == prior.Id.ToString("D")).FirstAsync()).State);
    }

    [PolicyMutationMongoFact]
    public async Task EffectiveRead_FailsClosedOnAuditTamperingAndScopeLoss()
    {
        var context = PolicyMutationMongoTests.Context();
        var store = new ManualQuantityMongoStore(context, new PolicyAuditAppender(context));
        var (tenant, company, author, reviewer, policy) = await ApprovedPolicyAsync(context);
        var first = (await store.ExecuteAsync(Create(tenant, company, author, policy, 6m),
            Scope, "EA", default)).Value!;
        var submitted = (await store.ExecuteAsync(Transition(ManualQuantityOperation.Submit, author,
            first, tenant, company), Scope, "EA", default)).Value!;
        Assert.Equal(ManualQuantityStatus.Updated,
            (await store.ExecuteAsync(Transition(ManualQuantityOperation.Approve, reviewer,
                submitted, tenant, company), Scope, "EA", default)).Status);
        var query = new EffectiveManualQuantityQuery(tenant, company, reviewer, Sku, Warehouse,
            new DateOnly(2026, 10, 19));
        Assert.Equal(ManualQuantityStatus.NotFound,
            (await store.ReadEffectiveAsync(query, new(new HashSet<SeriesKey>()), "EA", default)).Status);
        Assert.Equal(ManualQuantityStatus.Found,
            (await store.ReadEffectiveAsync(query, Scope, "EA", default)).Status);

        await context.Audit.UpdateOneAsync(a => a.RevisionId == first.Id.ToString("D") &&
            a.Operation == "safety-stock.manual-quantity-approved",
            Builders<PolicyAuditDocument>.Update.Set(a => a.ActorId, author.ToString("D")));
        Assert.Equal(ManualQuantityStatus.Unavailable,
            (await store.ReadEffectiveAsync(query, Scope, "EA", default)).Status);

        await context.Audit.UpdateOneAsync(a => a.RevisionId == first.Id.ToString("D") &&
            a.Operation == "safety-stock.manual-quantity-approved",
            Builders<PolicyAuditDocument>.Update.Set(a => a.ActorId, reviewer.ToString("D")));
        await context.ManualQuantities.UpdateOneAsync(q => q.Id == first.Id.ToString("D"),
            Builders<ManualQuantityDocument>.Update.Set(q => q.Quantity, 99m));
        Assert.Equal(ManualQuantityStatus.Unavailable,
            (await store.ReadEffectiveAsync(query, Scope, "EA", default)).Status);
    }

    private static async Task<(Guid Tenant, Guid Company, Guid Author, Guid Reviewer, PolicyMutationReceipt Policy)>
        ApprovedPolicyAsync(SafetyStockMongoContext context)
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var author = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var store = new MongoPolicyMutationStore(context, new PolicyAuditAppender(context));
        var created = (await store.ExecuteAsync(new(PolicyOperation.Create, tenant, company, author,
            Guid.Empty, Guid.Empty, Sku, Warehouse, 5, null, null, Guid.NewGuid().ToString("N")),
            Scope, default)).Receipt!;
        var submitted = (await store.ExecuteAsync(new(PolicyOperation.Submit, tenant, company, author,
            created.PolicyId, created.RevisionId, Sku, Warehouse, null, created.Version,
            null, Guid.NewGuid().ToString("N")), Scope, default)).Receipt!;
        var approved = (await store.ExecuteAsync(new(PolicyOperation.Approve, tenant, company, reviewer,
            submitted.PolicyId, submitted.RevisionId, Sku, Warehouse, null, submitted.Version,
            null, Guid.NewGuid().ToString("N")), Scope, default)).Receipt!;
        return (tenant, company, author, reviewer, approved);
    }

    private static ManualQuantityCommand Create(Guid tenant, Guid company, Guid actor,
        PolicyMutationReceipt policy, decimal value) =>
        new(ManualQuantityOperation.Create, tenant, company, actor, policy.PolicyId,
            policy.RevisionId, Guid.Empty, Sku, Warehouse, value,
            new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25), "Manual safety buffer",
            null, Guid.NewGuid().ToString("N"));

    private static ManualQuantityCommand Transition(ManualQuantityOperation operation, Guid actor,
        ManualQuantityReceipt amount, Guid tenant, Guid company) =>
        new(operation, tenant, company, actor, amount.PolicyId, amount.PolicyRevisionId,
            amount.Id, Sku, Warehouse, null, null, null, null,
            amount.Version, Guid.NewGuid().ToString("N"));

    private sealed class ThrowingAudit : IPolicyAuditAppender
    {
        public Task AppendAsync(IClientSessionHandle session, PolicyAuditDocument record, CancellationToken ct)
            => throw new InvalidOperationException("Intentional audit failure");
    }
}
