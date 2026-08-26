using System.Reflection;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationRegisterMongoTests
{
    [Fact]
    public async Task Required_ledger_and_active_binding_indexes_exist_with_correct_partiality()
    {
        await using var scope = await MongoScope.CreateAsync();
        _ = scope.Ledger(scope.TenantA);
        _ = scope.Register(scope.TenantA);

        var ledgerIndexes = await ReadIndexesAsync(
            scope.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_product_abbreviation_allocation_ledger"));
        var abbreviation = Assert.Single(
            ledgerIndexes,
            item => item["name"] == "ux_mdm_product_abbreviation_ledger_tenant_abbreviation");
        var idempotency = Assert.Single(
            ledgerIndexes,
            item => item["name"] == "ux_mdm_product_abbreviation_ledger_tenant_idempotency");
        Assert.True(abbreviation["unique"].AsBoolean);
        Assert.True(idempotency["unique"].AsBoolean);
        Assert.False(abbreviation.Contains("partialFilterExpression"));
        Assert.False(idempotency.Contains("partialFilterExpression"));

        var registerIndexes = await ReadIndexesAsync(
            scope.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_product_abbreviation_register"));
        var active = Assert.Single(
            registerIndexes,
            item => item["name"] == "ux_mdm_product_abbreviation_register_tenant_active_product");
        Assert.True(active["unique"].AsBoolean);
        Assert.True(active.Contains("partialFilterExpression"));
    }

    [Fact]
    public async Task Tenant_isolation_allows_same_ABB_in_another_tenant_and_hides_cross_tenant_entry()
    {
        await using var scope = await MongoScope.CreateAsync();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var entryA = await CreateRequestedAsync(scope, scope.TenantA, productA, "ABC", "tenant-a");
        var entryB = await CreateRequestedAsync(scope, scope.TenantB, productB, "ABC", "tenant-b");

        Assert.NotEqual(entryA.Id, entryB.Id);
        Assert.Null(await scope.Register(scope.TenantB).GetByIdAsync(entryA.Id));
        Assert.Null(await scope.Register(scope.TenantA).GetByIdAsync(entryB.Id));
    }

    [Fact]
    public async Task Concurrent_same_tenant_ABB_allocation_has_one_durable_winner()
    {
        await using var scope = await MongoScope.CreateAsync();
        var repository = scope.Ledger(scope.TenantA);
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(index =>
        {
            var entryId = Guid.NewGuid();
            return repository.AllocateAsync(new ProductAbbreviationAllocationLedger
            {
                Id = Guid.NewGuid(),
                NormalizedAbbreviation = "ONE",
                GlobalProductId = Guid.NewGuid(),
                RegisterEntryId = entryId,
                IdempotencyKey = $"parallel-{index}",
                PayloadHash = $"payload-{index}",
                AllocatedByCanonicalSubjectId = "maker",
                AllocatedAtUtc = DateTimeOffset.UtcNow
            });
        }));

        Assert.Single(results, result => result.Succeeded);
        Assert.All(
            results.Where(result => !result.Succeeded),
            result => Assert.Equal("ABBREVIATION_ALREADY_ALLOCATED", result.ErrorCode));
    }

    [Fact]
    public async Task Partial_unique_index_allows_at_most_one_active_ABB_per_product()
    {
        await using var scope = await MongoScope.CreateAsync();
        var productId = Guid.NewGuid();
        var first = await CreateRequestedAsync(scope, scope.TenantA, productId, "AAA", "first");
        var second = await CreateRequestedAsync(scope, scope.TenantA, productId, "BBB", "second");
        var repository = scope.Register(scope.TenantA);

        var firstApproval = await repository.TransitionAsync(
            first.Id, 0, ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE, "checker", "approve-first", null, DateTimeOffset.UtcNow);
        var secondApproval = await repository.TransitionAsync(
            second.Id, 0, ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE, "checker", "approve-second", null, DateTimeOffset.UtcNow);

        Assert.True(firstApproval.Succeeded);
        Assert.False(secondApproval.Succeeded);
        Assert.Equal("ACTIVE_ABBREVIATION_CONFLICT", secondApproval.ErrorCode);
    }

    [Fact]
    public async Task Durable_ledger_tombstone_is_idempotent_and_never_reusable()
    {
        await using var scope = await MongoScope.CreateAsync();
        var repository = scope.Ledger(scope.TenantA);
        var original = Allocation("NVR", "no-reuse", Guid.NewGuid(), Guid.NewGuid());
        var created = await repository.AllocateAsync(original);
        var replay = await repository.AllocateAsync(Allocation(
            "NVR", "no-reuse", original.GlobalProductId, original.RegisterEntryId, original.Id, original.PayloadHash));
        var drift = await repository.AllocateAsync(Allocation(
            "NVR", "no-reuse", Guid.NewGuid(), original.RegisterEntryId, original.Id, "different"));
        var reuse = await repository.AllocateAsync(Allocation(
            "NVR", "new-command", Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(created.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(drift.Succeeded);
        Assert.Equal("ABBREVIATION_IDEMPOTENCY_CONFLICT", drift.ErrorCode);
        Assert.False(reuse.Succeeded);
        Assert.Equal("ABBREVIATION_ALREADY_ALLOCATED", reuse.ErrorCode);
    }

    [Fact]
    public async Task Request_owner_can_cancel_requested_entry_without_releasing_durable_ABB()
    {
        await using var scope = await MongoScope.CreateAsync();
        var owner = Guid.NewGuid().ToString("D");
        var entry = await CreateRequestedAsync(
            scope, scope.TenantA, Guid.NewGuid(), "OWN", "owner-request", requestedBy: owner);
        var workflow = CreateWorkflow(scope, owner);

        var response = await workflow.CancelAsync(
            new CancelProductAbbreviationAllocationCommand(entry.Id, 0, "owner-cancel"),
            default);

        Assert.True(response.IsSuccessful);
        var persisted = await scope.Register(scope.TenantA).GetByIdAsync(entry.Id);
        Assert.Equal(ProductAbbreviationLifecycleStatus.CANCELLED, persisted!.LifecycleStatus);
        Assert.Equal(1, persisted.Version);
        Assert.NotNull(await scope.Ledger(scope.TenantA).GetByIdAsync(entry.AllocationLedgerId));
        Assert.Single(await scope.History(scope.TenantA).GetForRegisterEntryAsync(entry.Id));

        var reuse = await scope.Ledger(scope.TenantA).AllocateAsync(
            Allocation("OWN", "owner-cancel-reuse", Guid.NewGuid(), Guid.NewGuid()));
        Assert.False(reuse.Succeeded);
        Assert.Equal("ABBREVIATION_ALREADY_ALLOCATED", reuse.ErrorCode);
    }

    [Fact]
    public async Task Same_tenant_non_owner_cancel_denial_changes_no_register_ledger_or_history_state()
    {
        await using var scope = await MongoScope.CreateAsync();
        var owner = Guid.NewGuid().ToString("D");
        var entry = await CreateRequestedAsync(
            scope, scope.TenantA, Guid.NewGuid(), "NNO", "non-owner-request", requestedBy: owner);
        var ledgerBefore = await scope.Ledger(scope.TenantA).GetByIdAsync(entry.AllocationLedgerId);
        var historyBefore = await scope.History(scope.TenantA).GetForRegisterEntryAsync(entry.Id);
        var workflow = CreateWorkflow(scope, Guid.NewGuid().ToString("D"));

        var response = await workflow.CancelAsync(
            new CancelProductAbbreviationAllocationCommand(entry.Id, 0, "non-owner-cancel"),
            default);

        Assert.False(response.IsSuccessful);
        Assert.Equal("ABBREVIATION_CANCEL_NOT_REQUEST_OWNER", Assert.Single(response.Errors));
        var persisted = await scope.Register(scope.TenantA).GetByIdAsync(entry.Id);
        Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, persisted!.LifecycleStatus);
        Assert.Equal(entry.Version, persisted.Version);
        Assert.Null(persisted.LastDecisionAtUtc);
        Assert.Null(persisted.LastDecisionIdempotencyKey);
        var ledgerAfter = await scope.Ledger(scope.TenantA).GetByIdAsync(entry.AllocationLedgerId);
        Assert.Equal(ledgerBefore!.Id, ledgerAfter!.Id);
        Assert.Equal(ledgerBefore.AllocationState, ledgerAfter.AllocationState);
        Assert.Equal(ledgerBefore.PayloadHash, ledgerAfter.PayloadHash);
        Assert.Equal(historyBefore.Count, (await scope.History(scope.TenantA)
            .GetForRegisterEntryAsync(entry.Id)).Count);
    }

    [Fact]
    public async Task Expected_version_stale_transition_changes_nothing()
    {
        await using var scope = await MongoScope.CreateAsync();
        var entry = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "CAS", "cas");
        var repository = scope.Register(scope.TenantA);

        var stale = await repository.TransitionAsync(
            entry.Id, 7, ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE, "checker", "stale", null, DateTimeOffset.UtcNow);
        var persisted = await repository.GetByIdAsync(entry.Id);

        Assert.False(stale.Succeeded);
        Assert.Equal("CONCURRENCY_CONFLICT", stale.ErrorCode);
        Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, persisted!.LifecycleStatus);
        Assert.Equal(0, persisted.Version);
    }

    [Fact]
    public async Task Correction_approval_reconciles_to_replacement_active_and_former_retired()
    {
        await using var scope = await MongoScope.CreateAsync();
        var productId = Guid.NewGuid();
        var former = await CreateRequestedAsync(scope, scope.TenantA, productId, "OLD", "old");
        var repository = scope.Register(scope.TenantA);
        Assert.True((await repository.TransitionAsync(
            former.Id, 0, ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE, "checker-1", "activate-old", null, DateTimeOffset.UtcNow)).Succeeded);
        var replacement = await CreateRequestedAsync(
            scope, scope.TenantA, productId, "NEW", "replacement", former.Id);

        var result = await repository.ReconcileCorrectionApprovalAsync(
            former.Id, 1, replacement.Id, 0, "checker-2", "approve-correction", "reason", DateTimeOffset.UtcNow);
        var replay = await repository.ReconcileCorrectionApprovalAsync(
            former.Id, 1, replacement.Id, 0, "checker-2", "approve-correction", "reason", DateTimeOffset.UtcNow);

        Assert.True(result.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(ProductAbbreviationLifecycleStatus.RETIRED, (await repository.GetByIdAsync(former.Id))!.LifecycleStatus);
        Assert.Equal(ProductAbbreviationLifecycleStatus.ACTIVE, (await repository.GetByIdAsync(replacement.Id))!.LifecycleStatus);
        Assert.False((await scope.Ledger(scope.TenantA).AllocateAsync(
            Allocation("OLD", "reuse-old", Guid.NewGuid(), Guid.NewGuid()))).Succeeded);
        Assert.False((await scope.Ledger(scope.TenantA).AllocateAsync(
            Allocation("NEW", "reuse-new", Guid.NewGuid(), Guid.NewGuid()))).Succeeded);
    }

    [Fact]
    public async Task Retirement_request_reject_and_later_checker_approval_preserve_closed_lifecycle()
    {
        await using var scope = await MongoScope.CreateAsync();
        var entry = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "RET", "ret");
        var repository = scope.Register(scope.TenantA);
        Assert.True((await repository.TransitionAsync(
            entry.Id, 0, ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE, "checker", "activate", null, DateTimeOffset.UtcNow)).Succeeded);

        var requested = await repository.RequestRetirementAsync(
            entry.Id, 1, "retirement-1", "maker", "request-retirement-1", "reason", DateTimeOffset.UtcNow);
        var rejected = await repository.ClearRetirementRequestAsync(
            entry.Id, requested.Entry!.Version, "retirement-1", "checker", "reject-retirement", "reason", DateTimeOffset.UtcNow);
        var requestedAgain = await repository.RequestRetirementAsync(
            entry.Id, rejected.Entry!.Version, "retirement-2", "maker", "request-retirement-2", "reason", DateTimeOffset.UtcNow);
        var approved = await repository.TransitionAsync(
            entry.Id, requestedAgain.Entry!.Version, ProductAbbreviationLifecycleStatus.ACTIVE,
            ProductAbbreviationLifecycleStatus.RETIRED, "different-checker", "approve-retirement", "reason", DateTimeOffset.UtcNow);

        Assert.True(rejected.Succeeded);
        Assert.Equal(ProductAbbreviationLifecycleStatus.ACTIVE, rejected.Entry.LifecycleStatus);
        Assert.True(approved.Succeeded);
        Assert.Equal(ProductAbbreviationLifecycleStatus.RETIRED, approved.Entry!.LifecycleStatus);
        Assert.Null(approved.Entry.RetirementRequestId);
    }

    [Fact]
    public async Task Reject_cancel_retire_and_soft_delete_never_release_a_durable_ABB()
    {
        await using var scope = await MongoScope.CreateAsync();
        var repository = scope.Register(scope.TenantA);
        var rejected = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "RJT", "reject-path");
        var cancelled = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "CNL", "cancel-path");
        var retired = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "RTR", "retire-path");
        var deleted = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "DEL", "delete-path");

        Assert.True((await repository.TransitionAsync(
            rejected.Id, 0, ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.REJECTED, "checker", "reject", null, DateTimeOffset.UtcNow)).Succeeded);
        Assert.True((await repository.TransitionAsync(
            cancelled.Id, 0, ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.CANCELLED, "maker", "cancel", null, DateTimeOffset.UtcNow)).Succeeded);
        Assert.True((await repository.TransitionAsync(
            retired.Id, 0, ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE, "checker", "activate-retire", null, DateTimeOffset.UtcNow)).Succeeded);
        Assert.True((await repository.TransitionAsync(
            retired.Id, 1, ProductAbbreviationLifecycleStatus.ACTIVE,
            ProductAbbreviationLifecycleStatus.RETIRED, "checker", "retire", null, DateTimeOffset.UtcNow)).Succeeded);
        await scope.Database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register")
            .UpdateOneAsync(
                Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Id, deleted.Id),
                Builders<ProductAbbreviationRegisterEntry>.Update
                    .Set(x => x.IsDeleted, true)
                    .Set(x => x.DeletedAt, DateTimeOffset.UtcNow));

        foreach (var abbreviation in new[] { "RJT", "CNL", "RTR", "DEL" })
        {
            var reuse = await scope.Ledger(scope.TenantA).AllocateAsync(
                Allocation(abbreviation, $"reuse-{abbreviation}", Guid.NewGuid(), Guid.NewGuid()));
            Assert.False(reuse.Succeeded);
            Assert.Equal("ABBREVIATION_ALREADY_ALLOCATED", reuse.ErrorCode);
        }
    }

    [Fact]
    public async Task Immutable_history_append_is_idempotent_and_payload_drift_fails_closed()
    {
        await using var scope = await MongoScope.CreateAsync();
        var repository = new ProductAbbreviationHistoryRepository(scope.Database, scope.Context(scope.TenantA));
        var entry = new ProductAbbreviationHistoryEntry
        {
            Id = Guid.NewGuid(),
            RegisterEntryId = Guid.NewGuid(),
            GlobalProductId = Guid.NewGuid(),
            NormalizedAbbreviation = "HIS",
            EventType = ProductAbbreviationHistoryEventType.ALLOCATION_REQUESTED,
            AfterStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
            CanonicalHumanSubjectId = "maker",
            ActorType = "tenant_user",
            IdempotencyKey = "history-1",
            CorrelationId = "correlation",
            EvidenceHash = "HASH-1",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        Assert.True(await repository.AppendIfAbsentAsync(entry));
        Assert.True(await repository.AppendIfAbsentAsync(entry));
        entry.EvidenceHash = "HASH-DRIFT";
        Assert.False(await repository.AppendIfAbsentAsync(entry));
        Assert.Single(await repository.GetForRegisterEntryAsync(entry.RegisterEntryId));
    }

    private static ProductAbbreviationAllocationLedger Allocation(
        string abbreviation,
        string idempotencyKey,
        Guid productId,
        Guid entryId,
        Guid? id = null,
        string? payloadHash = null)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            NormalizedAbbreviation = abbreviation,
            GlobalProductId = productId,
            RegisterEntryId = entryId,
            IdempotencyKey = idempotencyKey,
            PayloadHash = payloadHash ?? $"{abbreviation}|{productId:N}|{entryId:N}",
            AllocatedByCanonicalSubjectId = "maker",
            AllocatedAtUtc = DateTimeOffset.UtcNow
        };

    private static async Task<ProductAbbreviationRegisterEntry> CreateRequestedAsync(
        MongoScope scope,
        Guid tenantId,
        Guid productId,
        string abbreviation,
        string idempotencyKey,
        Guid? replacesEntryId = null,
        string requestedBy = "maker")
    {
        var entryId = Guid.NewGuid();
        var allocation = Allocation(abbreviation, idempotencyKey, productId, entryId);
        var ledger = await scope.Ledger(tenantId).AllocateAsync(allocation);
        Assert.True(ledger.Succeeded);
        var entry = new ProductAbbreviationRegisterEntry
        {
            Id = entryId,
            GlobalProductId = productId,
            NormalizedAbbreviation = abbreviation,
            AllocationLedgerId = ledger.Ledger!.Id,
            AllocationIdempotencyKey = idempotencyKey,
            RequestedByCanonicalSubjectId = requestedBy,
            RequestedAtUtc = DateTimeOffset.UtcNow,
            ReplacesEntryId = replacesEntryId
        };
        var inserted = await scope.Register(tenantId).InsertRequestedAsync(entry);
        Assert.True(inserted.Succeeded);
        return inserted.Entry!;
    }

    private static ProductAbbreviationWorkflow CreateWorkflow(MongoScope scope, string subject)
    {
        var actor = new TestActorContext(
            scope.TenantA,
            true,
            true,
            "tenant_user",
            subject,
            new HashSet<string>(StringComparer.Ordinal) { ProductAbbreviationPermissions.Cancel },
            "mongo-correlation");
        return new ProductAbbreviationWorkflow(
            scope.Register(scope.TenantA),
            scope.Ledger(scope.TenantA),
            scope.History(scope.TenantA),
            DispatchProxy.Create<IGlobalProductRepository, ThrowingProxy>(),
            actor,
            new ProductAbbreviationAuthorization(actor));
    }

    private static async Task<List<MongoDB.Bson.BsonDocument>> ReadIndexesAsync(
        IMongoCollection<MongoDB.Bson.BsonDocument> collection)
    {
        using var cursor = await collection.Indexes.ListAsync();
        return await cursor.ToListAsync();
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
            var uri = Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? "mongodb://localhost:27017";
            var settings = MongoClientSettings.FromConnectionString(uri);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
#pragma warning disable CS0618
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var databaseName = "DitenERP_MOD0290_FU01_Test_" + Guid.NewGuid().ToString("N");
            var database = client.GetDatabase(databaseName);
            await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1));
            return new MongoScope(client, database, databaseName);
        }

        public ProductAbbreviationAllocationLedgerRepository Ledger(Guid tenantId)
            => new(Database, Context(tenantId));

        public ProductAbbreviationRegisterRepository Register(Guid tenantId)
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

    private sealed record TestActorContext(
        Guid Tenant,
        bool TenantResolved,
        bool Authenticated,
        string ActorTypeValue,
        string Subject,
        IReadOnlySet<string> Permissions,
        string Correlation) : IProductAbbreviationActorContext
    {
        public Guid TenantId => Tenant;
        public bool TenantIsResolved => TenantResolved;
        public bool IsAuthenticated => Authenticated;
        public string ActorType => ActorTypeValue;
        public string CanonicalHumanSubjectId => Subject;
        public IReadOnlySet<string> GrantedPermissions => Permissions;
        public string CorrelationId => Correlation;
    }

    private class ThrowingProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException($"Repository access was not expected: {targetMethod?.Name}");
    }
}
