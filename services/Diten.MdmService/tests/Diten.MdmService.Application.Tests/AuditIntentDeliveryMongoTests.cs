using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class AuditIntentDeliveryMongoTests
{
    private static long _codeSequence;

    [Fact]
    public async Task Pending_discovery_is_tenant_isolated_and_exposes_only_work_item_metadata()
    {
        await using var scope = await MongoScope.CreateAsync();
        var tenantAReservation = CreateReservation(scope.TenantA);
        var tenantBReservation = CreateReservation(scope.TenantB);
        await scope.Reservations.InsertManyAsync([tenantAReservation, tenantBReservation]);

        var items = await scope.Delivery(scope.TenantA).DiscoverEligibleAsync(10);

        var item = Assert.Single(items);
        Assert.Equal(scope.TenantA, item.Locator.TenantId);
        Assert.Equal(tenantAReservation.Id, item.Locator.AggregateId);
        Assert.DoesNotContain(
            item.GetType().GetProperties(),
            property => property.Name.Contains("Payload", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("aggregate-id")]
    [InlineData("aggregate-type")]
    [InlineData("source-service")]
    public async Task Parent_intent_identity_mismatch_is_excluded_and_never_mutated(string mismatch)
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA, version: 31);
        var intent = Assert.Single(reservation.AuditIntents);
        switch (mismatch)
        {
            case "tenant":
                intent.TenantId = scope.TenantB;
                break;
            case "aggregate-id":
                intent.AggregateId = Guid.NewGuid();
                break;
            case "aggregate-type":
                intent.AggregateType = AuditAggregateType.GlobalProduct;
                break;
            case "source-service":
                intent.SourceService = "untrusted-source";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mismatch));
        }

        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);
        var locator = Locator(reservation);

        Assert.Empty(await repository.DiscoverEligibleAsync(10));
        Assert.Null(await repository.TryClaimAsync(
            locator, 0, "worker-a", TimeSpan.FromMinutes(5)));
        var forgedClaim = new AuditIntentClaim(
            locator, "opaque", "worker-a", 1, now, now.AddMinutes(5), 1);
        Assert.False(await repository.MarkRetryableFailureAsync(
            forgedClaim, TimeSpan.FromMinutes(1), "retry"));
        Assert.False(await repository.MarkDeadLetterAsync(forgedClaim, "terminal"));
        Assert.False(await repository.MarkDeliveredAsync(
            forgedClaim, Acknowledgement(forgedClaim, now)));
        Assert.False(await repository.CompactDeliveredAsync(forgedClaim, "mismatch-receipt"));

        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        var storedIntent = Assert.Single(stored.AuditIntents);
        Assert.Equal(AuditIntentDeliveryState.Pending, storedIntent.DeliveryState);
        Assert.Equal(0, storedIntent.AttemptCount);
        Assert.Equal(0, storedIntent.ClaimGeneration);
        Assert.Null(storedIntent.ClaimToken);
        Assert.Empty(stored.AuditIntentReceipts);
        Assert.Equal(31, stored.Version);
    }

    [Fact]
    public async Task Soft_deleted_reservation_and_product_complete_internal_delivery_without_business_disclosure()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA, version: 17);
        var product = CreateGlobalProduct(scope.TenantA);
        await scope.Reservations.InsertOneAsync(reservation);
        await scope.GlobalProducts.InsertOneAsync(product);
        await SoftDeleteAsync(scope.Reservations, reservation.Id);
        await SoftDeleteAsync(scope.GlobalProducts, product.Id);

        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);
        var repository = scope.Delivery(scope.TenantA);
        var items = await repository.DiscoverEligibleAsync(10);

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.True(item.AggregateIsDeleted));
        Assert.All(items, item => Assert.DoesNotContain(
            item.GetType().GetProperties(),
            property => property.Name.Contains("Payload", StringComparison.OrdinalIgnoreCase)
                        || property.PropertyType == typeof(CodeReservation)
                        || property.PropertyType == typeof(GlobalProduct)));
        Assert.Contains(items, item => item.Locator.AggregateType == AuditAggregateType.CodeReservation);
        Assert.Contains(items, item => item.Locator.AggregateType == AuditAggregateType.GlobalProduct);
        Assert.Null(await scope.ReservationBusiness(scope.TenantA).GetByIdAsync(reservation.Id));
        Assert.Null(await scope.ProductBusiness(scope.TenantA).GetByIdAsync(product.Id));
        Assert.Empty(await scope.Delivery(scope.TenantB).DiscoverEligibleAsync(10));

        foreach (var item in items)
        {
            Assert.Null(await scope.Delivery(scope.TenantB).TryClaimAsync(
                item.Locator, item.ClaimGeneration, "foreign-worker", TimeSpan.FromMinutes(5)));

            var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
                item.Locator, item.ClaimGeneration, "worker-a", TimeSpan.FromMinutes(5)));
            var staleClaim = claim with { ClaimGeneration = claim.ClaimGeneration - 1 };
            scope.Clock.Advance(TimeSpan.FromMinutes(1));
            Assert.False(await repository.MarkDeliveredAsync(
                staleClaim, Acknowledgement(staleClaim, scope.Clock.GetUtcNow())));
            Assert.True(await repository.MarkDeliveredAsync(
                claim, Acknowledgement(claim, scope.Clock.GetUtcNow())));
            scope.Clock.Advance(TimeSpan.FromMinutes(1));
            Assert.True(await repository.CompactDeliveredAsync(
                claim, $"soft-deleted-{item.Locator.AggregateType}"));
        }

        var storedReservation = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.True(storedReservation.IsDeleted);
        Assert.Equal(17, storedReservation.Version);
        Assert.Empty(storedReservation.AuditIntents);
        Assert.Single(storedReservation.AuditIntentReceipts);

        var storedProduct = await scope.GlobalProducts.Find(item => item.Id == product.Id).SingleAsync();
        Assert.True(storedProduct.IsDeleted);
        Assert.Equal(9, storedProduct.Version);
        Assert.Empty(storedProduct.AuditIntents);
        Assert.Single(storedProduct.AuditIntentReceipts);

        Assert.Empty(await repository.DiscoverEligibleAsync(10));
        Assert.Null(await scope.ReservationBusiness(scope.TenantA).GetByIdAsync(reservation.Id));
        Assert.Null(await scope.ProductBusiness(scope.TenantA).GetByIdAsync(product.Id));
    }

    [Fact]
    public async Task Concurrent_claims_for_same_intent_have_exactly_one_winner()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var locator = Locator(reservation);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);

        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            repository.TryClaimAsync(locator, 0, $"worker-{index}", TimeSpan.FromMinutes(5))));

        var claim = Assert.Single(claims, candidate => candidate is not null);
        Assert.NotNull(claim);
        Assert.Equal(1, claim!.ClaimGeneration);
        Assert.Equal(1, claim.AttemptCount);
    }

    [Fact]
    public async Task Lease_blocks_early_claim_and_expiry_allows_new_generation()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var locator = Locator(reservation);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);

        var first = await repository.TryClaimAsync(locator, 0, "worker-a", TimeSpan.FromMinutes(5));
        scope.Clock.Advance(TimeSpan.FromMinutes(4));
        var early = await repository.TryClaimAsync(locator, 1, "worker-b", TimeSpan.FromMinutes(5));
        scope.Clock.Advance(TimeSpan.FromMinutes(2));
        var reclaimed = await repository.TryClaimAsync(locator, 1, "worker-b", TimeSpan.FromMinutes(5));

        Assert.NotNull(first);
        Assert.Null(early);
        Assert.NotNull(reclaimed);
        Assert.Equal(2, reclaimed!.ClaimGeneration);
        Assert.Equal(2, reclaimed.AttemptCount);
        Assert.NotEqual(first!.ClaimToken, reclaimed.ClaimToken);
    }

    [Fact]
    public async Task Old_claim_cannot_complete_fail_or_dead_letter_after_reclaim()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var locator = Locator(reservation);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);
        var oldClaim = (await repository.TryClaimAsync(locator, 0, "worker-a", TimeSpan.FromMinutes(1)))!;
        var newNow = now.AddMinutes(2);
        scope.Clock.SetUtcNow(newNow);
        var newClaim = (await repository.TryClaimAsync(locator, 1, "worker-b", TimeSpan.FromMinutes(5)))!;

        Assert.False(await repository.MarkRetryableFailureAsync(
            oldClaim, TimeSpan.FromMinutes(1), "old retry"));
        Assert.False(await repository.MarkDeadLetterAsync(oldClaim, "old terminal"));
        Assert.False(await repository.MarkDeliveredAsync(oldClaim, Acknowledgement(oldClaim, newNow)));

        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        var intent = Assert.Single(stored.AuditIntents);
        Assert.Equal(newClaim.ClaimToken, intent.ClaimToken);
        Assert.Equal(AuditIntentDeliveryState.Processing, intent.DeliveryState);
    }

    [Fact]
    public async Task Retryable_failure_schedules_retry_and_terminal_failure_dead_letters()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var locator = Locator(reservation);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);
        var first = (await repository.TryClaimAsync(locator, 0, "worker-a", TimeSpan.FromMinutes(5)))!;
        var nextRetry = now.AddMinutes(10);

        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.MarkRetryableFailureAsync(first, TimeSpan.FromMinutes(9), "timeout"));
        scope.Clock.Advance(TimeSpan.FromMinutes(8));
        Assert.Empty(await repository.DiscoverEligibleAsync(10));
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Single(await repository.DiscoverEligibleAsync(10));

        var second = (await repository.TryClaimAsync(locator, 1, "worker-b", TimeSpan.FromMinutes(5)))!;
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.MarkDeadLetterAsync(second, "contract rejected"));
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Empty(await repository.DiscoverEligibleAsync(10));

        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        var intent = Assert.Single(stored.AuditIntents);
        Assert.Equal(AuditIntentDeliveryState.DeadLetter, intent.DeliveryState);
        Assert.Equal(AuditIntentFailureClass.Terminal, intent.FailureClass);
        Assert.NotNull(intent.DeadLetteredAt);
        Assert.Null(intent.ClaimToken);
        Assert.Null(intent.LeaseOwner);
        Assert.Null(intent.LeaseUntil);
        Assert.Equal(second.ClaimGeneration, intent.ClaimGeneration);
        Assert.Null(await repository.TryClaimAsync(
            locator, second.ClaimGeneration, "worker-c", TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task Dead_letter_clears_claim_capability_but_preserves_fencing_generation()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA, version: 23);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var locator = Locator(reservation);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            locator, 0, "worker-a", TimeSpan.FromMinutes(5)));

        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.MarkDeadLetterAsync(claim, "terminal contract rejection"));

        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        var intent = Assert.Single(stored.AuditIntents);
        Assert.Equal(AuditIntentDeliveryState.DeadLetter, intent.DeliveryState);
        Assert.Null(intent.ClaimToken);
        Assert.Null(intent.LeaseOwner);
        Assert.Null(intent.LeaseUntil);
        Assert.Equal(claim.ClaimGeneration, intent.ClaimGeneration);
        Assert.Equal(23, stored.Version);
        Assert.Empty(await repository.DiscoverEligibleAsync(10));
        Assert.Null(await repository.TryClaimAsync(
            locator, claim.ClaimGeneration, "worker-b", TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task Compact_is_rejected_without_acknowledged_delivery()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        scope.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        var claim = (await repository.TryClaimAsync(
            Locator(reservation), 0, "worker-a", TimeSpan.FromMinutes(5)))!;

        Assert.False(await repository.CompactDeliveredAsync(
            claim, "receipt-before-ack"));

        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Single(stored.AuditIntents);
        Assert.Empty(stored.AuditIntentReceipts);
    }

    [Fact]
    public async Task Invalid_central_idempotency_contract_cannot_mark_intent_delivered()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);
        var claim = (await repository.TryClaimAsync(
            Locator(reservation), 0, "worker-a", TimeSpan.FromMinutes(5)))!;
        var invalidAcknowledgement = new AuditIntentAcknowledgement(
            "durable-outbox-accepted",
            "caller-controlled-key",
            "owner-approved-contract-test-v1",
            now.AddMinutes(1));

        await Assert.ThrowsAsync<ArgumentException>(() => repository.MarkDeliveredAsync(
            claim, invalidAcknowledgement));

        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Equal(AuditIntentDeliveryState.Processing, Assert.Single(stored.AuditIntents).DeliveryState);
        Assert.Null(stored.AuditIntents[0].CentralAcknowledgement);
    }

    public static TheoryData<AuditAggregateType> AtomicAcknowledgementAggregateTypes => new()
    {
        AuditAggregateType.CodeReservation,
        AuditAggregateType.GlobalProduct,
        AuditAggregateType.ProductDefinitionRevision,
        AuditAggregateType.Gsku,
        AuditAggregateType.FinishedGood,
        AuditAggregateType.Lsku,
        AuditAggregateType.ProductLegalEntityScopePolicy,
        AuditAggregateType.ProductLegalEntityScopeRolloutState
    };

    [Theory]
    [MemberData(nameof(AtomicAcknowledgementAggregateTypes))]
    public async Task Atomic_acknowledgement_compacts_once_and_exact_replay_survives_worker_restart(
        AuditAggregateType aggregateType)
    {
        await using var scope = await MongoScope.CreateAsync();
        var aggregateId = await scope.InsertAuditAggregateAsync(aggregateType, version: 37);
        var repository = scope.Delivery(scope.TenantA);
        var item = Assert.Single(
            await repository.DiscoverEligibleAsync(10),
            candidate => candidate.Locator.AggregateType == aggregateType);
        Assert.Equal(aggregateId, item.Locator.AggregateId);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            item.Locator,
            item.ClaimGeneration,
            "atomic-worker",
            TimeSpan.FromMinutes(5)));
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        var acknowledgement = Acknowledgement(claim, scope.Clock.GetUtcNow());

        Assert.True(await repository.AcknowledgeAndCompactAsync(
            claim,
            acknowledgement,
            "atomic-receipt"));

        var restartedRepository = scope.Delivery(scope.TenantA);
        Assert.Empty(await restartedRepository.DiscoverEligibleAsync(10));
        Assert.True(await restartedRepository.AcknowledgeAndCompactAsync(
            claim,
            acknowledgement,
            "atomic-receipt"));
        Assert.False(await restartedRepository.AcknowledgeAndCompactAsync(
            claim,
            acknowledgement,
            "different-receipt"));
        Assert.False(await restartedRepository.AcknowledgeAndCompactAsync(
            claim,
            acknowledgement with { CentralAcknowledgement = "different-acknowledgement" },
            "atomic-receipt"));
        Assert.False(await restartedRepository.AcknowledgeAndCompactAsync(
            claim,
            acknowledgement with { AcceptedAt = acknowledgement.AcceptedAt.AddTicks(1) },
            "atomic-receipt"));
        const string otherContractVersion = "owner-approved-contract-test-v2";
        Assert.False(await restartedRepository.AcknowledgeAndCompactAsync(
            claim,
            acknowledgement with
            {
                ContractVersion = otherContractVersion,
                CentralIdempotencyKey = AuditIntentContract.BuildCentralIdempotencyKey(
                    claim.Locator.TenantId,
                    claim.Locator.IntentId,
                    otherContractVersion)
            },
            "atomic-receipt"));

        var stored = await scope.ReadAuditAggregateAsync(aggregateType, aggregateId);
        Assert.Equal(37, stored.Version);
        Assert.Empty(stored.Intents);
        var receipt = Assert.Single(stored.Receipts);
        Assert.Equal(item.Locator.IntentId, receipt.IntentId);
        Assert.Equal(acknowledgement.CentralAcknowledgement, receipt.CentralAcknowledgement);
        Assert.Equal(acknowledgement.CentralIdempotencyKey, receipt.CentralIdempotencyKey);
        Assert.Equal(acknowledgement.ContractVersion, receipt.ContractVersion);
        Assert.Equal(acknowledgement.AcceptedAt, receipt.AcknowledgedAt);
        Assert.Equal("atomic-receipt", receipt.CompactReceiptReference);
    }

    [Fact]
    public async Task Atomic_acknowledgement_rejects_stale_expired_and_cross_tenant_claims_without_partial_mutation()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA, version: 29);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            Locator(reservation),
            0,
            "atomic-worker",
            TimeSpan.FromMinutes(1)));
        var acknowledgement = Acknowledgement(claim, scope.Clock.GetUtcNow());
        scope.Clock.Advance(TimeSpan.FromMinutes(2));

        Assert.False(await repository.AcknowledgeAndCompactAsync(
            claim,
            acknowledgement,
            "expired-receipt"));
        Assert.False(await scope.Delivery(scope.TenantB).AcknowledgeAndCompactAsync(
            claim,
            acknowledgement,
            "cross-tenant-receipt"));

        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Equal(29, stored.Version);
        Assert.Single(stored.AuditIntents);
        Assert.Empty(stored.AuditIntentReceipts);
    }

    [Fact]
    public async Task Concurrent_atomic_acknowledgement_calls_converge_on_one_exact_receipt()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA, version: 31);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            Locator(reservation),
            0,
            "atomic-worker",
            TimeSpan.FromMinutes(5)));
        var acknowledgement = Acknowledgement(claim, scope.Clock.GetUtcNow());

        var results = await Task.WhenAll(
            repository.AcknowledgeAndCompactAsync(claim, acknowledgement, "concurrent-receipt"),
            repository.AcknowledgeAndCompactAsync(claim, acknowledgement, "concurrent-receipt"));

        Assert.All(results, Assert.True);
        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Equal(31, stored.Version);
        Assert.Empty(stored.AuditIntents);
        Assert.Single(stored.AuditIntentReceipts);
    }

    [Fact]
    public async Task Duplicate_claimed_intent_identity_fails_closed_without_partial_compaction()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA, version: 43);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            Locator(reservation),
            0,
            "atomic-worker",
            TimeSpan.FromMinutes(5)));
        var claimed = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        var duplicate = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<LocalAuditIntent>(
            Assert.Single(claimed.AuditIntents).ToBsonDocument());
        duplicate.ClaimToken = $"{duplicate.ClaimToken}-duplicate";
        await scope.Reservations.UpdateOneAsync(
            item => item.Id == reservation.Id,
            Builders<CodeReservation>.Update.Push(item => item.AuditIntents, duplicate));

        Assert.False(await repository.AcknowledgeAndCompactAsync(
            claim,
            Acknowledgement(claim, scope.Clock.GetUtcNow()),
            "duplicate-receipt"));

        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Equal(43, stored.Version);
        Assert.Equal(2, stored.AuditIntents.Count);
        Assert.Empty(stored.AuditIntentReceipts);
    }

    [Fact]
    public async Task Stale_token_generation_and_conflicting_receipt_fail_closed_without_mutation()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA, version: 47);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            Locator(reservation),
            0,
            "atomic-worker",
            TimeSpan.FromMinutes(5)));
        var acknowledgement = Acknowledgement(claim, scope.Clock.GetUtcNow());
        var staleToken = claim with { ClaimToken = $"{claim.ClaimToken}-stale" };
        var staleGeneration = claim with { ClaimGeneration = claim.ClaimGeneration + 1 };

        Assert.False(await repository.AcknowledgeAndCompactAsync(
            staleToken,
            acknowledgement,
            "stale-token-receipt"));
        Assert.False(await repository.AcknowledgeAndCompactAsync(
            staleGeneration,
            acknowledgement,
            "stale-generation-receipt"));

        var conflictingReceipt = new LocalAuditIntentReceipt
        {
            SourceService = AuditIntentContract.SourceService,
            IntentId = claim.Locator.IntentId,
            TenantId = claim.Locator.TenantId,
            IdempotencyKey = reservation.AuditIntents[0].IdempotencyKey,
            CentralAcknowledgement = "conflicting-acknowledgement",
            CentralIdempotencyKey = acknowledgement.CentralIdempotencyKey,
            ContractVersion = acknowledgement.ContractVersion,
            AcknowledgedAt = acknowledgement.AcceptedAt,
            DeliveredAt = scope.Clock.GetUtcNow(),
            CompactedAt = scope.Clock.GetUtcNow(),
            CompactReceiptReference = "conflicting-receipt",
            EvidenceHash = reservation.AuditIntents[0].EvidenceHash
        };
        await scope.Reservations.UpdateOneAsync(
            item => item.Id == reservation.Id,
            Builders<CodeReservation>.Update.Push(item => item.AuditIntentReceipts, conflictingReceipt));

        Assert.False(await repository.AcknowledgeAndCompactAsync(
            claim,
            acknowledgement,
            "expected-receipt"));
        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Equal(47, stored.Version);
        Assert.Single(stored.AuditIntents);
        Assert.Single(stored.AuditIntentReceipts);
    }

    [Fact]
    public async Task Reclaim_before_atomic_CAS_fences_old_claim_and_allows_only_new_generation()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA, version: 49);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var oldClaim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            Locator(reservation),
            0,
            "worker-old",
            TimeSpan.FromMinutes(1)));
        scope.Clock.Advance(TimeSpan.FromMinutes(2));
        var staleItem = Assert.Single(await repository.DiscoverEligibleAsync(10));
        var newClaim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            staleItem.Locator,
            staleItem.ClaimGeneration,
            "worker-new",
            TimeSpan.FromMinutes(5)));

        Assert.False(await repository.AcknowledgeAndCompactAsync(
            oldClaim,
            Acknowledgement(oldClaim, scope.Clock.GetUtcNow()),
            "old-receipt"));
        Assert.True(await repository.AcknowledgeAndCompactAsync(
            newClaim,
            Acknowledgement(newClaim, scope.Clock.GetUtcNow()),
            "new-receipt"));

        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Equal(49, stored.Version);
        Assert.Empty(stored.AuditIntents);
        Assert.Equal("new-receipt", Assert.Single(stored.AuditIntentReceipts).CompactReceiptReference);
    }

    [Fact]
    public async Task Atomic_compaction_accepts_exact_budget_document_when_result_fits()
    {
        await using var scope = await MongoScope.CreateAsync();
        var aggregateId = await scope.InsertAuditAggregateAsync(
            AuditAggregateType.ProductLegalEntityScopePolicy,
            version: 53);
        var repository = scope.Delivery(scope.TenantA);
        var item = Assert.Single(await repository.DiscoverEligibleAsync(10));
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            item.Locator,
            item.ClaimGeneration,
            "atomic-worker",
            TimeSpan.FromMinutes(5)));
        await scope.ProductScopePolicies.UpdateOneAsync(
            policy => policy.Id == aggregateId,
            Builders<ProductLegalEntityScopePolicy>.Update.Set("AuditIntents.$[intent].EvidenceHash", string.Empty),
            new UpdateOptions
            {
                ArrayFilters =
                [
                    new BsonDocumentArrayFilterDefinition<BsonDocument>(
                        new BsonDocument("intent.IntentId", new BsonBinaryData(
                            item.Locator.IntentId,
                            GuidRepresentation.Standard)))
                ]
            });
        var baseline = await scope.ProductScopePolicies.Find(policy => policy.Id == aggregateId).SingleAsync();
        var padding = ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes
            - baseline.ToBsonDocument().ToBson().Length;
        Assert.True(padding > 0);
        await scope.ProductScopePolicies.UpdateOneAsync(
            policy => policy.Id == aggregateId,
            Builders<ProductLegalEntityScopePolicy>.Update.Set(
                "AuditIntents.$[intent].EvidenceHash",
                new string('x', padding)),
            new UpdateOptions
            {
                ArrayFilters =
                [
                    new BsonDocumentArrayFilterDefinition<BsonDocument>(
                        new BsonDocument("intent.IntentId", new BsonBinaryData(
                            item.Locator.IntentId,
                            GuidRepresentation.Standard)))
                ]
            });
        var exact = await scope.ProductScopePolicies.Find(policy => policy.Id == aggregateId).SingleAsync();
        Assert.Equal(
            ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes,
            exact.ToBsonDocument().ToBson().Length);

        Assert.True(await repository.AcknowledgeAndCompactAsync(
            claim,
            Acknowledgement(claim, scope.Clock.GetUtcNow()),
            "exact-budget-receipt"));
        var stored = await scope.ReadAuditAggregateAsync(
            AuditAggregateType.ProductLegalEntityScopePolicy,
            aggregateId);
        Assert.Equal(53, stored.Version);
        Assert.Empty(stored.Intents);
        Assert.Single(stored.Receipts);
    }

    [Fact]
    public async Task Atomic_compaction_rejects_over_budget_candidate_without_partial_mutation()
    {
        await using var scope = await MongoScope.CreateAsync();
        var aggregateId = await scope.InsertAuditAggregateAsync(
            AuditAggregateType.ProductLegalEntityScopePolicy,
            version: 59);
        var repository = scope.Delivery(scope.TenantA);
        var item = Assert.Single(await repository.DiscoverEligibleAsync(10));
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            item.Locator,
            item.ClaimGeneration,
            "atomic-worker",
            TimeSpan.FromMinutes(5)));
        var unrelated = CreateIntent(
            scope.TenantA,
            AuditAggregateType.ProductLegalEntityScopePolicy,
            aggregateId);
        unrelated.EvidenceHash = new string('x', ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes);
        await scope.ProductScopePolicies.UpdateOneAsync(
            policy => policy.Id == aggregateId,
            Builders<ProductLegalEntityScopePolicy>.Update.Push(policy => policy.AuditIntents, unrelated));

        Assert.False(await repository.AcknowledgeAndCompactAsync(
            claim,
            Acknowledgement(claim, scope.Clock.GetUtcNow()),
            "over-budget-receipt"));
        var stored = await scope.ReadAuditAggregateAsync(
            AuditAggregateType.ProductLegalEntityScopePolicy,
            aggregateId);
        Assert.Equal(59, stored.Version);
        Assert.Equal(2, stored.Intents.Count);
        Assert.Empty(stored.Receipts);
    }

    [Fact]
    public async Task Acknowledged_delivery_compacts_to_receipt_without_changing_business_version()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA, version: 17);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);
        var claim = (await repository.TryClaimAsync(
            Locator(reservation), 0, "worker-a", TimeSpan.FromMinutes(5)))!;
        var acknowledgement = Acknowledgement(claim, now.AddMinutes(1));
        Assert.Equal(now, claim.ClaimedAt);
        Assert.Equal(now.AddMinutes(5), claim.LeaseUntil);

        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.MarkDeliveredAsync(claim, acknowledgement));
        var delivered = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Equal(scope.Clock.GetUtcNow(), Assert.Single(delivered.AuditIntents).DeliveredAt);
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.CompactDeliveredAsync(claim, "receipt-001"));

        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Equal(17, stored.Version);
        Assert.Empty(stored.AuditIntents);
        var receipt = Assert.Single(stored.AuditIntentReceipts);
        Assert.Equal(reservation.AuditIntents[0].IntentId, receipt.IntentId);
        Assert.Equal(reservation.AuditIntents[0].IdempotencyKey, receipt.IdempotencyKey);
        Assert.Equal(acknowledgement.CentralAcknowledgement, receipt.CentralAcknowledgement);
        Assert.Equal(acknowledgement.CentralIdempotencyKey, receipt.CentralIdempotencyKey);
        Assert.Equal(acknowledgement.ContractVersion, receipt.ContractVersion);
        Assert.Equal("receipt-001", receipt.CompactReceiptReference);
        Assert.Equal(scope.Clock.GetUtcNow(), receipt.CompactedAt);

        Assert.True(await repository.CompactDeliveredAsync(claim, "receipt-001"));
        Assert.False(await repository.CompactDeliveredAsync(claim, "different-receipt"));
        stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Empty(stored.AuditIntents);
        Assert.Single(stored.AuditIntentReceipts);
    }

    [Fact]
    public async Task Conflicting_receipt_and_delivered_intent_cannot_be_compacted_or_duplicated()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA, version: 41);
        await scope.Reservations.InsertOneAsync(reservation);
        var repository = scope.Delivery(scope.TenantA);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            Locator(reservation), 0, "worker-a", TimeSpan.FromMinutes(5)));
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        var acknowledgement = Acknowledgement(claim, scope.Clock.GetUtcNow());
        Assert.True(await repository.MarkDeliveredAsync(claim, acknowledgement));

        var conflictingReceipt = new LocalAuditIntentReceipt
        {
            SourceService = AuditIntentContract.SourceService,
            IntentId = claim.Locator.IntentId,
            TenantId = claim.Locator.TenantId,
            IdempotencyKey = reservation.AuditIntents[0].IdempotencyKey,
            CentralAcknowledgement = acknowledgement.CentralAcknowledgement,
            CentralIdempotencyKey = acknowledgement.CentralIdempotencyKey,
            ContractVersion = acknowledgement.ContractVersion,
            AcknowledgedAt = acknowledgement.AcceptedAt,
            DeliveredAt = scope.Clock.GetUtcNow(),
            CompactedAt = scope.Clock.GetUtcNow(),
            CompactReceiptReference = "conflicting-receipt",
            EvidenceHash = reservation.AuditIntents[0].EvidenceHash
        };
        await scope.Reservations.UpdateOneAsync(
            item => item.Id == reservation.Id,
            Builders<CodeReservation>.Update.Push(item => item.AuditIntentReceipts, conflictingReceipt));

        Assert.False(await repository.CompactDeliveredAsync(claim, "conflicting-receipt"));

        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Single(stored.AuditIntents);
        Assert.Single(stored.AuditIntentReceipts);
        Assert.Equal(41, stored.Version);
    }

    [Fact]
    public async Task Global_product_retry_delivery_and_compaction_preserve_business_version()
    {
        await using var scope = await MongoScope.CreateAsync();
        var product = CreateGlobalProduct(scope.TenantA);
        await scope.GlobalProducts.InsertOneAsync(product);
        var repository = scope.Delivery(scope.TenantA);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);
        var locator = Locator(product);
        var first = (await repository.TryClaimAsync(
            locator, 0, "worker-a", TimeSpan.FromMinutes(5)))!;

        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.MarkRetryableFailureAsync(
            first, TimeSpan.FromMinutes(9), "timeout"));
        scope.Clock.Advance(TimeSpan.FromMinutes(9));
        var second = (await repository.TryClaimAsync(
            locator, 1, "worker-b", TimeSpan.FromMinutes(5)))!;
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.MarkDeliveredAsync(
            second, Acknowledgement(second, scope.Clock.GetUtcNow())));
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.CompactDeliveredAsync(second, "product-receipt"));

        var stored = await scope.GlobalProducts.Find(item => item.Id == product.Id).SingleAsync();
        Assert.Equal(9, stored.Version);
        Assert.Empty(stored.AuditIntents);
        Assert.Single(stored.AuditIntentReceipts);
    }

    [Fact]
    public async Task Finished_good_discovery_claim_acknowledgement_and_compaction_preserve_business_version()
    {
        await using var scope = await MongoScope.CreateAsync();
        var finishedGood = CreateFinishedGood(scope.TenantA);
        await scope.FinishedGoods.InsertOneAsync(finishedGood);
        await SoftDeleteAsync(scope.FinishedGoods, finishedGood.Id);
        var repository = scope.Delivery(scope.TenantA);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);
        var workItem = Assert.Single(
            await repository.DiscoverEligibleAsync(10),
            item => item.Locator.AggregateType == AuditAggregateType.FinishedGood);
        Assert.True(workItem.AggregateIsDeleted);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            workItem.Locator, workItem.ClaimGeneration, "finished-good-worker", TimeSpan.FromMinutes(5)));
        Assert.Null(await repository.TryClaimAsync(
            workItem.Locator, workItem.ClaimGeneration, "stale-worker", TimeSpan.FromMinutes(5)));
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.MarkDeliveredAsync(
            claim, Acknowledgement(claim, scope.Clock.GetUtcNow())));
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.CompactDeliveredAsync(claim, "finished-good-receipt"));
        Assert.True(await repository.CompactDeliveredAsync(claim, "finished-good-receipt"));

        var stored = await scope.FinishedGoods.Find(item => item.Id == finishedGood.Id).SingleAsync();
        Assert.Equal(13, stored.Version);
        Assert.Empty(stored.AuditIntents);
        Assert.Single(stored.AuditIntentReceipts);
    }

    [Fact]
    public async Task Lsku_discovery_claim_fencing_acknowledgement_and_compaction_preserve_business_version()
    {
        await using var scope = await MongoScope.CreateAsync();
        var lsku = CreateLsku(scope.TenantA);
        await scope.Lskus.InsertOneAsync(lsku);
        await SoftDeleteAsync(scope.Lskus, lsku.Id);
        var repository = scope.Delivery(scope.TenantA);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);
        var workItem = Assert.Single(
            await repository.DiscoverEligibleAsync(10),
            item => item.Locator.AggregateType == AuditAggregateType.Lsku);
        Assert.True(workItem.AggregateIsDeleted);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            workItem.Locator, workItem.ClaimGeneration, "lsku-worker", TimeSpan.FromMinutes(5)));
        Assert.Null(await repository.TryClaimAsync(
            workItem.Locator, workItem.ClaimGeneration, "stale-worker", TimeSpan.FromMinutes(5)));
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.MarkDeliveredAsync(
            claim, Acknowledgement(claim, scope.Clock.GetUtcNow())));
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.CompactDeliveredAsync(claim, "lsku-receipt"));
        Assert.True(await repository.CompactDeliveredAsync(claim, "lsku-receipt"));

        var stored = await scope.Lskus.Find(item => item.Id == lsku.Id).SingleAsync();
        Assert.Equal(23, stored.Version);
        Assert.Empty(stored.AuditIntents);
        Assert.Single(stored.AuditIntentReceipts);
    }

    [Theory]
    [InlineData(AuditAggregateType.ProductLegalEntityScopePolicy)]
    [InlineData(AuditAggregateType.ProductLegalEntityScopeRolloutState)]
    public async Task Product_scope_aggregates_support_delivery_retry_dead_letter_ack_compaction_and_fencing(
        AuditAggregateType aggregateType)
    {
        await using var scope = await MongoScope.CreateAsync();
        var aggregateId = Guid.NewGuid();
        var retryIntent = CreateIntent(scope.TenantA, aggregateType, aggregateId);
        retryIntent.TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        AuditIntentTemporalStorage.ApplyCurrentVersion(retryIntent);
        var deliveryIntent = CreateIntent(scope.TenantA, aggregateType, aggregateId);
        deliveryIntent.TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        AuditIntentTemporalStorage.ApplyCurrentVersion(deliveryIntent);
        var expectedVersion = aggregateType == AuditAggregateType.ProductLegalEntityScopePolicy ? 31 : 37;
        await scope.InsertProductScopeAggregateAsync(
            aggregateType,
            aggregateId,
            expectedVersion,
            [retryIntent, deliveryIntent]);
        var repository = scope.Delivery(scope.TenantA);
        scope.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        var items = (await repository.DiscoverEligibleAsync(10))
            .Where(item => item.Locator.AggregateType == aggregateType)
            .ToArray();

        Assert.Equal(2, items.Length);
        var retryItem = Assert.Single(items, item => item.Locator.IntentId == retryIntent.IntentId);
        var firstClaim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            retryItem.Locator,
            retryItem.ClaimGeneration,
            "product-scope-retry-worker",
            TimeSpan.FromMinutes(5)));
        Assert.Null(await repository.TryClaimAsync(
            retryItem.Locator,
            retryItem.ClaimGeneration,
            "stale-worker",
            TimeSpan.FromMinutes(5)));
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.MarkRetryableFailureAsync(
            firstClaim,
            TimeSpan.FromMinutes(3),
            "temporary-provider-failure"));
        Assert.False(await repository.MarkDeadLetterAsync(firstClaim, "stale-claim"));
        scope.Clock.Advance(TimeSpan.FromMinutes(3));
        var secondClaim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            retryItem.Locator,
            firstClaim.ClaimGeneration,
            "product-scope-dead-letter-worker",
            TimeSpan.FromMinutes(5)));
        Assert.True(await repository.MarkDeadLetterAsync(secondClaim, "terminal-contract-failure"));

        var deliveryItem = Assert.Single(items, item => item.Locator.IntentId == deliveryIntent.IntentId);
        var deliveryClaim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(
            deliveryItem.Locator,
            deliveryItem.ClaimGeneration,
            "product-scope-delivery-worker",
            TimeSpan.FromMinutes(5)));
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.MarkDeliveredAsync(
            deliveryClaim,
            Acknowledgement(deliveryClaim, scope.Clock.GetUtcNow())));
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await repository.CompactDeliveredAsync(deliveryClaim, "product-scope-receipt"));
        Assert.True(await repository.CompactDeliveredAsync(deliveryClaim, "product-scope-receipt"));

        var stored = await scope.ReadProductScopeAggregateAsync(aggregateType, aggregateId);
        Assert.Equal(expectedVersion, stored.Version);
        var deadLetter = Assert.Single(stored.Intents);
        Assert.Equal(retryIntent.IntentId, deadLetter.IntentId);
        Assert.Equal(AuditIntentDeliveryState.DeadLetter, deadLetter.DeliveryState);
        Assert.Single(stored.Receipts);
    }

    [Theory]
    [InlineData(AuditAggregateType.ProductLegalEntityScopePolicy)]
    [InlineData(AuditAggregateType.ProductLegalEntityScopeRolloutState)]
    public async Task Product_scope_audit_discovery_propagates_cancellation(AuditAggregateType aggregateType)
    {
        await using var scope = await MongoScope.CreateAsync();
        var aggregateId = Guid.NewGuid();
        await scope.InsertProductScopeAggregateAsync(
            aggregateType,
            aggregateId,
            1,
            [CreateIntent(scope.TenantA, aggregateType, aggregateId)]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scope.Delivery(scope.TenantA).DiscoverEligibleAsync(10, cancellation.Token));
    }

    [Theory]
    [InlineData(AuditAggregateType.ProductLegalEntityScopePolicy)]
    [InlineData(AuditAggregateType.ProductLegalEntityScopeRolloutState)]
    public async Task Product_scope_audit_update_refuses_to_cross_complete_Bson_limit_without_mutation(
        AuditAggregateType aggregateType)
    {
        await using var scope = await MongoScope.CreateAsync();
        var evidence = await scope.InsertExactLimitProductScopeAggregateAsync(aggregateType, version: 43);
        scope.Clock.SetUtcNow(DateTimeOffset.UtcNow);

        var claim = await scope.Delivery(scope.TenantA).TryClaimAsync(
            evidence.Locator,
            0,
            "budget-worker",
            TimeSpan.FromMinutes(5));
        var stored = await scope.ReadProductScopeAggregateAsync(aggregateType, evidence.Locator.AggregateId);

        Assert.Null(claim);
        Assert.Equal(43, stored.Version);
        var intent = Assert.Single(stored.Intents);
        Assert.Equal(AuditIntentDeliveryState.Pending, intent.DeliveryState);
        Assert.Equal(0, intent.ClaimGeneration);
        Assert.Null(intent.ClaimToken);
        Assert.Empty(stored.Receipts);
    }

    [Fact]
    public async Task Cross_tenant_discovery_claim_and_completion_fail_without_disclosure()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservation = CreateReservation(scope.TenantA);
        await scope.Reservations.InsertOneAsync(reservation);
        var tenantBRepository = scope.Delivery(scope.TenantB);
        var foreignLocator = Locator(reservation);
        var now = DateTimeOffset.UtcNow;
        scope.Clock.SetUtcNow(now);

        Assert.Empty(await tenantBRepository.DiscoverEligibleAsync(10));
        Assert.Null(await tenantBRepository.TryClaimAsync(
            foreignLocator, 0, "worker-b", TimeSpan.FromMinutes(5)));
        var forgedClaim = new AuditIntentClaim(
            foreignLocator, "opaque", "worker-b", 1, now, now.AddMinutes(5), 1);
        Assert.False(await tenantBRepository.MarkDeliveredAsync(forgedClaim, Acknowledgement(forgedClaim, now)));

        var stored = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Equal(AuditIntentDeliveryState.Pending, Assert.Single(stored.AuditIntents).DeliveryState);
    }

    [Fact]
    public async Task Repository_registration_or_discovery_never_marks_intent_delivered_without_transport()
    {
        await using var scope = await MongoScope.CreateAsync();
        var product = CreateGlobalProduct(scope.TenantA);
        await scope.GlobalProducts.InsertOneAsync(product);
        var repository = scope.Delivery(scope.TenantA);

        Assert.Single(await repository.DiscoverEligibleAsync(10));

        var stored = await scope.GlobalProducts.Find(item => item.Id == product.Id).SingleAsync();
        var intent = Assert.Single(stored.AuditIntents);
        Assert.Equal(AuditIntentDeliveryState.Pending, intent.DeliveryState);
        Assert.Null(intent.CentralAcknowledgement);
        Assert.Null(intent.DeliveredAt);
    }

    private static CodeReservation CreateReservation(Guid tenantId, int version = 0)
    {
        var id = Guid.NewGuid();
        return new CodeReservation
        {
            Id = id,
            TenantId = tenantId,
            EntityType = CodeBearingEntityType.GlobalProduct,
            ReservedCode = $"GP-{Interlocked.Increment(ref _codeSequence):D12}",
            ReservationCommandId = Guid.NewGuid().ToString("N"),
            ReservedAt = DateTimeOffset.UtcNow,
            ReservedByActorId = Guid.NewGuid().ToString("N"),
            Version = version,
            AuditIntents = [CreateIntent(tenantId, AuditAggregateType.CodeReservation, id)]
        };
    }

    private static GlobalProduct CreateGlobalProduct(Guid tenantId)
    {
        var id = Guid.NewGuid();
        return new GlobalProduct
        {
            Id = id,
            TenantId = tenantId,
            CanonicalCode = $"GP-{Interlocked.Increment(ref _codeSequence):D12}",
            CodeReservationId = Guid.NewGuid(),
            Version = 9,
            AuditIntents = [CreateIntent(tenantId, AuditAggregateType.GlobalProduct, id)]
        };
    }

    private static ProductDefinitionRevision CreateProductDefinitionRevision(Guid tenantId)
    {
        var id = Guid.NewGuid();
        return new ProductDefinitionRevision
        {
            Id = id,
            TenantId = tenantId,
            GlobalProductId = Guid.NewGuid(),
            RevisionIdentifier = "REV-001",
            CreationCommandId = Guid.NewGuid().ToString("N"),
            Version = 5,
            AuditIntents = [CreateIntent(tenantId, AuditAggregateType.ProductDefinitionRevision, id)]
        };
    }

    private static Gsku CreateGsku(Guid tenantId)
    {
        var id = Guid.NewGuid();
        return new Gsku
        {
            Id = id,
            TenantId = tenantId,
            ProductDefinitionRevisionId = Guid.NewGuid(),
            CanonicalCode = $"GS-{Interlocked.Increment(ref _codeSequence):D12}",
            CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("N"),
            PackApplicabilityCode = "STANDARD",
            PackQuantity = 1m,
            PackUomCode = "EA",
            Version = 7,
            AuditIntents = [CreateIntent(tenantId, AuditAggregateType.Gsku, id)]
        };
    }

    private static FinishedGood CreateFinishedGood(Guid tenantId)
    {
        var id = Guid.NewGuid();
        return new FinishedGood
        {
            Id = id,
            TenantId = tenantId,
            GskuId = Guid.NewGuid(),
            CanonicalCode = $"FG-{Interlocked.Increment(ref _codeSequence):D12}",
            CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("N"),
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
            Version = 13,
            AuditIntents = [CreateIntent(tenantId, AuditAggregateType.FinishedGood, id)]
        };
    }

    private static Lsku CreateLsku(Guid tenantId)
    {
        var id = Guid.NewGuid();
        return new Lsku
        {
            Id = id,
            TenantId = tenantId,
            GskuId = Guid.NewGuid(),
            CanonicalCode = $"LS-{Interlocked.Increment(ref _codeSequence):D12}",
            CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("N"),
            MarketCode = "TR",
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
            Version = 23,
            AuditIntents = [CreateIntent(tenantId, AuditAggregateType.Lsku, id)]
        };
    }

    private static LocalAuditIntent CreateIntent(
        Guid tenantId,
        AuditAggregateType aggregateType,
        Guid aggregateId)
    {
        var intent = new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(),
            TenantId = tenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Operation = aggregateType switch
            {
                AuditAggregateType.CodeReservation => ProductAuditOperation.CodeReserved,
                AuditAggregateType.ProductDefinitionRevision =>
                    ProductAuditOperation.ProductDefinitionRevisionDraftCreated,
                AuditAggregateType.Gsku => ProductAuditOperation.GskuDraftCreated,
                AuditAggregateType.FinishedGood => ProductAuditOperation.FinishedGoodDraftCreated,
                AuditAggregateType.Lsku => ProductAuditOperation.LskuDraftCreated,
                AuditAggregateType.ProductLegalEntityScopePolicy =>
                    ProductAuditOperation.ProductLegalEntityScopePolicyCreated,
                AuditAggregateType.ProductLegalEntityScopeRolloutState =>
                    ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated,
                _ => ProductAuditOperation.GlobalProductDraftCreated
            },
            ActorId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString("N"),
            CausationId = Guid.NewGuid().ToString("N"),
            CommandId = Guid.NewGuid().ToString("N"),
            Sequence = 1,
            TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            EvidenceHash = Guid.NewGuid().ToString("N"),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            DeliveryState = AuditIntentDeliveryState.Pending
        };
        AuditIntentTemporalStorage.ApplyCurrentVersion(intent);
        return intent;
    }

    private static AuditIntentLocator Locator(CodeReservation reservation)
        => new(
            reservation.TenantId,
            AuditAggregateType.CodeReservation,
            reservation.Id,
            reservation.AuditIntents[0].IntentId);

    private static AuditIntentLocator Locator(GlobalProduct product)
        => new(
            product.TenantId,
            AuditAggregateType.GlobalProduct,
            product.Id,
            product.AuditIntents[0].IntentId);

    private static AuditIntentLocator Locator(FinishedGood finishedGood)
        => new(
            finishedGood.TenantId,
            AuditAggregateType.FinishedGood,
            finishedGood.Id,
            finishedGood.AuditIntents[0].IntentId);

    private static AuditIntentLocator Locator(Lsku lsku)
        => new(
            lsku.TenantId,
            AuditAggregateType.Lsku,
            lsku.Id,
            lsku.AuditIntents[0].IntentId);

    private static AuditIntentAcknowledgement Acknowledgement(
        AuditIntentClaim claim,
        DateTimeOffset acceptedAt)
    {
        const string contractVersion = "owner-approved-contract-test-v1";
        return new AuditIntentAcknowledgement(
            "durable-outbox-accepted",
            AuditIntentContract.BuildCentralIdempotencyKey(
                claim.Locator.TenantId,
                claim.Locator.IntentId,
                contractVersion),
            contractVersion,
            acceptedAt);
    }

    private static Task SoftDeleteAsync<TEntity>(IMongoCollection<TEntity> collection, Guid id)
        where TEntity : EntityBase
        => collection.UpdateOneAsync(
            item => item.Id == id,
            Builders<TEntity>.Update
                .Set(item => item.IsDeleted, true)
                .Set(item => item.DeletedAt, DateTimeOffset.UtcNow));

    private sealed class MongoScope : IAsyncDisposable
    {
        private MongoScope(
            IMongoDatabase database,
            ManualTimeProvider clock)
        {
            Database = database;
            Clock = clock;
            TenantA = Guid.NewGuid();
            TenantB = Guid.NewGuid();
        }

        public IMongoDatabase Database { get; }
        public ManualTimeProvider Clock { get; }
        public Guid TenantA { get; }
        public Guid TenantB { get; }
        public IMongoCollection<CodeReservation> Reservations =>
            Database.GetCollection<CodeReservation>("mdm_code_reservations");
        public IMongoCollection<GlobalProduct> GlobalProducts =>
            Database.GetCollection<GlobalProduct>("mdm_global_products");
        public IMongoCollection<ProductDefinitionRevision> ProductDefinitionRevisions =>
            Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions");
        public IMongoCollection<Gsku> Gskus =>
            Database.GetCollection<Gsku>("mdm_gskus");
        public IMongoCollection<FinishedGood> FinishedGoods =>
            Database.GetCollection<FinishedGood>("mdm_finished_goods");
        public IMongoCollection<Lsku> Lskus =>
            Database.GetCollection<Lsku>("mdm_lskus");
        public IMongoCollection<ProductLegalEntityScopePolicy> ProductScopePolicies =>
            Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies");
        public IMongoCollection<ProductLegalEntityScopeRolloutState> ProductScopeRolloutStates =>
            Database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states");

        public static async Task<MongoScope> CreateAsync()
        {
            var connectionString = Environment.GetEnvironmentVariable("MDM_TEST_MONGO")
                ?? "mongodb://localhost:27017";
            var settings = MongoClientSettings.FromConnectionString(connectionString);
#pragma warning disable CS0618
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var database = client.GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
            await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1));
            return new MongoScope(database, new ManualTimeProvider(DateTimeOffset.UtcNow));
        }

        public AuditIntentDeliveryRepository Delivery(Guid tenantId)
            => new(Database, Tenant(tenantId), Clock);

        public CodeReservationRepository ReservationBusiness(Guid tenantId)
            => new(Database, Tenant(tenantId));

        public GlobalProductRepository ProductBusiness(Guid tenantId)
            => new(Database, Tenant(tenantId));

        public async Task<Guid> InsertAuditAggregateAsync(AuditAggregateType aggregateType, int version)
        {
            switch (aggregateType)
            {
                case AuditAggregateType.CodeReservation:
                {
                    var aggregate = CreateReservation(TenantA, version);
                    await Reservations.InsertOneAsync(aggregate);
                    return aggregate.Id;
                }
                case AuditAggregateType.GlobalProduct:
                {
                    var aggregate = CreateGlobalProduct(TenantA);
                    aggregate.Version = version;
                    await GlobalProducts.InsertOneAsync(aggregate);
                    return aggregate.Id;
                }
                case AuditAggregateType.ProductDefinitionRevision:
                {
                    var aggregate = CreateProductDefinitionRevision(TenantA);
                    aggregate.Version = version;
                    await ProductDefinitionRevisions.InsertOneAsync(aggregate);
                    return aggregate.Id;
                }
                case AuditAggregateType.Gsku:
                {
                    var aggregate = CreateGsku(TenantA);
                    aggregate.Version = version;
                    await Gskus.InsertOneAsync(aggregate);
                    return aggregate.Id;
                }
                case AuditAggregateType.FinishedGood:
                {
                    var aggregate = CreateFinishedGood(TenantA);
                    aggregate.Version = version;
                    await FinishedGoods.InsertOneAsync(aggregate);
                    return aggregate.Id;
                }
                case AuditAggregateType.Lsku:
                {
                    var aggregate = CreateLsku(TenantA);
                    aggregate.Version = version;
                    await Lskus.InsertOneAsync(aggregate);
                    return aggregate.Id;
                }
                case AuditAggregateType.ProductLegalEntityScopePolicy:
                case AuditAggregateType.ProductLegalEntityScopeRolloutState:
                {
                    var aggregateId = Guid.NewGuid();
                    await InsertProductScopeAggregateAsync(
                        aggregateType,
                        aggregateId,
                        version,
                        [CreateIntent(TenantA, aggregateType, aggregateId)]);
                    return aggregateId;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(aggregateType));
            }
        }

        public async Task<(int Version, List<LocalAuditIntent> Intents, List<LocalAuditIntentReceipt> Receipts)>
            ReadAuditAggregateAsync(AuditAggregateType aggregateType, Guid aggregateId)
        {
            switch (aggregateType)
            {
                case AuditAggregateType.CodeReservation:
                {
                    var aggregate = await Reservations.Find(item => item.Id == aggregateId).SingleAsync();
                    return (aggregate.Version, aggregate.AuditIntents, aggregate.AuditIntentReceipts);
                }
                case AuditAggregateType.GlobalProduct:
                {
                    var aggregate = await GlobalProducts.Find(item => item.Id == aggregateId).SingleAsync();
                    return (aggregate.Version, aggregate.AuditIntents, aggregate.AuditIntentReceipts);
                }
                case AuditAggregateType.ProductDefinitionRevision:
                {
                    var aggregate = await ProductDefinitionRevisions.Find(item => item.Id == aggregateId).SingleAsync();
                    return (aggregate.Version, aggregate.AuditIntents, aggregate.AuditIntentReceipts);
                }
                case AuditAggregateType.Gsku:
                {
                    var aggregate = await Gskus.Find(item => item.Id == aggregateId).SingleAsync();
                    return (aggregate.Version, aggregate.AuditIntents, aggregate.AuditIntentReceipts);
                }
                case AuditAggregateType.FinishedGood:
                {
                    var aggregate = await FinishedGoods.Find(item => item.Id == aggregateId).SingleAsync();
                    return (aggregate.Version, aggregate.AuditIntents, aggregate.AuditIntentReceipts);
                }
                case AuditAggregateType.Lsku:
                {
                    var aggregate = await Lskus.Find(item => item.Id == aggregateId).SingleAsync();
                    return (aggregate.Version, aggregate.AuditIntents, aggregate.AuditIntentReceipts);
                }
                case AuditAggregateType.ProductLegalEntityScopePolicy:
                case AuditAggregateType.ProductLegalEntityScopeRolloutState:
                    return await ReadProductScopeAggregateAsync(aggregateType, aggregateId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(aggregateType));
            }
        }

        public async Task InsertProductScopeAggregateAsync(
            AuditAggregateType aggregateType,
            Guid aggregateId,
            int version,
            List<LocalAuditIntent> intents)
        {
            if (aggregateType == AuditAggregateType.ProductLegalEntityScopePolicy)
            {
                var policy = ProductLegalEntityScopePolicy.Create(
                    TenantA,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ProductLegalEntityScopeMode.GroupWide,
                    [],
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow.AddMinutes(-3));
                policy.Id = aggregateId;
                policy.Version = version;
                policy.AuditIntents = intents;
                await ProductScopePolicies.InsertOneAsync(policy);
                return;
            }

            var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
                TenantA,
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(-3));
            rollout.Id = aggregateId;
            rollout.Version = version;
            rollout.AuditIntents = intents;
            await ProductScopeRolloutStates.InsertOneAsync(rollout);
        }

        public async Task<(AuditIntentLocator Locator, int SerializedBytes)>
            InsertExactLimitProductScopeAggregateAsync(
                AuditAggregateType aggregateType,
                int version)
            {
                var aggregateId = Guid.NewGuid();
                var intent = CreateIntent(TenantA, aggregateType, aggregateId);
                intent.EvidenceHash = string.Empty;
                if (aggregateType == AuditAggregateType.ProductLegalEntityScopePolicy)
            {
                var policy = ProductLegalEntityScopePolicy.Create(
                    TenantA,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ProductLegalEntityScopeMode.GroupWide,
                    [],
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow.AddMinutes(-3));
                policy.Id = aggregateId;
                policy.Version = version;
                policy.AuditIntents = [intent];
                var baseSize = policy.ToBsonDocument().ToBson().Length;
                intent.EvidenceHash = new string(
                    'x',
                    ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes - baseSize);
                var bytes = policy.ToBsonDocument().ToBson().Length;
                Assert.Equal(ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes, bytes);
                await ProductScopePolicies.InsertOneAsync(policy);
                return (new AuditIntentLocator(TenantA, aggregateType, aggregateId, intent.IntentId), bytes);
            }

            var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
                TenantA,
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(-3));
            rollout.Id = aggregateId;
            rollout.Version = version;
            rollout.AuditIntents = [intent];
            var rolloutBaseSize = rollout.ToBsonDocument().ToBson().Length;
            intent.EvidenceHash = new string(
                'x',
                ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes - rolloutBaseSize);
            var rolloutBytes = rollout.ToBsonDocument().ToBson().Length;
            Assert.Equal(ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes, rolloutBytes);
            await ProductScopeRolloutStates.InsertOneAsync(rollout);
            return (new AuditIntentLocator(TenantA, aggregateType, aggregateId, intent.IntentId), rolloutBytes);
        }

        public async Task<(int Version, List<LocalAuditIntent> Intents, List<LocalAuditIntentReceipt> Receipts)>
            ReadProductScopeAggregateAsync(
            AuditAggregateType aggregateType,
            Guid aggregateId)
        {
            if (aggregateType == AuditAggregateType.ProductLegalEntityScopePolicy)
            {
                var policy = await ProductScopePolicies.Find(item => item.Id == aggregateId).SingleAsync();
                return (policy.Version, policy.AuditIntents, policy.AuditIntentReceipts);
            }

            var rollout = await ProductScopeRolloutStates.Find(item => item.Id == aggregateId).SingleAsync();
            return (rollout.Version, rollout.AuditIntents, rollout.AuditIntentReceipts);
        }

        public async ValueTask DisposeAsync()
        {
            await Task.WhenAll(
                Reservations.DeleteManyAsync(item => item.TenantId == TenantA || item.TenantId == TenantB),
                GlobalProducts.DeleteManyAsync(item => item.TenantId == TenantA || item.TenantId == TenantB),
                ProductDefinitionRevisions.DeleteManyAsync(item => item.TenantId == TenantA || item.TenantId == TenantB),
                Gskus.DeleteManyAsync(item => item.TenantId == TenantA || item.TenantId == TenantB),
                FinishedGoods.DeleteManyAsync(item => item.TenantId == TenantA || item.TenantId == TenantB),
                Lskus.DeleteManyAsync(item => item.TenantId == TenantA || item.TenantId == TenantB),
                ProductScopePolicies.DeleteManyAsync(item => item.TenantId == TenantA || item.TenantId == TenantB),
                ProductScopeRolloutStates.DeleteManyAsync(item => item.TenantId == TenantA || item.TenantId == TenantB));
        }

        private static TenantContext Tenant(Guid tenantId)
        {
            var context = new TenantContext();
            context.SetTenant(tenantId);
            return context;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly object _sync = new();
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            lock (_sync)
            {
                return _utcNow;
            }
        }

        public void SetUtcNow(DateTimeOffset utcNow)
        {
            lock (_sync)
            {
                _utcNow = utcNow;
            }
        }

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            lock (_sync)
            {
                _utcNow = _utcNow.Add(duration);
            }
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProductLegalEntityScopeMongoCollection
{
    public const string Name = "ProductLegalEntityScopeMongo";
    public const string DatabaseName = "diten_mdm_product_scope_itest";
}
