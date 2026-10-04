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

public sealed class ProductAbbreviationRegisterMongoTests(Diten.MdmService.Application.Tests.Audit.AuditIntentTemporalMongoFixture mongo)
    : IClassFixture<Diten.MdmService.Application.Tests.Audit.AuditIntentTemporalMongoFixture>
{
    private static readonly string Maker = Guid.NewGuid().ToString("D");
    private static readonly string Checker = Guid.NewGuid().ToString("D");
    private static readonly string Checker2 = Guid.NewGuid().ToString("D");

    [Fact]
    public async Task Required_ledger_and_active_binding_indexes_exist_with_correct_partiality()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
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
    public async Task The_ledger_rules_exist_where_only_the_register_was_ever_built()
    {
        // FIX3 item 3 — the register writes the ledger row inside its own transaction; the uniqueness of a code must
        // not depend on someone having built the ledger repository first.
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        await scope.Database.DropCollectionAsync("mdm_product_abbreviation_allocation_ledger");
        _ = scope.Register(scope.TenantA);

        var ledgerIndexes = await ReadIndexesAsync(
            scope.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_product_abbreviation_allocation_ledger"));
        Assert.Contains(ledgerIndexes, item => item["name"] == "ux_mdm_product_abbreviation_ledger_tenant_abbreviation"
                                               && item["unique"].AsBoolean);
    }

    [Fact]
    public async Task Tenant_isolation_allows_same_ABB_in_another_tenant_and_hides_cross_tenant_entry()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var entryA = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "ABC", "tenant-a");
        var entryB = await CreateRequestedAsync(scope, scope.TenantB, Guid.NewGuid(), "ABC", "tenant-b");

        Assert.NotEqual(entryA.Id, entryB.Id);
        Assert.Null(await scope.Register(scope.TenantB).GetByIdAsync(entryA.Id));
        Assert.Null(await scope.Register(scope.TenantA).GetByIdAsync(entryB.Id));
    }

    [Fact]
    public async Task Concurrent_same_tenant_ABB_requests_have_one_durable_winner()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(index =>
            ProductAbbreviationTestWrites.RequestAsync(
                scope.Register(scope.TenantA), Guid.NewGuid(), "ONE", $"parallel-{index}-{Guid.NewGuid():N}", Maker)));

        Assert.Single(results, result => result.Succeeded);
        Assert.All(results.Where(result => !result.Succeeded), result =>
            Assert.Contains(result.ErrorCode, new[] { "ABBREVIATION_ALREADY_ALLOCATED", "CONCURRENCY_CONFLICT" }));
        Assert.Equal(1, await scope.Database
            .GetCollection<ProductAbbreviationAllocationLedger>("mdm_product_abbreviation_allocation_ledger")
            .CountDocumentsAsync(x => x.TenantId == scope.TenantA && x.NormalizedAbbreviation == "ONE"));
    }

    [Fact]
    public async Task Partial_unique_index_allows_at_most_one_active_ABB_per_product()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var productId = Guid.NewGuid();
        var first = await CreateRequestedAsync(scope, scope.TenantA, productId, "AAA", "first");
        var repository = scope.Register(scope.TenantA);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Approve, first.Id, 0, Checker, "approve-first")).Succeeded);
        // A second REQUESTED entry of the same product cannot be made through the register any more (FIX1 S2): it is
        // written straight to the collection, so the database's own last line of defence is what is measured.
        var second = await InsertRawRequestedAsync(scope, scope.TenantA, productId, "BBB");

        var secondApproval = await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Approve, second.Id, 0, Checker, "approve-second");

        Assert.False(secondApproval.Succeeded);
        Assert.Equal("ACTIVE_ABBREVIATION_CONFLICT", secondApproval.ErrorCode);
    }

    [Fact]
    public async Task Durable_ledger_tombstone_is_idempotent_and_never_reusable()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var register = scope.Register(scope.TenantA);
        var product = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var created = await ProductAbbreviationTestWrites.RequestAsync(register, product, "NVR", "no-reuse", Maker, entryId: entryId);
        var replay = await ProductAbbreviationTestWrites.RequestAsync(register, product, "NVR", "no-reuse", Maker, entryId: entryId);
        var drift = await ProductAbbreviationTestWrites.RequestAsync(register, Guid.NewGuid(), "NVR", "no-reuse", Maker, entryId: entryId);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            register, ProductAbbreviationDecisionKind.Reject, entryId, 0, Checker, "reject-nvr")).Succeeded);
        var reuse = await ProductAbbreviationTestWrites.RequestAsync(register, Guid.NewGuid(), "NVR", "new-command", Maker);

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
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
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
        var history = await scope.History(scope.TenantA).GetForRegisterEntryAsync(entry.Id);
        Assert.Single(history, row => row.EventType == ProductAbbreviationHistoryEventType.ALLOCATION_CANCELLED);
        var intent = Assert.Single(persisted.AuditIntents,
            x => x.Operation == ProductAuditOperation.ProductAbbreviationAllocationCancelled);
        Assert.Equal(AuditAggregateType.ProductAbbreviation, intent.AggregateType);
        Assert.Equal(0, intent.PreVersion);
        Assert.Equal(1, intent.PostVersion);
        Assert.False(await scope.Register(scope.TenantB).AppendAuditIntentIfAbsentAsync(entry.Id, intent));
        Assert.True(await scope.Register(scope.TenantA).AppendAuditIntentIfAbsentAsync(entry.Id, intent));
        Assert.Single((await scope.Register(scope.TenantA).GetByIdAsync(entry.Id))!.AuditIntents,
            x => x.IntentId == intent.IntentId);

        var delivery = new AuditIntentDeliveryRepository(
            scope.Database,
            scope.Context(scope.TenantA),
            TimeProvider.System);
        foreach (var workItem in (await delivery.DiscoverEligibleAsync(10)).Where(item => item.Locator.AggregateId == entry.Id))
        {
            var claim = Assert.IsType<AuditIntentClaim>(await delivery.TryClaimAsync(
                workItem.Locator,
                workItem.ClaimGeneration,
                "abb-audit-worker",
                TimeSpan.FromMinutes(1)));
            const string contractVersion = "mod-0290.audit-intent.v1";
            var acknowledgement = new AuditIntentAcknowledgement(
                "abb-central-receipt",
                AuditIntentContract.BuildCentralIdempotencyKey(tenantId: scope.TenantA, intentId: workItem.Locator.IntentId,
                    contractVersion),
                contractVersion,
                DateTimeOffset.UtcNow);
            Assert.True(await delivery.MarkDeliveredAsync(claim, acknowledgement));
            Assert.True(await delivery.CompactDeliveredAsync(claim, "abb-compact-receipt"));
        }

        var delivered = await scope.Register(scope.TenantA).GetByIdAsync(entry.Id);
        Assert.Empty(delivered!.AuditIntents);
        Assert.Contains(delivered.AuditIntentReceipts, x => x.IntentId == intent.IntentId);
        Assert.Equal(1, delivered.Version);
        Assert.Empty(await delivery.DiscoverEligibleAsync(10));

        Assert.True(await scope.Register(scope.TenantA).AppendAuditIntentIfAbsentAsync(entry.Id, intent));
        Assert.Empty((await scope.Register(scope.TenantA).GetByIdAsync(entry.Id))!.AuditIntents);
        intent.EvidenceHash = new string('B', 64);
        Assert.False(await scope.Register(scope.TenantA).AppendAuditIntentIfAbsentAsync(entry.Id, intent));
        Assert.Empty((await scope.Register(scope.TenantA).GetByIdAsync(entry.Id))!.AuditIntents);

        var reuse = await ProductAbbreviationTestWrites.RequestAsync(
            scope.Register(scope.TenantA), Guid.NewGuid(), "OWN", "owner-cancel-reuse", Maker);
        Assert.False(reuse.Succeeded);
        Assert.Equal("ABBREVIATION_ALREADY_ALLOCATED", reuse.ErrorCode);
    }

    [Fact]
    public async Task Same_tenant_non_owner_cancel_denial_changes_no_register_ledger_or_history_state()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
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
        Assert.Equal(entry.LastDecisionIdempotencyKey, persisted.LastDecisionIdempotencyKey);
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
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var entry = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "CAS", "cas");
        var repository = scope.Register(scope.TenantA);

        var stale = await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Approve, entry.Id, 7, Checker, "stale");
        var persisted = await repository.GetByIdAsync(entry.Id);

        Assert.False(stale.Succeeded);
        Assert.Equal("CONCURRENCY_CONFLICT", stale.ErrorCode);
        Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, persisted!.LifecycleStatus);
        Assert.Equal(0, persisted.Version);
    }

    [Fact]
    public async Task Correction_approval_reconciles_to_replacement_active_and_former_retired()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var productId = Guid.NewGuid();
        var former = await CreateRequestedAsync(scope, scope.TenantA, productId, "OLD", "old");
        var repository = scope.Register(scope.TenantA);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Approve, former.Id, 0, Checker, "activate-old")).Succeeded);
        var replacement = await CreateRequestedAsync(
            scope, scope.TenantA, productId, "NEW", "replacement", former.Id);

        var result = await repository.ApproveCorrectionAsync(
            ProductAbbreviationFix1MongoTests.Approval(former.Id, 1, replacement.Id, 0, "approve-correction"));
        var replay = await repository.ApproveCorrectionAsync(
            ProductAbbreviationFix1MongoTests.Approval(former.Id, 1, replacement.Id, 0, "approve-correction"));

        Assert.True(result.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(ProductAbbreviationLifecycleStatus.RETIRED, (await repository.GetByIdAsync(former.Id))!.LifecycleStatus);
        Assert.Equal(ProductAbbreviationLifecycleStatus.ACTIVE, (await repository.GetByIdAsync(replacement.Id))!.LifecycleStatus);
        foreach (var abbreviation in new[] { "OLD", "NEW" })
        {
            var reuse = await ProductAbbreviationTestWrites.RequestAsync(
                repository, Guid.NewGuid(), abbreviation, $"reuse-{abbreviation}", Maker);
            Assert.Equal("ABBREVIATION_ALREADY_ALLOCATED", reuse.ErrorCode);
        }
    }

    [Fact]
    public async Task Retirement_request_reject_and_later_checker_approval_preserve_closed_lifecycle()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var entry = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "RET", "ret");
        var repository = scope.Register(scope.TenantA);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Approve, entry.Id, 0, Checker, "activate")).Succeeded);

        var requested = await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.RequestRetirement, entry.Id, 1, Maker, "retirement-1");
        var rejected = await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.RejectRetirement, entry.Id, requested.Entry!.Version, Checker,
            "reject-retirement", retirementRequestId: "retirement-1");
        var requestedAgain = await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.RequestRetirement, entry.Id, rejected.Entry!.Version, Maker,
            "retirement-2");
        var approved = await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.ApproveRetirement, entry.Id, requestedAgain.Entry!.Version,
            Checker2, "approve-retirement", retirementRequestId: "retirement-2");

        Assert.True(rejected.Succeeded);
        Assert.Equal(ProductAbbreviationLifecycleStatus.ACTIVE, rejected.Entry.LifecycleStatus);
        Assert.True(approved.Succeeded);
        Assert.Equal(ProductAbbreviationLifecycleStatus.RETIRED, approved.Entry!.LifecycleStatus);
        Assert.Null(approved.Entry.RetirementRequestId);
    }

    [Fact]
    public async Task A_second_retirement_request_while_one_waits_is_refused_and_the_first_stays()
    {
        // FIX3 item 8 — REG RetirementStateHolds (no request already waiting) and RetirementFilter (its stored half),
        // through the production decision path.
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var entry = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "SRQ", "srq");
        var repository = scope.Register(scope.TenantA);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Approve, entry.Id, 0, Checker, "activate-srq")).Succeeded);
        var first = await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.RequestRetirement, entry.Id, 1, Maker, "retire-first");
        Assert.True(first.Succeeded);

        var sameVersion = await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.RequestRetirement, entry.Id, first.Entry!.Version, Checker,
            "retire-second");

        Assert.False(sameVersion.Succeeded);
        Assert.Equal("CONCURRENCY_CONFLICT", sameVersion.ErrorCode);
        var stored = (await repository.GetByIdAsync(entry.Id))!;
        Assert.Equal("retire-first", stored.RetirementRequestId);
        Assert.Equal(Maker, stored.RetirementRequestedByCanonicalSubjectId);
    }

    [Fact]
    public async Task A_retirement_request_repeated_by_someone_else_with_its_key_is_refused_not_reported_as_theirs()
    {
        // FIX3 item 8 — the legacy RequestRetirementAsync accepted a repeat from anyone; the production path answers
        // a repeat as a replay only for the person who asked (Recognise), everyone else is refused.
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var entry = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "RRK", "rrk");
        var repository = scope.Register(scope.TenantA);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Approve, entry.Id, 0, Checker, "activate-rrk")).Succeeded);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.RequestRetirement, entry.Id, 1, Maker, "retire-rrk")).Succeeded);

        var mine = await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.RequestRetirement, entry.Id, 1, Maker, "retire-rrk");
        var theirs = await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.RequestRetirement, entry.Id, 1, Checker, "retire-rrk");

        Assert.True(mine.Succeeded);
        Assert.True(mine.IsReplay);
        Assert.False(theirs.Succeeded);
        Assert.Equal(ProductAbbreviationRegisterRepository.AlreadyDecidedByAnother, theirs.ErrorCode);
    }

    [Fact]
    public async Task Reject_cancel_retire_and_soft_delete_never_release_a_durable_ABB()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
        var repository = scope.Register(scope.TenantA);
        var rejected = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "RJT", "reject-path");
        var cancelled = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "CNL", "cancel-path");
        var retired = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "RTR", "retire-path");
        var deleted = await CreateRequestedAsync(scope, scope.TenantA, Guid.NewGuid(), "DEL", "delete-path");

        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Reject, rejected.Id, 0, Checker, "reject")).Succeeded);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Cancel, cancelled.Id, 0, Maker, "cancel")).Succeeded);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.Approve, retired.Id, 0, Checker, "activate-retire")).Succeeded);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.RequestRetirement, retired.Id, 1, Maker, "retire-request")).Succeeded);
        Assert.True((await ProductAbbreviationTestWrites.DecideAsync(
            repository, ProductAbbreviationDecisionKind.ApproveRetirement, retired.Id, 2, Checker, "retire",
            retirementRequestId: "retire-request")).Succeeded);
        await scope.Database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register")
            .UpdateOneAsync(
                Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Id, deleted.Id),
                Builders<ProductAbbreviationRegisterEntry>.Update
                    .Set(x => x.IsDeleted, true)
                    .Set(x => x.DeletedAt, DateTimeOffset.UtcNow));

        foreach (var abbreviation in new[] { "RJT", "CNL", "RTR", "DEL" })
        {
            var reuse = await ProductAbbreviationTestWrites.RequestAsync(
                repository, Guid.NewGuid(), abbreviation, $"reuse-{abbreviation}", Maker);
            Assert.False(reuse.Succeeded);
            Assert.Equal("ABBREVIATION_ALREADY_ALLOCATED", reuse.ErrorCode);
        }
    }

    [Fact]
    public async Task Immutable_history_append_is_idempotent_and_payload_drift_fails_closed()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString);
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

    private static async Task<ProductAbbreviationRegisterEntry> CreateRequestedAsync(
        MongoScope scope,
        Guid tenantId,
        Guid productId,
        string abbreviation,
        string idempotencyKey,
        Guid? replacesEntryId = null,
        string? requestedBy = null)
    {
        var inserted = await ProductAbbreviationTestWrites.RequestAsync(
            scope.Register(tenantId), productId, abbreviation, idempotencyKey, requestedBy ?? Maker, replacesEntryId);
        Assert.True(inserted.Succeeded, inserted.ErrorCode);
        return inserted.Entry!;
    }

    /// <summary>A REQUESTED row written straight to the collection — for a rule only the database itself enforces.</summary>
    private static async Task<ProductAbbreviationRegisterEntry> InsertRawRequestedAsync(
        MongoScope scope,
        Guid tenantId,
        Guid productId,
        string abbreviation)
    {
        var entry = new ProductAbbreviationRegisterEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GlobalProductId = productId,
            NormalizedAbbreviation = abbreviation,
            AllocationLedgerId = Guid.NewGuid(),
            AllocationIdempotencyKey = "raw-" + Guid.NewGuid().ToString("N"),
            LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
            RequestedByCanonicalSubjectId = Maker,
            RequestedAtUtc = DateTimeOffset.UtcNow
        };
        await scope.Database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register")
            .InsertOneAsync(entry);
        return entry;
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

        public static async Task<MongoScope> CreateAsync(string connectionString)
        {
            var uri = connectionString;
            var settings = MongoClientSettings.FromConnectionString(uri);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
#pragma warning disable CS0618
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var databaseName = "diten_mdm_abb_register_itest";
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

        public async ValueTask DisposeAsync()
        {
            foreach (var name in new[] { "mdm_product_abbreviation_register", "mdm_product_abbreviation_history", "mdm_product_abbreviation_allocation_ledger", "mdm_product_abbreviation_command_keys" })
                await Database.GetCollection<MongoDB.Bson.BsonDocument>(name).DeleteManyAsync(
                    MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.In("TenantId", new[] { TenantA, TenantB }));
        }
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
