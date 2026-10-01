using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using Diten.MdmService.Application.Tests.Audit;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class FinishedGoodDraftFoundationMongoTests(
    AuditIntentTemporalMongoFixture mongo,
    ITestOutputHelper output) : IClassFixture<AuditIntentTemporalMongoFixture>
{
    [Fact]
    public async Task Identity_approved_gskus_allow_many_finished_goods_with_idempotent_replay()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var draft = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var approved = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);

        var first = await scope.Create(scope.TenantA, draft.Id, " first-command ");
        var replay = await scope.Create(scope.TenantA, draft.Id, "FIRST-COMMAND");
        var second = await scope.Create(scope.TenantA, draft.Id, "second-command");
        var third = await scope.Create(scope.TenantA, approved.Id, "approved-command");

        Assert.True(first.IsSuccessful, string.Join(',', first.Errors));
        Assert.True(replay.IsSuccessful, string.Join(',', replay.Errors));
        Assert.True(second.IsSuccessful, string.Join(',', second.Errors));
        Assert.True(third.IsSuccessful, string.Join(',', third.Errors));
        Assert.Equal(first.Data!.FinishedGoodId, replay.Data!.FinishedGoodId);
        Assert.Equal(draft.Id, first.Data.GskuId);
        Assert.Equal(draft.CanonicalCode, first.Data.GskuDisplay());
        Assert.NotEqual(first.Data.CanonicalCode, second.Data!.CanonicalCode);
        Assert.Equal(3, await scope.FinishedGoods.CountDocumentsAsync(Builders<FinishedGood>.Filter.Eq(item => item.TenantId, scope.TenantA)));
        Assert.Equal(3, await scope.Reservations.CountDocumentsAsync(
            Builders<CodeReservation>.Filter.Eq(x => x.TenantId, scope.TenantA)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)));
    }

    [Theory]
    [InlineData(ProductIdentityLifecycleStatus.PendingIdentityApproval)]
    [InlineData(ProductIdentityLifecycleStatus.Retired)]
    public async Task Non_referenceable_gsku_is_rejected_before_reservation(ProductIdentityLifecycleStatus status)
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var gsku = await scope.InsertGskuAsync(scope.TenantA, status);

        var result = await scope.Create(scope.TenantA, gsku.Id, "blocked-status");

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("GSKU_NOT_REFERENCEABLE", result.Errors);
        Assert.Equal(0, await scope.Reservations.CountDocumentsAsync(Builders<CodeReservation>.Filter.Eq(item => item.TenantId, scope.TenantA)));
        Assert.Equal(0, await scope.FinishedGoods.CountDocumentsAsync(Builders<FinishedGood>.Filter.Eq(item => item.TenantId, scope.TenantA)));
        Assert.Empty(await scope.Attempts.Find(item => item.TenantId == scope.TenantA).ToListAsync());
    }

    [Fact]
    public async Task Missing_cross_tenant_and_soft_deleted_gskus_are_indistinguishably_rejected_before_reservation()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var foreign = await scope.InsertGskuAsync(scope.TenantB, ProductIdentityLifecycleStatus.IdentityApproved);
        var deleted = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved, isDeleted: true);

        var results = new[]
        {
            await scope.Create(scope.TenantA, Guid.NewGuid(), "missing"),
            await scope.Create(scope.TenantA, foreign.Id, "foreign"),
            await scope.Create(scope.TenantA, deleted.Id, "deleted")
        };

        Assert.All(results, result =>
        {
            Assert.False(result.IsSuccessful);
            Assert.Equal(404, result.StatusCode);
            Assert.Contains("GSKU_NOT_REFERENCEABLE", result.Errors);
        });
        Assert.Equal(0, await scope.Reservations.CountDocumentsAsync(Builders<CodeReservation>.Filter.Eq(item => item.TenantId, scope.TenantA)));
        Assert.Empty(await scope.Attempts.Find(item => item.TenantId == scope.TenantA).ToListAsync());
    }

    [Fact]
    public async Task Conflicting_replay_and_tombstoned_command_never_allocate_a_second_code()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var firstGsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var otherGsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var created = await scope.Create(scope.TenantA, firstGsku.Id, "stable-command");
        var drift = await scope.Create(scope.TenantA, otherGsku.Id, "stable-command");
        await scope.FinishedGoods.UpdateOneAsync(
            item => item.TenantId == scope.TenantA && item.Id == created.Data!.FinishedGoodId,
            Builders<FinishedGood>.Update.Set(item => item.IsDeleted, true).Set(item => item.DeletedAt, DateTimeOffset.UtcNow));
        var tombstoneReplay = await scope.Create(scope.TenantA, firstGsku.Id, "stable-command");

        Assert.False(drift.IsSuccessful);
        Assert.Equal(409, drift.StatusCode);
        Assert.Contains("IDEMPOTENCY_KEY_CONFLICT", drift.Errors);
        Assert.False(tombstoneReplay.IsSuccessful);
        Assert.Equal(409, tombstoneReplay.StatusCode);
        Assert.Contains("CREATION_COMMAND_TOMBSTONED", tombstoneReplay.Errors);
        Assert.Equal(1, await scope.Reservations.CountDocumentsAsync(
            Builders<CodeReservation>.Filter.Eq(x => x.TenantId, scope.TenantA)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)));
        Assert.Equal(1, await scope.FinishedGoods.CountDocumentsAsync(Builders<FinishedGood>.Filter.Eq(item => item.TenantId, scope.TenantA)));

        var replacement = await scope.Create(scope.TenantA, firstGsku.Id, "replacement-command");
        Assert.True(replacement.IsSuccessful);
        Assert.NotEqual(created.Data!.CanonicalCode, replacement.Data!.CanonicalCode);
        Assert.Equal(2, await scope.Reservations.CountDocumentsAsync(
            Builders<CodeReservation>.Filter.Eq(x => x.TenantId, scope.TenantA)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)));
    }

    [Fact]
    public async Task Completed_and_tombstoned_same_target_replay_survive_parent_retirement_without_new_mutation()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        const string completedKey = "REPLAY-REFERENCEABILITY";
        const string tombstoneKey = "TOMBSTONE-REFERENCEABILITY";
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var completed = await scope.Create(scope.TenantA, gsku.Id, completedKey);
        var tombstoned = await scope.Create(scope.TenantA, gsku.Id, tombstoneKey);
        Assert.True(completed.IsSuccessful);
        Assert.True(tombstoned.IsSuccessful);
        await scope.FinishedGoods.UpdateOneAsync(
            item => item.TenantId == scope.TenantA && item.Id == tombstoned.Data!.FinishedGoodId,
            Builders<FinishedGood>.Update.Set(item => item.IsDeleted, true).Set(item => item.DeletedAt, DateTimeOffset.UtcNow));
        await scope.Database.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(
            item => item.TenantId == scope.TenantA && item.Id == gsku.Id,
            Builders<Gsku>.Update.Set(item => item.LifecycleStatus, ProductIdentityLifecycleStatus.Retired));
        var before = await scope.ReadStateAsync(scope.TenantA, "frozen same-target replay after parent retirement");

        var completedReplay = await scope.Create(scope.TenantA, gsku.Id, completedKey);
        var tombstoneReplay = await scope.Create(scope.TenantA, gsku.Id, tombstoneKey);
        var after = await scope.ReadStateAsync(scope.TenantA, "frozen same-target parent-retirement replay no-new-mutation");

        Assert.True(completedReplay.IsSuccessful, string.Join(',', completedReplay.Errors));
        Assert.Equal(201, completedReplay.StatusCode);
        Assert.Equal(completed.Data!.FinishedGoodId, completedReplay.Data!.FinishedGoodId);
        Assert.False(tombstoneReplay.IsSuccessful);
        Assert.Equal(409, tombstoneReplay.StatusCode);
        Assert.Contains("CREATION_COMMAND_TOMBSTONED", tombstoneReplay.Errors);
        Assert.Equal(before.RawState, after.RawState);
    }

    [Fact]
    public async Task Existing_completed_or_tombstoned_command_with_foreign_or_nonreferenceable_requested_gsku_is_indistinguishably_rejected()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        const string completedKey = "REPLAY-ORACLE-COMPLETED";
        const string tombstoneKey = "REPLAY-ORACLE-TOMBSTONE";
        var original = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var foreign = await scope.InsertGskuAsync(scope.TenantB, ProductIdentityLifecycleStatus.IdentityApproved);
        var retired = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Retired);
        var completed = await scope.Create(scope.TenantA, original.Id, completedKey);
        var tombstoned = await scope.Create(scope.TenantA, original.Id, tombstoneKey);
        Assert.True(completed.IsSuccessful);
        Assert.True(tombstoned.IsSuccessful);
        await scope.FinishedGoods.UpdateOneAsync(
            item => item.TenantId == scope.TenantA && item.Id == tombstoned.Data!.FinishedGoodId,
            Builders<FinishedGood>.Update.Set(item => item.IsDeleted, true).Set(item => item.DeletedAt, DateTimeOffset.UtcNow));
        var beforeA = await scope.ReadStateAsync(scope.TenantA, "replay oracle before invalid target");
        var beforeB = await scope.ReadStateAsync(scope.TenantB, "replay oracle foreign tenant before invalid target");

        var results = new[]
        {
            await scope.Create(scope.TenantA, foreign.Id, completedKey),
            await scope.Create(scope.TenantA, retired.Id, completedKey),
            await scope.Create(scope.TenantA, foreign.Id, tombstoneKey),
            await scope.Create(scope.TenantA, retired.Id, tombstoneKey)
        };
        var afterA = await scope.ReadStateAsync(scope.TenantA, "replay oracle after invalid target");
        var afterB = await scope.ReadStateAsync(scope.TenantB, "replay oracle foreign tenant after invalid target");

        Assert.All(results, result =>
        {
            Assert.False(result.IsSuccessful);
            Assert.Equal(404, result.StatusCode);
            Assert.Contains("GSKU_NOT_REFERENCEABLE", result.Errors);
            Assert.DoesNotContain("IDEMPOTENCY_KEY_CONFLICT", result.Errors);
            Assert.DoesNotContain("CREATION_COMMAND_TOMBSTONED", result.Errors);
        });
        Assert.Equal(beforeA.RawState, afterA.RawState);
        Assert.Equal(beforeB.RawState, afterB.RawState);
    }

    [Fact]
    public async Task Concurrent_same_command_has_one_identity_and_one_consumed_confirmed_reservation()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => scope.Create(scope.TenantA, gsku.Id, "concurrent-command")));

        Assert.All(results, result => Assert.True(result.IsSuccessful, string.Join(',', result.Errors)));
        Assert.Single(results.Select(result => result.Data!.FinishedGoodId).Distinct());
        var reservation = Assert.Single(await scope.Reservations.Find(
            Builders<CodeReservation>.Filter.Eq(x => x.TenantId, scope.TenantA)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)).ToListAsync());
        Assert.Equal(CodeReservationState.Consumed, reservation.ReservationState);
        Assert.Equal(CodeReservationBindingState.Confirmed, reservation.BindingState);
        Assert.Equal(1, await scope.FinishedGoods.CountDocumentsAsync(Builders<FinishedGood>.Filter.Eq(item => item.TenantId, scope.TenantA)));
        var attempt = Assert.Single(await scope.Attempts.Find(item => item.TenantId == scope.TenantA).ToListAsync());
        Assert.Equal(gsku.Id, attempt.GskuId);
        Assert.Equal("CONCURRENT-COMMAND", attempt.CreationCommandId);
    }

    [Fact]
    public async Task Concurrent_same_key_different_gskus_has_one_durable_binding_and_a_side_effect_free_loser()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var first = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var second = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);

        var results = await Task.WhenAll(
            scope.Create(scope.TenantA, first.Id, "concurrent-parent-drift"),
            scope.Create(scope.TenantA, second.Id, "concurrent-parent-drift"));

        var winner = Assert.Single(results.Where(result => result.IsSuccessful));
        var loser = Assert.Single(results.Where(result => !result.IsSuccessful));
        Assert.Equal(409, loser.StatusCode);
        Assert.Contains("IDEMPOTENCY_KEY_CONFLICT", loser.Errors);
        var attempt = Assert.Single(await scope.Attempts.Find(item => item.TenantId == scope.TenantA).ToListAsync());
        Assert.Equal("CONCURRENT-PARENT-DRIFT", attempt.CreationCommandId);
        var stored = Assert.Single(await scope.FinishedGoods.Find(item => item.TenantId == scope.TenantA).ToListAsync());
        Assert.Equal(attempt.GskuId, stored.GskuId);
        Assert.Equal(winner.Data!.FinishedGoodId, stored.Id);
        Assert.Single(await scope.Reservations.Find(item => item.TenantId == scope.TenantA).ToListAsync());
        Assert.All((await scope.ReadStateAsync(scope.TenantA, "concurrent changed-parent result")).Parents
            .Where(parent => parent.Id != attempt.GskuId), parent => Assert.Empty(parent.ChildCreationAdmissions));
    }

    [Fact]
    public async Task Pre_cancelled_real_mongo_create_propagates_cancellation_without_reservation_or_identity_write()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var context = scope.Context(scope.TenantA);
        var access = ProductLegalEntityScopeTestFixture.Preparation(context);
        var handler = new CreateFinishedGoodDraftHandler(
            new CodeReservationRepository(scope.Database, context),
            new FinishedGoodRepository(scope.Database, context),
            new GskuRepository(scope.Database, context),
            new ProductDefinitionRevisionRepository(scope.Database, context),
            new GlobalProductRepository(scope.Database, context),
            context,
            new ActorContext(),
            access.Rollouts,
            access.Policies,
            access.Candidates);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(
            new CreateFinishedGoodDraftCommand(new()
            {
                GskuId = gsku.Id,
                IdempotencyKey = "cancelled-real-mongo-command"
            }),
            cancellation.Token));

        Assert.Equal(0, await scope.Reservations.CountDocumentsAsync(
            Builders<CodeReservation>.Filter.Eq(x => x.TenantId, scope.TenantA)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)));
        Assert.Equal(0, await scope.FinishedGoods.CountDocumentsAsync(Builders<FinishedGood>.Filter.Eq(item => item.TenantId, scope.TenantA)));
    }

    [Fact]
    public async Task Concurrent_distinct_commands_for_one_gsku_create_distinct_codes_without_a_cardinality_cap()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(index => scope.Create(scope.TenantA, gsku.Id, $"distinct-{index}")));

        Assert.All(results, result => Assert.True(result.IsSuccessful, string.Join(',', result.Errors)));
        Assert.Equal(8, results.Select(result => result.Data!.FinishedGoodId).Distinct().Count());
        Assert.Equal(8, results.Select(result => result.Data!.CanonicalCode).Distinct().Count());
        Assert.All(results, result => Assert.Equal(gsku.Id, result.Data!.GskuId));
        Assert.Equal(8, await scope.FinishedGoods.CountDocumentsAsync(
            Builders<FinishedGood>.Filter.Eq(item => item.TenantId, scope.TenantA)
            & Builders<FinishedGood>.Filter.Eq(item => item.GskuId, gsku.Id)));
    }

    [Fact]
    public async Task Stale_finished_good_binding_confirmation_is_rejected_without_aggregate_mutation()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var repository = new CodeReservationRepository(scope.Database, scope.Context(scope.TenantA));
        var reservation = await repository.ReserveAsync(
            CodeBearingEntityType.FinishedGood,
            "stale-binding-reserve",
            "actor",
            "correlation");
        var identityId = Guid.NewGuid();
        var consumed = await repository.ConsumeForIdentityAsync(
            reservation.Id,
            CodeBearingEntityType.FinishedGood,
            identityId,
            reservation.Version,
            "stale-binding-consume",
            "actor",
            "correlation");
        Assert.True(consumed.Succeeded);
        var stale = await repository.ConfirmIdentityBindingAsync(
                reservation.Id,
                identityId,
                0,
                "stale-confirm",
                "actor",
                "correlation");

        Assert.False(stale.Succeeded);
        Assert.Equal("CONCURRENCY_CONFLICT", stale.ErrorCode);
        Assert.Equal(0, await scope.FinishedGoods.CountDocumentsAsync(Builders<FinishedGood>.Filter.Eq(item => item.TenantId, scope.TenantA)));
        var storedReservation = await scope.Reservations.Find(item => item.TenantId == scope.TenantA && item.Id == reservation.Id).SingleAsync();
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, storedReservation.BindingState);
        Assert.Equal(identityId, storedReservation.ConsumedEntityId);
    }

    [Fact]
    public async Task List_detail_and_selector_are_tenant_scoped_bounded_and_code_only()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var draft = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved, "GS-000000000010");
        var approved = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved, "GS-000000000020");
        _ = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.PendingIdentityApproval, "GS-000000000030");
        _ = await scope.InsertGskuAsync(scope.TenantB, ProductIdentityLifecycleStatus.Draft, "GS-000000000040");
        var first = await scope.Create(scope.TenantA, approved.Id, "list-b");
        var second = await scope.Create(scope.TenantA, draft.Id, "list-a");

        var context = scope.Context(scope.TenantA);
        var access = ProductLegalEntityScopeTestFixture.Preparation(context);
        var repositories = scope.Repositories(scope.TenantA);
        var revisions = new ProductDefinitionRevisionRepository(scope.Database, context);
        var products = new GlobalProductRepository(scope.Database, context);
        var list = await new GetFinishedGoodsHandler(
            repositories.FinishedGoods, repositories.Gskus, revisions, products,
            access.Rollouts, access.Policies, access.Candidates, context).Handle(
            new GetFinishedGoodsQuery { Search = approved.CanonicalCode, PageSize = 20 }, CancellationToken.None);
        var detail = await new GetFinishedGoodByIdHandler(
            repositories.FinishedGoods, repositories.Gskus, revisions, products,
            access.Rollouts, access.Policies, access.Candidates, context).Handle(
            new GetFinishedGoodByIdQuery(first.Data!.FinishedGoodId), CancellationToken.None);
        var selector = await new GetFinishedGoodGskuSelectorHandler(
            repositories.Gskus, revisions, products,
            access.Rollouts, access.Policies, access.Candidates, context).Handle(
            new GetFinishedGoodGskuSelectorQuery { PageSize = 20 }, CancellationToken.None);

        Assert.Single(list.Data!.Items);
        Assert.Equal(first.Data.FinishedGoodId, list.Data.Items[0].Id);
        Assert.Equal(approved.CanonicalCode, list.Data.Items[0].GskuDisplay);
        Assert.Equal(approved.CanonicalCode, detail.Data!.GskuDisplay);
        Assert.Equal([draft.CanonicalCode, approved.CanonicalCode], selector.Data!.Items.Select(item => item.Display));
        Assert.All(selector.Data.Items, item => Assert.Equal(item.CanonicalCode, item.Display));
        Assert.NotEqual(first.Data.FinishedGoodId, second.Data!.FinishedGoodId);
    }

    [Fact]
    public async Task Finished_good_audit_delivery_is_fenced_acknowledged_compacted_and_version_neutral()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var created = await scope.Create(scope.TenantA, gsku.Id, "audit-command");
        var delivery = new AuditIntentDeliveryRepository(scope.Database, scope.Context(scope.TenantA), TimeProvider.System);
        var item = Assert.Single(
            await delivery.DiscoverEligibleAsync(100),
            work => work.Locator.AggregateType == AuditAggregateType.FinishedGood);
        var claim = Assert.IsType<AuditIntentClaim>(await delivery.TryClaimAsync(
            item.Locator, item.ClaimGeneration, "worker-a", TimeSpan.FromMinutes(5)));
        Assert.Null(await delivery.TryClaimAsync(item.Locator, item.ClaimGeneration, "worker-b", TimeSpan.FromMinutes(5)));
        var staleClaim = claim with { ClaimToken = "stale-token" };
        Assert.False(await delivery.MarkRetryableFailureAsync(staleClaim, TimeSpan.FromMinutes(1), "stale"));
        const string contractVersion = "finished-good-v1";
        var acknowledgement = new AuditIntentAcknowledgement(
            "central-ack",
            AuditIntentContract.BuildCentralIdempotencyKey(scope.TenantA, item.Locator.IntentId, contractVersion),
            contractVersion,
            DateTimeOffset.UtcNow);
        Assert.True(await delivery.MarkDeliveredAsync(claim, acknowledgement));
        Assert.True(await delivery.CompactDeliveredAsync(claim, "fg-receipt"));
        Assert.True(await delivery.CompactDeliveredAsync(claim, "fg-receipt"));

        var stored = await scope.FinishedGoods.Find(item => item.TenantId == scope.TenantA && item.Id == created.Data!.FinishedGoodId).SingleAsync();
        Assert.Equal(0, stored.Version);
        Assert.Empty(stored.AuditIntents);
        Assert.Single(stored.AuditIntentReceipts);
    }

    [Theory]
    [InlineData(PreInsertCrashPoint.AfterAdmissionBeforeReservation)]
    [InlineData(PreInsertCrashPoint.AfterConsumptionBeforeInsert)]
    public async Task Pre_insert_crash_same_parent_first_retry_preserves_one_identity_and_code(
        PreInsertCrashPoint crashPoint)
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var interrupted = await InterruptCreateAsync(scope, crashPoint);
        var beforeRetry = await scope.ReadStateAsync(scope.TenantA, "same-parent: before first retry");

        // A new handler and undecorated, real repositories simulate a restarted request.
        var retry = await scope.Create(scope.TenantA, interrupted.Parent.Id, interrupted.Key);
        var afterRetry = await scope.ReadStateAsync(scope.TenantA, "same-parent: after first retry");

        Assert.True(retry.IsSuccessful, string.Join(',', retry.Errors));
        var stored = Assert.Single(afterRetry.FinishedGoods);
        var reservation = Assert.Single(afterRetry.Reservations);
        Assert.Equal(interrupted.Parent.Id, stored.GskuId);
        Assert.Equal(retry.Data!.FinishedGoodId, stored.Id);
        Assert.Equal(stored.CodeReservationId, reservation.Id);
        Assert.Equal(stored.Id, reservation.ConsumedEntityId);
        Assert.Equal(stored.CanonicalCode, reservation.ReservedCode);
        Assert.Equal(CodeReservationState.Consumed, reservation.ReservationState);
        Assert.Equal(CodeReservationBindingState.Confirmed, reservation.BindingState);
        Assert.All(afterRetry.Parents, parent => Assert.Empty(parent.ChildCreationAdmissions));
        Assert.Equal(
            new[] { ProductAuditOperation.CodeReserved, ProductAuditOperation.CodeConsumed, ProductAuditOperation.CodeBindingConfirmed },
            reservation.AuditIntents.Select(intent => intent.Operation));
        Assert.Equal(ProductAuditOperation.FinishedGoodDraftCreated, Assert.Single(stored.AuditIntents).Operation);
        if (crashPoint == PreInsertCrashPoint.AfterConsumptionBeforeInsert)
        {
            var consumed = Assert.Single(beforeRetry.Reservations);
            Assert.Equal(consumed.Id, reservation.Id);
            Assert.Equal(consumed.ConsumedEntityId, stored.Id);
            Assert.Equal(consumed.ReservedCode, stored.CanonicalCode);
        }

        var replay = await scope.Create(scope.TenantA, interrupted.Parent.Id, interrupted.Key);
        var afterReplay = await scope.ReadStateAsync(scope.TenantA, "same-parent: after completed replay");
        Assert.True(replay.IsSuccessful, string.Join(',', replay.Errors));
        Assert.Equal(stored.Id, replay.Data!.FinishedGoodId);
        Assert.Equal(stored.CanonicalCode, replay.Data.CanonicalCode);
        Assert.Equal(afterRetry.RawState, afterReplay.RawState);
    }

    [Theory]
    [InlineData(PreInsertCrashPoint.AfterAdmissionBeforeReservation)]
    [InlineData(PreInsertCrashPoint.AfterConsumptionBeforeInsert)]
    public async Task Pre_insert_crash_changed_parent_first_retry_must_reject_without_creating_finished_good(
        PreInsertCrashPoint crashPoint)
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var interrupted = await InterruptCreateAsync(scope, crashPoint);
        var before = await scope.ReadStateAsync(scope.TenantA, "changed-parent: before FIRST retry");

        // Deliberately no successful same-parent insertion before this changed-parent FIRST retry.
        var drift = await scope.Create(scope.TenantA, interrupted.OtherParent.Id, interrupted.Key);
        var after = await scope.ReadStateAsync(scope.TenantA, "changed-parent: after FIRST retry");

        Assert.False(drift.IsSuccessful);
        Assert.Equal(409, drift.StatusCode);
        Assert.Contains("IDEMPOTENCY_KEY_CONFLICT", drift.Errors);
        Assert.Empty(after.FinishedGoods);
        // The attempt, original parent admission, other parent, reservation/code and local-audit state are all in RawState.
        Assert.Equal(before.RawState, after.RawState);
    }

    [Fact]
    public async Task Post_bind_interruption_recovers_only_the_bound_parent_and_rejects_changed_parent()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        const string key = "POST-BIND-INTERRUPTION";
        var parent = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var other = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var context = scope.Context(scope.TenantA);
        var interruptedRepository = new InterruptingFinishedGoodRepository(
            new FinishedGoodRepository(scope.Database, context));

        await Assert.ThrowsAsync<InjectedPostBindCrashException>(() =>
            scope.Create(scope.TenantA, parent.Id, key, finishedGoods: interruptedRepository));
        var afterCrash = await scope.ReadStateAsync(scope.TenantA, "post-bind interruption");
        Assert.Single(await scope.Attempts.Find(item => item.TenantId == scope.TenantA).ToListAsync());
        Assert.All(afterCrash.Parents, item => Assert.Empty(item.ChildCreationAdmissions));
        Assert.Empty(afterCrash.Reservations);
        Assert.Empty(afterCrash.FinishedGoods);

        var changed = await scope.Create(scope.TenantA, other.Id, key);
        var afterChanged = await scope.ReadStateAsync(scope.TenantA, "post-bind changed parent");
        Assert.False(changed.IsSuccessful);
        Assert.Equal(409, changed.StatusCode);
        Assert.Contains("IDEMPOTENCY_KEY_CONFLICT", changed.Errors);
        Assert.Equal(afterCrash.RawState, afterChanged.RawState);

        var recovered = await scope.Create(scope.TenantA, parent.Id, key);
        Assert.True(recovered.IsSuccessful, string.Join(',', recovered.Errors));
        var stored = Assert.Single((await scope.ReadStateAsync(scope.TenantA, "post-bind same-parent recovery")).FinishedGoods);
        Assert.Equal(parent.Id, stored.GskuId);
    }

    [Fact]
    public async Task Uncertain_binding_result_fails_closed_without_admission_allocation_or_finished_good_write()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        const string key = "UNCERTAIN-BINDING";
        var parent = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var context = scope.Context(scope.TenantA);
        var uncertain = new UncertainBindingResultFinishedGoodRepository(new FinishedGoodRepository(scope.Database, context));

        var result = await scope.Create(scope.TenantA, parent.Id, key, finishedGoods: uncertain);
        var state = await scope.ReadStateAsync(scope.TenantA, "uncertain binding result");

        Assert.False(result.IsSuccessful);
        Assert.Equal(202, result.StatusCode);
        Assert.Contains("FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED", result.Errors);
        Assert.Single(await scope.Attempts.Find(item => item.TenantId == scope.TenantA).ToListAsync());
        Assert.All(state.Parents, item => Assert.Empty(item.ChildCreationAdmissions));
        Assert.Empty(state.Reservations);
        Assert.Empty(state.FinishedGoods);
    }

    [Fact]
    public async Task Attemptless_legacy_admission_partial_requires_reconciliation_without_rebinding_another_parent()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        const string key = "LEGACY-ADMISSION-PARTIAL";
        var original = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var other = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var fingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
            original.Id, GskuChildIdentityKind.FinishedGood, key);
        var admission = await new GskuRepository(scope.Database, scope.Context(scope.TenantA))
            .AcquireChildCreationAdmissionAsync(original.Id, GskuChildIdentityKind.FinishedGood, key, fingerprint, DateTimeOffset.UtcNow);
        Assert.True(admission.Succeeded);
        var before = await scope.ReadStateAsync(scope.TenantA, "legacy admission partial");

        var result = await scope.Create(scope.TenantA, other.Id, key);
        var after = await scope.ReadStateAsync(scope.TenantA, "legacy admission partial rejected");

        Assert.False(result.IsSuccessful);
        Assert.Equal(202, result.StatusCode);
        Assert.Contains("FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED", result.Errors);
        Assert.Empty(await scope.Attempts.Find(item => item.TenantId == scope.TenantA).ToListAsync());
        Assert.Equal(before.RawState, after.RawState);
    }

    [Fact]
    public async Task Attemptless_legacy_consumed_reservation_partial_requires_reconciliation_without_rebinding_another_parent()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        const string key = "LEGACY-RESERVATION-PARTIAL";
        var original = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var other = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var reservations = new CodeReservationRepository(scope.Database, scope.Context(scope.TenantA));
        var reservation = await reservations.ReserveAsync(CodeBearingEntityType.FinishedGood, key, "legacy-actor", key);
        var consumed = await reservations.ConsumeForIdentityAsync(
            reservation.Id, CodeBearingEntityType.FinishedGood, Guid.NewGuid(), reservation.Version,
            key, "legacy-actor", key);
        Assert.True(consumed.Succeeded);
        var before = await scope.ReadStateAsync(scope.TenantA, "legacy consumed reservation partial");

        var result = await scope.Create(scope.TenantA, other.Id, key);
        var after = await scope.ReadStateAsync(scope.TenantA, "legacy consumed reservation partial rejected");

        Assert.False(result.IsSuccessful);
        Assert.Equal(202, result.StatusCode);
        Assert.Contains("FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED", result.Errors);
        Assert.Empty(await scope.Attempts.Find(item => item.TenantId == scope.TenantA).ToListAsync());
        Assert.Equal(before.RawState, after.RawState);
        Assert.Empty(after.FinishedGoods);
        Assert.Single(after.Reservations);
    }

    [Fact]
    public async Task Creation_attempt_unique_index_rejects_divergent_duplicate_binding_in_real_mongo()
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        _ = new FinishedGoodRepository(scope.Database, scope.Context(scope.TenantA));
        var indexes = await (await scope.Attempts.Indexes.ListAsync()).ToListAsync();
        var index = Assert.Single(indexes, item => item["name"] == "ux_mdm_finished_good_creation_attempts_tenant_command");
        Assert.True(index["unique"].AsBoolean);
        Assert.Equal(1, index["key"].AsBsonDocument["TenantId"].ToInt32());
        Assert.Equal(1, index["key"].AsBsonDocument["CreationCommandId"].ToInt32());

        var first = new FinishedGoodCreationAttempt
        {
            Id = Guid.NewGuid(), TenantId = scope.TenantA, GskuId = Guid.NewGuid(),
            CreationCommandId = "UNIQUE-ATTEMPT", RequestFingerprint = new string('A', 64), IsDeleted = false
        };
        await scope.Attempts.InsertOneAsync(first);
        var divergent = new FinishedGoodCreationAttempt
        {
            Id = Guid.NewGuid(), TenantId = scope.TenantA, GskuId = Guid.NewGuid(),
            CreationCommandId = first.CreationCommandId, RequestFingerprint = new string('B', 64), IsDeleted = false
        };

        await Assert.ThrowsAsync<MongoWriteException>(() => scope.Attempts.InsertOneAsync(divergent));
        Assert.Single(await scope.Attempts.Find(item => item.TenantId == scope.TenantA).ToListAsync());
    }

    [Theory]
    [InlineData(PreInsertCrashPoint.AfterAdmissionBeforeReservation)]
    [InlineData(PreInsertCrashPoint.AfterConsumptionBeforeInsert)]
    public async Task Pre_insert_crash_other_tenant_same_key_cannot_observe_or_mutate_original_partial_state(
        PreInsertCrashPoint crashPoint)
    {
        await using var scope = await MongoScope.CreateAsync(mongo.ReplicaConnectionString, output);
        var interrupted = await InterruptCreateAsync(scope, crashPoint);
        var foreignParent = await scope.InsertGskuAsync(scope.TenantB, ProductIdentityLifecycleStatus.IdentityApproved);
        _ = await scope.InsertGskuAsync(scope.TenantB, ProductIdentityLifecycleStatus.IdentityApproved);
        var beforeA = await scope.ReadStateAsync(scope.TenantA, "cross-tenant A: before B retry");
        var beforeB = await scope.ReadStateAsync(scope.TenantB, "cross-tenant B: before retry");
        Assert.Empty(beforeB.Reservations);
        Assert.Empty(beforeB.FinishedGoods);
        Assert.Empty(await scope.Attempts.Find(item => item.TenantId == scope.TenantB).ToListAsync());

        var denied = await scope.Create(scope.TenantB, interrupted.Parent.Id, interrupted.Key);
        var afterDeniedA = await scope.ReadStateAsync(scope.TenantA, "cross-tenant A: after foreign-parent rejection");
        var afterDeniedB = await scope.ReadStateAsync(scope.TenantB, "cross-tenant B: after foreign-parent rejection");
        Assert.False(denied.IsSuccessful);
        Assert.Equal(404, denied.StatusCode);
        Assert.Equal(beforeA.RawState, afterDeniedA.RawState);
        Assert.Equal(beforeB.RawState, afterDeniedB.RawState);

        var own = await scope.Create(scope.TenantB, foreignParent.Id, interrupted.Key);
        var afterA = await scope.ReadStateAsync(scope.TenantA, "cross-tenant A: after B own create");
        var afterB = await scope.ReadStateAsync(scope.TenantB, "cross-tenant B: after own create");
        Assert.True(own.IsSuccessful, string.Join(',', own.Errors));
        Assert.Equal(beforeA.RawState, afterA.RawState);
        var identity = Assert.Single(afterB.FinishedGoods);
        var reservation = Assert.Single(afterB.Reservations);
        Assert.Equal(foreignParent.Id, identity.GskuId);
        Assert.Equal(scope.TenantB, identity.TenantId);
        Assert.Equal(scope.TenantB, reservation.TenantId);
        Assert.Equal(identity.Id, reservation.ConsumedEntityId);
        Assert.Equal(identity.CodeReservationId, reservation.Id);
        Assert.Equal(CodeReservationBindingState.Confirmed, reservation.BindingState);
        Assert.All(afterB.Parents, parent => Assert.Empty(parent.ChildCreationAdmissions));
        Assert.Single(identity.AuditIntents);
        Assert.Equal(3, reservation.AuditIntents.Count);
        var tenantBAttempt = Assert.Single(await scope.Attempts.Find(item => item.TenantId == scope.TenantB).ToListAsync());
        Assert.Equal(foreignParent.Id, tenantBAttempt.GskuId);
        Assert.All(beforeA.Reservations, original =>
        {
            Assert.NotEqual(original.Id, reservation.Id);
            Assert.NotEqual(original.ConsumedEntityId, identity.Id);
        });
        // Tenant-isolated counters may legitimately produce the same visible canonical code.
    }

    private async Task<InterruptedCreate> InterruptCreateAsync(MongoScope scope, PreInsertCrashPoint crashPoint)
    {
        const string key = "PREINSERT-PROOF";
        var parent = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var otherParent = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);
        var initial = await scope.ReadStateAsync(scope.TenantA, $"{crashPoint}: before interruption");
        Assert.All(initial.Parents, item => Assert.Empty(item.ChildCreationAdmissions));
        Assert.Empty(initial.Reservations);
        Assert.Empty(initial.FinishedGoods);
        var decorator = new InterruptingReservationRepository(
            new CodeReservationRepository(scope.Database, scope.Context(scope.TenantA)), crashPoint);

        var exception = await Assert.ThrowsAsync<InjectedPreInsertCrashException>(
            () => scope.Create(scope.TenantA, parent.Id, key, decorator));
        Assert.Equal(crashPoint, exception.Point);
        Assert.Equal(1, decorator.InterruptionCount);
        var interrupted = await scope.ReadStateAsync(scope.TenantA, $"{crashPoint}: interrupted, before any retry");
        var attempt = Assert.Single(await scope.Attempts.Find(item => item.TenantId == scope.TenantA).ToListAsync());
        Assert.Equal(parent.Id, attempt.GskuId);
        Assert.Equal(key, attempt.CreationCommandId);
        Assert.Equal(GskuChildCreationAdmission.ComputeRequestFingerprint(
            parent.Id, GskuChildIdentityKind.FinishedGood, key), attempt.RequestFingerprint);
        Assert.Empty(interrupted.FinishedGoods);
        var admitted = Assert.Single(interrupted.Parents.Single(item => item.Id == parent.Id).ChildCreationAdmissions);
        Assert.Equal(GskuChildIdentityKind.FinishedGood, admitted.ChildKind);
        Assert.Equal(key, admitted.CreationCommandId);
        Assert.Equal(GskuChildCreationAdmission.ComputeRequestFingerprint(
            parent.Id, GskuChildIdentityKind.FinishedGood, key), admitted.RequestFingerprint);
        Assert.Empty(interrupted.Parents.Single(item => item.Id == otherParent.Id).ChildCreationAdmissions);
        if (crashPoint == PreInsertCrashPoint.AfterAdmissionBeforeReservation)
        {
            Assert.Empty(interrupted.Reservations);
        }
        else
        {
            var consumed = Assert.Single(interrupted.Reservations);
            Assert.Equal(CodeReservationState.Consumed, consumed.ReservationState);
            Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, consumed.BindingState);
            Assert.True(consumed.ConsumedEntityId.HasValue && consumed.ConsumedEntityId.Value != Guid.Empty);
            Assert.Equal(key, consumed.ConsumeCommandId);
            Assert.Equal(
                new[] { ProductAuditOperation.CodeReserved, ProductAuditOperation.CodeConsumed },
                consumed.AuditIntents.Select(intent => intent.Operation));
        }
        return new(parent, otherParent, key);
    }

    public enum PreInsertCrashPoint
    {
        AfterAdmissionBeforeReservation,
        AfterConsumptionBeforeInsert
    }

    private sealed record InterruptedCreate(Gsku Parent, Gsku OtherParent, string Key);
    private sealed record PersistedState(Gsku[] Parents, CodeReservation[] Reservations, FinishedGood[] FinishedGoods, string RawState);
    private sealed class InjectedPreInsertCrashException(PreInsertCrashPoint point) : Exception("Test-owned pre-insert interruption")
    {
        public PreInsertCrashPoint Point { get; } = point;
    }

    private sealed class InjectedPostBindCrashException : Exception
    {
        public InjectedPostBindCrashException() : base("Test-owned interruption after durable creation-attempt binding") { }
    }

    private abstract class FinishedGoodRepositoryDecorator(IFinishedGoodRepository inner) : IFinishedGoodRepository
    {
        protected IFinishedGoodRepository Inner { get; } = inner;

        public virtual Task<FinishedGoodCreationAttemptResult> BindCreationAttemptAsync(
            Guid gskuId, string normalizedCreationCommandId, string requestFingerprint,
            CancellationToken cancellationToken = default)
            => Inner.BindCreationAttemptAsync(gskuId, normalizedCreationCommandId, requestFingerprint, cancellationToken);

        public Task<FinishedGood?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Inner.GetByIdAsync(id, cancellationToken);

        public Task<FinishedGood?> GetByCreationCommandIdAsync(string creationCommandId, CancellationToken cancellationToken = default)
            => Inner.GetByCreationCommandIdAsync(creationCommandId, cancellationToken);

        public Task<FinishedGood?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default)
            => Inner.GetByReservationIdAsync(reservationId, cancellationToken);

        public Task<FinishedGoodPage> GetPageAsync(int pageNumber, int pageSize, string? canonicalCodeSearch,
            IReadOnlyCollection<Guid>? matchingGskuIds, CancellationToken cancellationToken = default)
            => Inner.GetPageAsync(pageNumber, pageSize, canonicalCodeSearch, matchingGskuIds, cancellationToken);

        public Task<FinishedGoodCreateResult> CreateDraftAsync(FinishedGood finishedGood,
            CancellationToken cancellationToken = default)
            => Inner.CreateDraftAsync(finishedGood, cancellationToken);

        public Task<FinishedGoodCreateResult> CreateDraftWithAdmissionAsync(FinishedGood finishedGood,
            string admissionFingerprint, CancellationToken cancellationToken = default)
            => Inner.CreateDraftWithAdmissionAsync(finishedGood, admissionFingerprint, cancellationToken);
    }

    private sealed class InterruptingFinishedGoodRepository(IFinishedGoodRepository inner)
        : FinishedGoodRepositoryDecorator(inner)
    {
        public override async Task<FinishedGoodCreationAttemptResult> BindCreationAttemptAsync(
            Guid gskuId, string normalizedCreationCommandId, string requestFingerprint,
            CancellationToken cancellationToken = default)
        {
            _ = await base.BindCreationAttemptAsync(gskuId, normalizedCreationCommandId, requestFingerprint, cancellationToken);
            throw new InjectedPostBindCrashException();
        }
    }

    private sealed class UncertainBindingResultFinishedGoodRepository(IFinishedGoodRepository inner)
        : FinishedGoodRepositoryDecorator(inner)
    {
        public override async Task<FinishedGoodCreationAttemptResult> BindCreationAttemptAsync(
            Guid gskuId, string normalizedCreationCommandId, string requestFingerprint,
            CancellationToken cancellationToken = default)
        {
            _ = await base.BindCreationAttemptAsync(gskuId, normalizedCreationCommandId, requestFingerprint, cancellationToken);
            return FinishedGoodCreationAttemptResult.Unavailable();
        }
    }

    // Only the interruption is synthetic. Every storage result comes from the real production repository.
    private sealed class InterruptingReservationRepository(
        ICodeReservationRepository inner,
        PreInsertCrashPoint point) : ICodeReservationRepository
    {
        public int InterruptionCount { get; private set; }
        public Task<CodeReservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => inner.GetByIdAsync(id, cancellationToken);

        public Task<CodeReservation> ReserveAsync(
            CodeBearingEntityType entityType, string idempotencyKey, string actorId, string correlationId,
            CancellationToken cancellationToken = default)
        {
            if (point == PreInsertCrashPoint.AfterAdmissionBeforeReservation) Interrupt();
            return inner.ReserveAsync(entityType, idempotencyKey, actorId, correlationId, cancellationToken);
        }

        public async Task<ReservationOperationResult> ConsumeForIdentityAsync(
            Guid reservationId, CodeBearingEntityType expectedEntityType, Guid identityId, int expectedVersion,
            string idempotencyKey, string actorId, string correlationId, CancellationToken cancellationToken = default)
        {
            var result = await inner.ConsumeForIdentityAsync(reservationId, expectedEntityType, identityId,
                expectedVersion, idempotencyKey, actorId, correlationId, cancellationToken);
            Assert.True(result.Succeeded, result.ErrorCode);
            Assert.Equal(CodeReservationState.Consumed, result.Reservation!.ReservationState);
            Assert.True(result.Reservation.ConsumedEntityId.HasValue);
            if (point == PreInsertCrashPoint.AfterConsumptionBeforeInsert) Interrupt();
            return result;
        }

        public Task<ReservationOperationResult> ConfirmIdentityBindingAsync(
            Guid reservationId, Guid identityId, int expectedVersion, string idempotencyKey,
            string actorId, string correlationId, CancellationToken cancellationToken = default)
            => inner.ConfirmIdentityBindingAsync(reservationId, identityId, expectedVersion,
                idempotencyKey, actorId, correlationId, cancellationToken);

        private void Interrupt()
        {
            InterruptionCount++;
            throw new InjectedPreInsertCrashException(point);
        }
    }

    private sealed class MongoScope : IAsyncDisposable
    {
        private readonly ITestOutputHelper _output;
        private static readonly string[] OwnedCollections =
        [
            "mdm_global_products", "mdm_product_definition_revisions", "mdm_gskus",
            "mdm_finished_goods", "mdm_finished_good_creation_attempts", "mdm_code_reservations",
            "mdm_canonical_code_counters"
        ];

        private MongoScope(IMongoDatabase database, ITestOutputHelper output)
        {
            Database = database;
            _output = output;
        }

        public Guid TenantA { get; } = Guid.NewGuid();
        public Guid TenantB { get; } = Guid.NewGuid();
        public IMongoDatabase Database { get; }
        public IMongoCollection<FinishedGood> FinishedGoods => Database.GetCollection<FinishedGood>("mdm_finished_goods");
        public IMongoCollection<FinishedGoodCreationAttempt> Attempts =>
            Database.GetCollection<FinishedGoodCreationAttempt>("mdm_finished_good_creation_attempts");
        public IMongoCollection<CodeReservation> Reservations => Database.GetCollection<CodeReservation>("mdm_code_reservations");

        public static async Task<MongoScope> CreateAsync(string replicaConnectionString, ITestOutputHelper output)
        {
            var settings = MongoClientSettings.FromConnectionString(replicaConnectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
#pragma warning disable CS0618
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var database = client.GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
            var hello = await database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
            Assert.True(hello.Contains("setName") && hello["isWritablePrimary"].AsBoolean);
            Assert.All(settings.Servers, server => Assert.NotEqual(27017, server.Port));
            output.WriteLine("Test-owned Mongo: replica set={0}; primary={1}; port={2}; database={3}",
                hello["setName"], hello["isWritablePrimary"], settings.Server.Port, database.DatabaseNamespace.DatabaseName);
            return new(database, output);
        }

        public TenantContext Context(Guid tenantId)
        {
            var context = new TenantContext();
            context.SetTenant(tenantId);
            return context;
        }

        public (FinishedGoodRepository FinishedGoods, GskuRepository Gskus) Repositories(Guid tenantId)
            => (new(Database, Context(tenantId)), new(Database, Context(tenantId)));

        public async Task<Gsku> InsertGskuAsync(
            Guid tenantId,
            ProductIdentityLifecycleStatus status,
            string? code = null,
            bool isDeleted = false)
        {
            _ = new GskuRepository(Database, Context(tenantId));
            var product = new GlobalProduct
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CanonicalCode = "GP-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                GlobalProductName = "Finished Good Parent",
                GlobalProductNameNormalized = "FINISHED GOOD PARENT " + Guid.NewGuid().ToString("N"),
                CodeReservationId = Guid.NewGuid(),
                LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
                IsDeleted = false
            };
            var revision = new ProductDefinitionRevision
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                GlobalProductId = product.Id,
                RevisionIdentifier = "REV-001",
                CreationCommandId = "REV:" + Guid.NewGuid().ToString("N"),
                LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
                IsDeleted = false
            };
            await Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(product);
            await Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
                .InsertOneAsync(revision);
            var gsku = new Gsku
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductDefinitionRevisionId = revision.Id,
                CanonicalCode = code ?? "GS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                CodeReservationId = Guid.NewGuid(),
                CreationCommandId = Guid.NewGuid().ToString("N"),
                PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES",
                PackQuantity = 1m,
                PackUomCode = "C62",
                LifecycleStatus = status,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? DateTimeOffset.UtcNow : null
            };
            await Database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
            return gsku;
        }

        public Task<Diten.Shared.Core.Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>> Create(
            Guid tenantId,
            Guid gskuId,
            string idempotencyKey,
            ICodeReservationRepository? interruptedReservations = null,
            IFinishedGoodRepository? finishedGoods = null)
        {
            var context = Context(tenantId);
            // Preparation is an existing scope test double: these tests prove repository tenant isolation,
            // not Enforced authorization, service-token transport or live acceptance.
            var access = ProductLegalEntityScopeTestFixture.Preparation(context);
            var handler = new CreateFinishedGoodDraftHandler(
                interruptedReservations ?? new CodeReservationRepository(Database, context),
                finishedGoods ?? new FinishedGoodRepository(Database, context),
                new GskuRepository(Database, context),
                new ProductDefinitionRevisionRepository(Database, context),
                new GlobalProductRepository(Database, context),
                context,
                new ActorContext(),
                access.Rollouts,
                access.Policies,
                access.Candidates);
            return handler.Handle(new CreateFinishedGoodDraftCommand(new()
            {
                GskuId = gskuId,
                IdempotencyKey = idempotencyKey
            }), CancellationToken.None);
        }

        public async Task<PersistedState> ReadStateAsync(Guid tenantId, string stage)
        {
            Assert.True(tenantId == TenantA || tenantId == TenantB);
            var parents = await Database.GetCollection<Gsku>("mdm_gskus")
                .Find(item => item.TenantId == tenantId).SortBy(item => item.Id).ToListAsync();
            var reservations = await Reservations.Find(item => item.TenantId == tenantId)
                .SortBy(item => item.Id).ToListAsync();
            var finishedGoods = await FinishedGoods.Find(item => item.TenantId == tenantId)
                .SortBy(item => item.Id).ToListAsync();
            var raw = new List<string>();
            foreach (var name in OwnedCollections)
            {
                var documents = await Database.GetCollection<BsonDocument>(name)
                    .Find(new BsonDocument("TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard)))
                    .Sort(new BsonDocument("_id", 1)).ToListAsync();
                raw.Add(name + ":" + new BsonArray(documents).ToJson());
            }
            foreach (var aggregate in parents.Select(item => (item.Id, Audit: (IAuditIntentAggregate)item))
                         .Concat(reservations.Select(item => (item.Id, Audit: (IAuditIntentAggregate)item)))
                         .Concat(finishedGoods.Select(item => (item.Id, Audit: (IAuditIntentAggregate)item))))
            {
                foreach (var intent in aggregate.Audit.AuditIntents)
                {
                    Assert.Equal(tenantId, intent.TenantId);
                    Assert.Equal(aggregate.Id, intent.AggregateId);
                }
            }
            _output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                Stage = stage,
                Tenant = tenantId,
                Parents = parents.Select(item => new
                {
                    item.Id,
                    Admissions = item.ChildCreationAdmissions.Select(admission => new
                    {
                        admission.ChildKind, admission.CreationCommandId, admission.RequestFingerprint
                    }),
                    Audit = item.AuditIntents.Select(intent => new { intent.IntentId, intent.AggregateId, intent.Operation })
                }),
                Reservations = reservations.Select(item => new
                {
                    item.Id, item.ReservedCode, item.ConsumedEntityId, item.ReservationState, item.BindingState,
                    Audit = item.AuditIntents.Select(intent => new { intent.IntentId, intent.AggregateId, intent.Operation, intent.CommandId })
                }),
                FinishedGoods = finishedGoods.Select(item => new
                {
                    item.Id, item.GskuId, item.CodeReservationId, item.CanonicalCode,
                    Audit = item.AuditIntents.Select(intent => new { intent.IntentId, intent.AggregateId, intent.Operation, intent.CommandId })
                })
            }));
            return new(parents.ToArray(), reservations.ToArray(), finishedGoods.ToArray(), string.Join("\n", raw));
        }

        public async ValueTask DisposeAsync()
        {
            var ownedTenants = Builders<BsonDocument>.Filter.In("TenantId",
                new[] { TenantA, TenantB }.Select(tenant => new BsonBinaryData(tenant, GuidRepresentation.Standard)));
            foreach (var name in OwnedCollections)
            {
                var collection = Database.GetCollection<BsonDocument>(name);
                await collection.DeleteManyAsync(ownedTenants);
                Assert.Equal(0, await collection.CountDocumentsAsync(ownedTenants));
            }
            _output.WriteLine("Tenant-owned cleanup verified: 0 documents remain in each of {0} collections; database retained.",
                OwnedCollections.Length);
        }
    }

    private sealed class ActorContext : IProductIdentityActorContext
    {
        public string ActorId => "finished-good-test-actor";
    }
}

internal static class FinishedGoodDraftDtoTestExtensions
{
    public static string GskuDisplay(this ProductItemSkuMasterModels.FinishedGoodDraftDto dto)
        => dto.GskuCanonicalCode;
}
