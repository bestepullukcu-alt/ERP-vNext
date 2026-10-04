using System.Reflection;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Handlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Diten.Shared.Core;
using MediatR;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationWorkItemMongoTests(Diten.MdmService.Application.Tests.Audit.AuditIntentTemporalMongoFixture mongo)
    : IClassFixture<Diten.MdmService.Application.Tests.Audit.AuditIntentTemporalMongoFixture>
{
    [Theory]
    [InlineData("approve", ProductAbbreviationLifecycleStatus.ACTIVE)]
    [InlineData("reject", ProductAbbreviationLifecycleStatus.REJECTED)]
    [InlineData("cancel", ProductAbbreviationLifecycleStatus.CANCELLED)]
    public async Task Dispatch_terminal_exact_replay_is_durable_and_payload_drift_changes_nothing(
        string actionCode,
        ProductAbbreviationLifecycleStatus terminalStatus)
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var register = scope.Repository(scope.TenantA);
        var history = scope.History(scope.TenantA);
        var requester = Guid.NewGuid().ToString("D");
        var decisionActor = actionCode == "cancel" ? requester : Guid.NewGuid().ToString("D");
        var entry = await InsertAsync(
            register,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "REPLAY-" + actionCode,
            requestedBy: requester);
        var seeded = (await history.GetForRegisterEntryAsync(entry.Id)).Count;
        var reason = actionCode == "reject" ? "required" : null;
        var handler = Handler(scope, register, history, decisionActor);
        var request = new DispatchProductAbbreviationWorkItemActionCommand(
            entry.Id,
            actionCode,
            "mdm-product-abbreviations",
            0,
            reason,
            null,
            false);

        var first = await handler.Handle(request, CancellationToken.None);
        var afterFirst = await register.GetByIdAsync(entry.Id);
        var historyAfterFirst = await history.GetForRegisterEntryAsync(entry.Id);
        var replay = await handler.Handle(request, CancellationToken.None);
        var afterReplay = await register.GetByIdAsync(entry.Id);
        var historyAfterReplay = await history.GetForRegisterEntryAsync(entry.Id);

        Assert.True(first.IsSuccessful);
        Assert.Equal(200, first.StatusCode);
        Assert.True(replay.IsSuccessful);
        Assert.Equal(200, replay.StatusCode);
        Assert.NotNull(afterFirst);
        Assert.NotNull(afterReplay);
        Assert.Equal(entry.Id, afterFirst.Id);
        Assert.Equal(terminalStatus, afterFirst.LifecycleStatus);
        Assert.Equal(1, afterFirst.Version);
        Assert.Equal(afterFirst.Id, afterReplay.Id);
        Assert.Equal(afterFirst.LifecycleStatus, afterReplay.LifecycleStatus);
        Assert.Equal(afterFirst.Version, afterReplay.Version);
        Assert.Equal(seeded + 1, historyAfterFirst.Count);
        Assert.Equal(historyAfterFirst.Select(x => x.Id), historyAfterReplay.Select(x => x.Id));
        // The request's own intent and the decision's — the replay added none.
        Assert.Equal(2, afterReplay.AuditIntents.Count);
        var revoked = await Handler(scope, register, history, decisionActor,
            new HashSet<string>(StringComparer.Ordinal)).Handle(request, CancellationToken.None);
        Assert.False(revoked.IsSuccessful);
        Assert.Equal(403, revoked.StatusCode);
        Assert.Equal(1, (await register.GetByIdAsync(entry.Id))!.Version);
        var foreign = await Handler(scope, scope.Repository(scope.TenantB), scope.History(scope.TenantB),
            decisionActor).Handle(request, CancellationToken.None);
        Assert.False(foreign.IsSuccessful);
        Assert.Equal(404, foreign.StatusCode);

        var changedReason = await handler.Handle(
            request with { Reason = reason == "changed" ? "other" : "changed" },
            CancellationToken.None);
        var changedVersion = await handler.Handle(
            request with { ExpectedVersion = 1 },
            CancellationToken.None);
        var changedActionCode = actionCode == "approve" ? "reject" : "approve";
        var changedActionActor = actionCode == "cancel"
            ? Guid.NewGuid().ToString("D")
            : decisionActor;
        var changedAction = await Handler(scope, register, history, changedActionActor).Handle(
            request with
            {
                ActionCode = changedActionCode,
                Reason = changedActionCode == "reject" ? "changed-action" : null
            },
            CancellationToken.None);

        AssertConflict(changedReason);
        AssertConflict(changedVersion);
        AssertConflict(changedAction);
        var final = await register.GetByIdAsync(entry.Id);
        var finalHistory = await history.GetForRegisterEntryAsync(entry.Id);
        Assert.NotNull(final);
        Assert.Equal(afterFirst.Id, final.Id);
        Assert.Equal(afterFirst.LifecycleStatus, final.LifecycleStatus);
        Assert.Equal(afterFirst.Version, final.Version);
        Assert.Equal(historyAfterFirst.Select(x => x.Id), finalHistory.Select(x => x.Id));
    }

    [Fact]
    public async Task Work_item_page_is_tenant_safe_and_holds_initial_correction_and_retirement_pending_only()
    {
        // FIX3 item 8 — the paged read the projection uses (the unpaged legacy reads are gone), seeded through the
        // register's own writes: decided rows and another tenant's rows never appear.
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var repository = scope.Repository(scope.TenantA);
        var initial = await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "INITIAL");
        var former = await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "FORMER");
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Approve, former.Id, 0, Checker, "former-active")).Succeeded);
        var correction = await InsertAsync(
            repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "CORRECTION-PENDING", former.Id, productId: former.GlobalProductId);
        var retiring = await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "RETIRING");
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Approve, retiring.Id, 0, Checker, "retiring-active")).Succeeded);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.RequestRetirement, retiring.Id, 1, Maker, "retirement-request")).Succeeded);
        var terminal = await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "TERMINAL");
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Cancel, terminal.Id, 0, Maker, "terminal-transition")).Succeeded);
        await InsertAsync(scope.Repository(scope.TenantB), Guid.NewGuid(), DateTimeOffset.UtcNow, "TENANT-B-PENDING");

        var results = await repository.GetPendingWorkItemsPageAsync(null, 500);

        Assert.Equal(3, results.Count);
        Assert.Contains(results, x => x.Id == initial.Id && x.ReplacesEntryId is null);
        Assert.Contains(results, x => x.Id == correction.Id && x.ReplacesEntryId == former.Id);
        Assert.Contains(results, x => x.Id == retiring.Id && x.RetirementRequestId == "retirement-request");
        Assert.All(results, x => Assert.Equal(scope.TenantA, x.TenantId));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.GetPendingWorkItemsPageAsync(null, 501));
    }

    [Theory]
    [InlineData(false, "approve")]
    [InlineData(false, "reject")]
    [InlineData(false, "cancel")]
    [InlineData(true, "approve")]
    [InlineData(true, "reject")]
    public async Task Correction_and_retirement_terminal_history_and_audit_read_back_replay(bool retirement, string action)
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var register = scope.Repository(scope.TenantA);
        var history = scope.History(scope.TenantA);
        var maker = Guid.NewGuid().ToString("D");
        var checker = Guid.NewGuid().ToString("D");
        var original = await InsertAsync(register, Guid.NewGuid(), DateTimeOffset.UtcNow, "BASE", requestedBy: maker);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            register, ProductAbbreviationDecisionKind.Approve, original.Id, 0, checker, "initial-approval")).Succeeded);
        ProductAbbreviationRegisterEntry target;
        if (retirement)
        {
            var actor = new ProductAbbreviationActor(scope.TenantA, maker, ProductAbbreviationPermissions.All);
            var workflow = new ProductAbbreviationWorkflow(register, scope.Ledger(scope.TenantA), history,
                Stub<IGlobalProductRepository>((method, _) => throw new InvalidOperationException(method.Name)),
                actor, new ProductAbbreviationAuthorization(actor));
            var requested = await workflow.RequestRetirementAsync(new(original.Id, 1, "retirement-request", "reason"),
                CancellationToken.None);
            Assert.True(requested.IsSuccessful);
            target = (await register.GetByIdAsync(original.Id))!;
        }
        else
        {
            var result = await ProductAbbreviationTestWrites.RequestAsync(
                register, original.GlobalProductId, "NEW", "replacement", maker, original.Id);
            Assert.True(result.Succeeded);
            target = result.Entry!;
        }
        var handler = Handler(scope, register, history, action == "cancel" ? maker : checker);
        var request = new DispatchProductAbbreviationWorkItemActionCommand(target.Id, action,
            "mdm-product-abbreviations", target.Version, "reason", null, false);
        var first = await handler.Handle(request, CancellationToken.None);
        Assert.True(first.IsSuccessful, first.ReasonCode);
        var after = (await register.GetByIdAsync(target.Id))!;
        var evidence = await history.GetForRegisterEntryAsync(target.Id);
        var replay = await handler.Handle(request, CancellationToken.None);
        Assert.True(replay.IsSuccessful, replay.ReasonCode);
        var reread = (await register.GetByIdAsync(target.Id))!;
        Assert.Equal(after.Version, reread.Version);
        Assert.Equal(after.LifecycleStatus, reread.LifecycleStatus);
        Assert.Equal(evidence.Count, (await history.GetForRegisterEntryAsync(target.Id)).Count);
        Assert.Equal(after.AuditIntents.Count, reread.AuditIntents.Count);
        Assert.NotEmpty(reread.AuditIntents);
        Assert.Equal(retirement
            ? action == "approve" ? ProductAbbreviationLifecycleStatus.RETIRED : ProductAbbreviationLifecycleStatus.ACTIVE
            : action == "approve" ? ProductAbbreviationLifecycleStatus.ACTIVE
                : action == "reject" ? ProductAbbreviationLifecycleStatus.REJECTED : ProductAbbreviationLifecycleStatus.CANCELLED,
            reread.LifecycleStatus);
        if (!retirement)
            Assert.Equal(action == "approve" ? ProductAbbreviationLifecycleStatus.RETIRED : ProductAbbreviationLifecycleStatus.ACTIVE,
                (await register.GetByIdAsync(original.Id))!.LifecycleStatus);
    }

    private static readonly string Maker = Guid.NewGuid().ToString("D");
    private static readonly string Checker = Guid.NewGuid().ToString("D");

    private static async Task<ProductAbbreviationRegisterEntry> InsertAsync(
        ProductAbbreviationRegisterRepository repository,
        Guid id,
        DateTimeOffset requestedAt,
        string key,
        Guid? replaces = null,
        string? requestedBy = null,
        Guid? productId = null)
    {
        var result = await ProductAbbreviationTestWrites.RequestAsync(
            repository,
            productId ?? Guid.NewGuid(),
            key,
            "work-item-" + key,
            requestedBy ?? Maker,
            replaces,
            requestedAtUtc: requestedAt,
            entryId: id);
        Assert.True(result.Succeeded, result.ErrorCode);
        return result.Entry!;
    }

    private static DispatchProductAbbreviationWorkItemActionHandler Handler(
        MongoScope scope,
        ProductAbbreviationRegisterRepository register,
        ProductAbbreviationHistoryRepository history,
        string actorId,
        IReadOnlySet<string>? permissions = null)
    {
        var tenant = scope.Context(scope.TenantA);
        var actor = new ProductAbbreviationActor(
            scope.TenantA,
            actorId,
            permissions ?? new HashSet<string>(StringComparer.Ordinal)
            {
                ProductAbbreviationPermissions.Approve,
                ProductAbbreviationPermissions.Reject,
                ProductAbbreviationPermissions.Cancel
            });
        var globalProducts = Stub<IGlobalProductRepository>((method, _) =>
            throw new InvalidOperationException(method.Name));
        var workflow = new ProductAbbreviationWorkflow(
            register,
            scope.Ledger(scope.TenantA),
            history,
            globalProducts,
            actor,
            new ProductAbbreviationAuthorization(actor));
        var mediator = Stub<IMediator>((method, args) =>
        {
            if (method.Name != nameof(IMediator.Send))
            {
                throw new InvalidOperationException(method.Name);
            }

            return args![0] switch
            {
                ApproveProductAbbreviationAllocationCommand command =>
                    workflow.ApproveAsync(command, (CancellationToken)args[1]!),
                RejectProductAbbreviationAllocationCommand command =>
                    workflow.RejectAsync(command, (CancellationToken)args[1]!),
                CancelProductAbbreviationAllocationCommand command =>
                    workflow.CancelAsync(command, (CancellationToken)args[1]!),
                ApproveProductAbbreviationRetirementCommand command =>
                    workflow.ApproveRetirementAsync(command, (CancellationToken)args[1]!),
                RejectProductAbbreviationRetirementCommand command =>
                    workflow.RejectRetirementAsync(command, (CancellationToken)args[1]!),
                _ => throw new InvalidOperationException(args[0]!.GetType().FullName)
            };
        });
        var rollout = Stub<IProductLegalEntityScopeRolloutStateRepository>((method, _) => method.Name switch
        {
            nameof(IProductLegalEntityScopeRolloutStateRepository.GetAsync) =>
                Task.FromResult<ProductLegalEntityScopeRolloutState?>(null),
            _ => throw new InvalidOperationException(method.Name)
        });
        var candidates = new ProductLegalEntityScopeCandidateFacade(
            Stub<ITrustedLegalEntityScopeProvider>((method, _) => throw new InvalidOperationException(method.Name)),
            Stub<ILegalEntityRepository>((method, _) => throw new InvalidOperationException(method.Name)),
            tenant,
            new ProductIdentityActor(actorId));
        return new(
            mediator,
            register,
            globalProducts,
            rollout,
            Stub<IProductLegalEntityScopePolicyRepository>((method, _) =>
                throw new InvalidOperationException(method.Name)),
            candidates,
            tenant,
            actor,
            history);
    }

    private static void AssertConflict(
        ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse> result)
    {
        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("CONCURRENCY_CONFLICT", result.ReasonCode);
    }

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> implementation) where T : class
    {
        var value = DispatchProxy.Create<T, StubProxy>();
        ((StubProxy)(object)value).Implementation = implementation;
        return value;
    }

    private class StubProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Implementation { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => Implementation(targetMethod!, args);
    }

    private sealed record ProductIdentityActor(string ActorId) : IProductIdentityActorContext;

    private sealed record ProductAbbreviationActor(
        Guid TenantId,
        string CanonicalHumanSubjectId,
        IReadOnlySet<string> GrantedPermissions) : IProductAbbreviationActorContext
    {
        public bool TenantIsResolved => true;
        public bool IsAuthenticated => true;
        public string ActorType => "tenant_user";
        public string CorrelationId => "abb-workcenter-replay";
    }

    private sealed class MongoScope : IAsyncDisposable
    {
        private readonly IMongoClient _client;
        private readonly string _databaseName;

        private MongoScope(IMongoClient client, IMongoDatabase database, string databaseName)
        {
            _client = client;
            Database = database;
            _databaseName = databaseName;
        }

        public Guid TenantA { get; } = Guid.NewGuid();
        public Guid TenantB { get; } = Guid.NewGuid();
        public IMongoDatabase Database { get; }

        public static async Task<MongoScope> CreateAsync(string connectionString)
        {
            var settings = MongoClientSettings.FromConnectionString(
                connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
#pragma warning disable CS0618
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var databaseName = "diten_mdm_abb_wc_itest";
            var database = client.GetDatabase(databaseName);
            await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1));
            return new MongoScope(client, database, databaseName);
        }

        public ProductAbbreviationRegisterRepository Repository(Guid tenantId)
        {
            var context = new TenantContext();
            context.SetTenant(tenantId);
            return new ProductAbbreviationRegisterRepository(Database, context);
        }

        public ProductAbbreviationAllocationLedgerRepository Ledger(Guid tenantId)
            => new(Database, Context(tenantId));

        public ProductAbbreviationHistoryRepository History(Guid tenantId)
            => new(Database, Context(tenantId));

        public TenantContext Context(Guid tenantId)
        {
            var context = new TenantContext();
            context.SetTenant(tenantId);
            return context;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var name in new[] { "mdm_product_abbreviation_register", "mdm_product_abbreviation_history", "mdm_product_abbreviation_allocation_ledger", "mdm_product_abbreviation_command_keys", "mdm_product_abbreviation_product_slots" })
                await Database.GetCollection<MongoDB.Bson.BsonDocument>(name).DeleteManyAsync(
                    MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.In("TenantId", new[] { TenantA, TenantB }));
        }
    }
}
