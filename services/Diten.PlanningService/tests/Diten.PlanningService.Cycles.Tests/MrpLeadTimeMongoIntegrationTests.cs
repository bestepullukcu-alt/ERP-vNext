using Diten.PlanningService.Application.Features.Mrp;
using Diten.PlanningService.Persistence.Features.Mrp;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class MrpLeadTimeMongoIntegrationTests
{
    private static readonly DateOnly From = new(2026, 10, 19);

    private static (LeadTimeMongoContext Context, LeadTimeCommandHandler Handler) Open()
    {
        var uri = Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")
            ?? throw new InvalidOperationException("Explicit test replica-set URI is required.");
        var url = new MongoUrl(uri);
        var servers = url.Servers.ToArray();
        if (servers.Length != 1 || servers[0].Host != "127.0.0.1" ||
            servers[0].Port is < 31994 or > 39994 || url.ReplicaSetName != "rs-mod0188" ||
            url.Username is not null || url.Password is not null)
            throw new InvalidOperationException("Only an explicit loopback test replica set is allowed.");
        var settings = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = uri,
                ["Mongo:SupplyChainDatabaseName"] = $"mod0189_tests_{Guid.NewGuid():N}"
            }).Build();
        var context = new LeadTimeMongoContext(settings);
        return (context, Handler(context));
    }

    private static LeadTimeCommandHandler Handler(LeadTimeMongoContext context, bool failAudit = false) =>
        new(new FixtureAuthority(), new LeadTimeMongoStore(context,
            failAudit ? new FailingAudit() : new MongoLeadTimeAuditAppender(context)));

    private static LeadTimeCommand Create(Guid tenant, Guid company, Guid sku, Guid warehouse, Guid actor) =>
        new(LeadTimeOperation.Create, tenant, company, sku, warehouse, actor,
            Guid.Empty, Guid.Empty, 9, From, null, null, null, Guid.NewGuid().ToString("N"));

    private static LeadTimeCommand Next(LeadTimeCommand seed, LeadTimeReceipt receipt,
        LeadTimeOperation operation, Guid actor, int? days = null, string? reason = null) =>
        seed with
        {
            Operation = operation, ActorId = actor, PolicyId = receipt.PolicyId,
            RevisionId = receipt.RevisionId, LeadTimeDays = days,
            EffectiveFrom = days is null ? null : From.AddDays(7),
            EffectiveUntil = null, ExpectedVersion = receipt.Version,
            Reason = reason, IdempotencyKey = Guid.NewGuid().ToString("N")
        };

    [Fact]
    public async Task ProductionDefaultAuthority_DeniesWithoutTouchingStore()
    {
        var handler = new LeadTimeCommandHandler(new DenyingAuthority(), new NeverStore());
        var command = Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(LeadTimeStatus.Forbidden, (await handler.HandleAsync(command)).Status);
        Assert.Equal(LeadTimeStatus.Invalid,
            (await handler.HandleAsync(command with { Operation = (LeadTimeOperation)999 })).Status);
    }

    [ManualDraftMongoFact]
    public async Task ApprovedLeadTimeReader_RequiresApprovedRevisionAndMatchingScope()
    {
        var (context, handler) = Open();
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var sku = Guid.NewGuid();
        var warehouse = Guid.NewGuid();
        var seed = Create(tenant, company, sku, warehouse, Guid.NewGuid());
        var reader = new MrpApprovedLeadTimeReader(context);

        var created = await handler.HandleAsync(seed);
        Assert.Equal(LeadTimeStatus.Created, created.Status);
        Assert.Null(await reader.ReadAsync(tenant, company, sku, warehouse,
            From, From.AddDays(6), CancellationToken.None));

        var submitted = await handler.HandleAsync(Next(seed, created.Receipt!,
            LeadTimeOperation.Submit, seed.ActorId));
        Assert.Equal(LeadTimeStatus.Updated, submitted.Status);
        var approved = await handler.HandleAsync(Next(seed, submitted.Receipt!,
            LeadTimeOperation.Approve, Guid.NewGuid()));
        Assert.Equal(LeadTimeStatus.Updated, approved.Status);

        var found = await reader.ReadAsync(tenant, company, sku, warehouse,
            From, From.AddDays(363), CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(created.Receipt!.RevisionId.ToString("D"), found.RevisionId);
        Assert.Equal(9, found.Days);
        Assert.Null(await reader.ReadAsync(tenant, Guid.NewGuid(), sku, warehouse,
            From, From.AddDays(363), CancellationToken.None));
        Assert.Null(await reader.ReadAsync(tenant, company, sku, Guid.NewGuid(),
            From, From.AddDays(363), CancellationToken.None));
    }

    [ManualDraftMongoFact]
    public async Task Lifecycle_IsDurableScopedAndAudited_WithIndependentReviewer()
    {
        var (context, handler) = Open();
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var planner = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var create = Create(tenant, company, Guid.NewGuid(), Guid.NewGuid(), planner);

        var created = await handler.HandleAsync(create);
        Assert.Equal(LeadTimeStatus.Created, created.Status);
        var retry = await handler.HandleAsync(create);
        Assert.Equal(created, retry);
        Assert.Equal(LeadTimeStatus.Conflict,
            (await handler.HandleAsync(create with { LeadTimeDays = 12 })).Status);

        var revised = await handler.HandleAsync(Next(create, created.Receipt!, LeadTimeOperation.Revise, planner, 12));
        Assert.Equal(LeadTimeStatus.Created, revised.Status);
        Assert.NotEqual(created.Receipt!.RevisionId, revised.Receipt!.RevisionId);
        Assert.Equal(LeadTimeStatus.Conflict,
            (await handler.HandleAsync(Next(create, created.Receipt, LeadTimeOperation.Submit, planner))).Status);

        var submitted = await handler.HandleAsync(Next(create, revised.Receipt, LeadTimeOperation.Submit, planner));
        Assert.Equal("InReview", submitted.Receipt!.State);
        Assert.Equal(LeadTimeStatus.Forbidden,
            (await handler.HandleAsync(Next(create, submitted.Receipt, LeadTimeOperation.Approve, planner))).Status);
        var approved = await handler.HandleAsync(Next(create, submitted.Receipt, LeadTimeOperation.Approve, reviewer));
        Assert.Equal("Approved", approved.Receipt!.State);

        var policy = await context.Policies.Find(p => p.Id == approved.Receipt.PolicyId.ToString("D") &&
            p.TenantId == tenant.ToString("D") && p.LegalEntityId == company.ToString("D") && !p.IsDeleted)
            .SingleAsync();
        Assert.Equal(2, policy.Revisions.Count);
        Assert.Equal(9, policy.Revisions[0].Days);
        Assert.Equal(12, policy.Revisions[1].Days);
        Assert.Equal(4, await context.Audit.CountDocumentsAsync(a => a.PolicyId == policy.Id &&
            a.TenantId == tenant.ToString("D") && a.LegalEntityId == company.ToString("D")));
        Assert.Equal(LeadTimeStatus.Forbidden,
            (await handler.HandleAsync(Next(create, approved.Receipt, LeadTimeOperation.Revise, Guid.Empty, 14))).Status);
    }

    [ManualDraftMongoFact]
    public async Task AuditFailure_RollsBackEveryCommand_AndItsReceipt()
    {
        foreach (var operation in Enum.GetValues<LeadTimeOperation>())
        {
            var (context, handler) = Open();
            var tenant = Guid.NewGuid();
            var company = Guid.NewGuid();
            var planner = Guid.NewGuid();
            var reviewer = Guid.NewGuid();
            var create = Create(tenant, company, Guid.NewGuid(), Guid.NewGuid(), planner);
            LeadTimeCommand command;
            if (operation == LeadTimeOperation.Create)
                command = create;
            else
            {
                var created = await handler.HandleAsync(create);
                Assert.NotNull(created.Receipt);
                var current = created.Receipt!;
                if (operation is LeadTimeOperation.Approve or LeadTimeOperation.Reject)
                {
                    var submitted = await handler.HandleAsync(Next(create, current, LeadTimeOperation.Submit, planner));
                    current = submitted.Receipt!;
                }
                command = Next(create, current, operation,
                    operation is LeadTimeOperation.Approve or LeadTimeOperation.Reject ? reviewer : planner,
                    operation == LeadTimeOperation.Revise ? 12 : null,
                    operation == LeadTimeOperation.Reject ? "fixture rejection" : null);
            }

            var beforePolicy = await context.Policies.Find(p => p.TenantId == tenant.ToString("D") &&
                p.LegalEntityId == company.ToString("D")).FirstOrDefaultAsync();
            var beforeVersion = beforePolicy?.Version;
            var beforeAudit = await context.Audit.CountDocumentsAsync(a => a.TenantId == tenant.ToString("D"));
            var failed = await Handler(context, true).HandleAsync(command);
            Assert.Equal(LeadTimeStatus.Unavailable, failed.Status);
            var afterPolicy = await context.Policies.Find(p => p.TenantId == tenant.ToString("D") &&
                p.LegalEntityId == company.ToString("D")).FirstOrDefaultAsync();
            Assert.Equal(beforeVersion, afterPolicy?.Version);
            Assert.Equal(beforeAudit, await context.Audit.CountDocumentsAsync(a => a.TenantId == tenant.ToString("D")));
            Assert.Equal(0, await context.Receipts.CountDocumentsAsync(r => r.TenantId == tenant.ToString("D") &&
                r.ActorId == command.ActorId.ToString("D") && r.IdempotencyKey == command.IdempotencyKey));

            var recovered = await handler.HandleAsync(command);
            Assert.Contains(recovered.Status, new[] { LeadTimeStatus.Created, LeadTimeStatus.Updated });
        }
    }

    [ManualDraftMongoFact]
    public async Task AuditWrittenThenFailure_RollsBackPolicyAuditAndReceipt()
    {
        var (context, normal) = Open();
        var command = Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
        var failing = new LeadTimeCommandHandler(new FixtureAuthority(),
            new LeadTimeMongoStore(context, new WriteThenFailAudit(context)));

        Assert.Equal(LeadTimeStatus.Unavailable, (await failing.HandleAsync(command)).Status);
        var tenant = command.TenantId.ToString("D");
        Assert.Equal(0, await context.Policies.CountDocumentsAsync(x => x.TenantId == tenant));
        Assert.Equal(0, await context.Audit.CountDocumentsAsync(x => x.TenantId == tenant));
        Assert.Equal(0, await context.Receipts.CountDocumentsAsync(x => x.TenantId == tenant));

        Assert.Equal(LeadTimeStatus.Created, (await normal.HandleAsync(command)).Status);
        Assert.Equal(1, await context.Policies.CountDocumentsAsync(x => x.TenantId == tenant));
        Assert.Equal(1, await context.Audit.CountDocumentsAsync(x => x.TenantId == tenant));
        Assert.Equal(1, await context.Receipts.CountDocumentsAsync(x => x.TenantId == tenant));
    }

    [ManualDraftMongoFact]
    public async Task DifferentTenantCannotReadOrMutateAnotherPolicy()
    {
        var (context, handler) = Open();
        var command = Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var created = await handler.HandleAsync(command);
        Assert.Equal(LeadTimeStatus.Created, created.Status);

        var otherTenant = Next(command, created.Receipt!, LeadTimeOperation.Submit, command.ActorId)
            with { TenantId = Guid.NewGuid() };
        Assert.Equal(LeadTimeStatus.NotFound, (await handler.HandleAsync(otherTenant)).Status);
        Assert.Equal(0, await context.Policies.CountDocumentsAsync(p => p.TenantId == otherTenant.TenantId.ToString("D")));
    }

    [ManualDraftMongoFact]
    public async Task ConcurrentCreation_LeavesOnePolicyAndOneAudit()
    {
        var (context, handler) = Open();
        var command = Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => handler.HandleAsync(command with { IdempotencyKey = Guid.NewGuid().ToString("N") })));

        Assert.Single(results, r => r.Status == LeadTimeStatus.Created);
        Assert.Equal(1, await context.Policies.CountDocumentsAsync(p =>
            p.TenantId == command.TenantId.ToString("D") &&
            p.LegalEntityId == command.LegalEntityId.ToString("D") &&
            p.SkuId == command.SkuId.ToString("D") &&
            p.WarehouseId == command.WarehouseId.ToString("D") && !p.IsDeleted));
        Assert.Equal(1, await context.Audit.CountDocumentsAsync(a =>
            a.TenantId == command.TenantId.ToString("D") &&
            a.SkuId == command.SkuId.ToString("D")));
    }

    [ManualDraftMongoFact]
    public async Task RejectionPersistsReason_WithoutApproval()
    {
        var (context, handler) = Open();
        var planner = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var command = Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), planner);
        var created = await handler.HandleAsync(command);
        var submitted = await handler.HandleAsync(Next(command, created.Receipt!, LeadTimeOperation.Submit, planner));
        var rejected = await handler.HandleAsync(Next(command, submitted.Receipt!,
            LeadTimeOperation.Reject, reviewer, reason: "fixture reason"));

        Assert.Equal("Rejected", rejected.Receipt!.State);
        var audit = await context.Audit.Find(a => a.PolicyId == rejected.Receipt.PolicyId.ToString("D") &&
            a.Action == "Reject").SingleAsync();
        Assert.Equal("fixture reason", audit.Reason);
        Assert.Equal(reviewer.ToString("D"), audit.ActorId);
        Assert.Equal(LeadTimeStatus.Conflict,
            (await handler.HandleAsync(Next(command, rejected.Receipt, LeadTimeOperation.Approve, reviewer))).Status);
    }

    private sealed class FixtureAuthority : ILeadTimeAuthority
    {
        public Task<LeadTimeAuthorityResult> CanExecuteAsync(LeadTimeCommand command, string permission, CancellationToken cancellationToken) =>
            Task.FromResult(command.ActorId != Guid.Empty && command.TenantId != Guid.Empty &&
                command.LegalEntityId != Guid.Empty && command.SkuId != Guid.Empty &&
                command.WarehouseId != Guid.Empty && permission.StartsWith("mrp.lead-times.", StringComparison.Ordinal)
                ? LeadTimeAuthorityResult.Allowed : LeadTimeAuthorityResult.Forbidden);
    }

    private sealed class DenyingAuthority : ILeadTimeAuthority
    {
        public Task<LeadTimeAuthorityResult> CanExecuteAsync(LeadTimeCommand command, string permission,
            CancellationToken cancellationToken) => Task.FromResult(LeadTimeAuthorityResult.Forbidden);
    }

    private sealed class WriteThenFailAudit(LeadTimeMongoContext context) : ILeadTimeAuditAppender
    {
        public async Task AppendAsync(MongoDB.Driver.IClientSessionHandle session,
            LeadTimeAuditDocument audit, CancellationToken cancellationToken)
        {
            await context.Audit.InsertOneAsync(session, audit, cancellationToken: cancellationToken);
            throw new InvalidOperationException("Injected failure after the audit insert.");
        }
    }

    private sealed class FailingAudit : ILeadTimeAuditAppender
    {
        public Task AppendAsync(IClientSessionHandle session, LeadTimeAuditDocument audit, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Injected audit failure");
    }

    private sealed class NeverStore : ILeadTimeMutationStore
    {
        public Task<LeadTimeResult> ExecuteAsync(LeadTimeCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unavailable authority must not call persistence");
    }
}
