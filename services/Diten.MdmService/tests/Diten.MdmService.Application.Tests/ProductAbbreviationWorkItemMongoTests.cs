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

public sealed class ProductAbbreviationWorkItemMongoTests
{
    [Theory]
    [InlineData("approve", ProductAbbreviationLifecycleStatus.ACTIVE)]
    [InlineData("reject", ProductAbbreviationLifecycleStatus.REJECTED)]
    [InlineData("cancel", ProductAbbreviationLifecycleStatus.CANCELLED)]
    public async Task Dispatch_terminal_exact_replay_is_durable_and_payload_drift_changes_nothing(
        string actionCode,
        ProductAbbreviationLifecycleStatus terminalStatus)
    {
        await using var scope = await MongoScope.CreateAsync();
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
        Assert.Single(historyAfterFirst);
        Assert.Single(historyAfterReplay);
        Assert.Equal(historyAfterFirst[0].Id, historyAfterReplay[0].Id);

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
        Assert.Single(finalHistory);
        Assert.Equal(historyAfterFirst[0].Id, finalHistory[0].Id);
    }

    [Fact]
    public async Task Pending_query_is_tenant_safe_initial_only_bounded_and_orders_by_scalar_id()
    {
        await using var scope = await MongoScope.CreateAsync();
        var repository = scope.Repository(scope.TenantA);
        var largerId = Guid.Parse("f0000000-0000-0000-0000-000000000001");
        var smallerId = Guid.Parse("10000000-0000-0000-0000-000000000001");

        await InsertAsync(repository, largerId, DateTimeOffset.Parse("2026-01-01T00:00:00+14:00"), "A");
        await InsertAsync(repository, smallerId, DateTimeOffset.Parse("2026-12-01T00:00:00-12:00"), "B");
        await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "CORRECTION", Guid.NewGuid());
        var terminal = await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "TERMINAL");
        Assert.True((await repository.TransitionAsync(
            terminal.Id, 0, ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.CANCELLED, "maker", "terminal-transition", null,
            DateTimeOffset.UtcNow)).Succeeded);
        await InsertAsync(scope.Repository(scope.TenantB), Guid.NewGuid(), DateTimeOffset.UtcNow, "TENANT-B");

        var results = await repository.GetInitialPendingWorkItemsAsync(101);

        Assert.Equal([smallerId, largerId], results.Select(x => x.Id).ToArray());
        Assert.All(results, x =>
        {
            Assert.Equal(scope.TenantA, x.TenantId);
            Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, x.LifecycleStatus);
            Assert.Null(x.ReplacesEntryId);
        });
    }

    [Fact]
    public async Task Pending_query_returns_the_101st_overflow_sentinel_and_rejects_larger_limits()
    {
        await using var scope = await MongoScope.CreateAsync();
        var repository = scope.Repository(scope.TenantA);
        for (var index = 0; index < 102; index++)
        {
            await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, $"K{index:D3}");
        }

        Assert.Equal(101, (await repository.GetInitialPendingWorkItemsAsync(101)).Count);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.GetInitialPendingWorkItemsAsync(102));
    }

    [Fact]
    public async Task Work_item_query_is_tenant_safe_and_includes_initial_correction_and_retirement_pending()
    {
        await using var scope = await MongoScope.CreateAsync();
        var repository = scope.Repository(scope.TenantA);
        var initial = await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "INITIAL");
        var former = await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "FORMER");
        Assert.True((await repository.TransitionAsync(
            former.Id, 0, ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE, "checker", "former-active", null,
            DateTimeOffset.UtcNow)).Succeeded);
        var correction = await InsertAsync(
            repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "CORRECTION-PENDING", former.Id);
        var retiring = await InsertAsync(repository, Guid.NewGuid(), DateTimeOffset.UtcNow, "RETIRING");
        var activeRetiring = await repository.TransitionAsync(
            retiring.Id, 0, ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE, "checker", "retiring-active", null,
            DateTimeOffset.UtcNow);
        Assert.True(activeRetiring.Succeeded);
        Assert.True((await repository.RequestRetirementAsync(
            retiring.Id, 1, "retirement-request", "retirement-maker", "retirement-request", null,
            DateTimeOffset.UtcNow)).Succeeded);
        await InsertAsync(scope.Repository(scope.TenantB), Guid.NewGuid(), DateTimeOffset.UtcNow, "TENANT-B-PENDING");

        var results = await repository.GetPendingWorkItemsAsync(101);

        Assert.Equal(3, results.Count);
        Assert.Contains(results, x => x.Id == initial.Id && x.ReplacesEntryId is null);
        Assert.Contains(results, x => x.Id == correction.Id && x.ReplacesEntryId == former.Id);
        Assert.Contains(results, x => x.Id == retiring.Id && x.RetirementRequestId == "retirement-request");
        Assert.All(results, x => Assert.Equal(scope.TenantA, x.TenantId));
    }

    private static async Task<ProductAbbreviationRegisterEntry> InsertAsync(
        ProductAbbreviationRegisterRepository repository,
        Guid id,
        DateTimeOffset requestedAt,
        string key,
        Guid? replaces = null,
        string requestedBy = "maker")
    {
        var result = await repository.InsertRequestedAsync(new ProductAbbreviationRegisterEntry
        {
            Id = id,
            GlobalProductId = Guid.NewGuid(),
            NormalizedAbbreviation = key,
            AllocationLedgerId = Guid.NewGuid(),
            AllocationIdempotencyKey = "work-item-" + key,
            RequestedByCanonicalSubjectId = requestedBy,
            RequestedAtUtc = requestedAt,
            ReplacesEntryId = replaces
        });
        Assert.True(result.Succeeded);
        return result.Entry!;
    }

    private static DispatchProductAbbreviationWorkItemActionHandler Handler(
        MongoScope scope,
        ProductAbbreviationRegisterRepository register,
        ProductAbbreviationHistoryRepository history,
        string actorId)
    {
        var tenant = scope.Context(scope.TenantA);
        var actor = new ProductAbbreviationActor(
            scope.TenantA,
            actorId,
            new HashSet<string>(StringComparer.Ordinal)
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
            tenant);
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

        public static async Task<MongoScope> CreateAsync()
        {
            var settings = MongoClientSettings.FromConnectionString(
                Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? "mongodb://localhost:27017");
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
#pragma warning disable CS0618
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var databaseName = "diten_mdm_abb_wc_itest_" + Guid.NewGuid().ToString("N");
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

        public async ValueTask DisposeAsync() => await _client.DropDatabaseAsync(_databaseName);
    }
}
