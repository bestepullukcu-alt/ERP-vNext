using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

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
        => new()
        {
            IntentId = Guid.NewGuid(),
            TenantId = tenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Operation = aggregateType switch
            {
                AuditAggregateType.CodeReservation => ProductAuditOperation.CodeReserved,
                AuditAggregateType.FinishedGood => ProductAuditOperation.FinishedGoodDraftCreated,
                AuditAggregateType.Lsku => ProductAuditOperation.LskuDraftCreated,
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
        private readonly IMongoClient _client;
        private readonly string _databaseName;

        private MongoScope(
            IMongoClient client,
            IMongoDatabase database,
            string databaseName,
            ManualTimeProvider clock)
        {
            _client = client;
            Database = database;
            _databaseName = databaseName;
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
        public IMongoCollection<FinishedGood> FinishedGoods =>
            Database.GetCollection<FinishedGood>("mdm_finished_goods");
        public IMongoCollection<Lsku> Lskus =>
            Database.GetCollection<Lsku>("mdm_lskus");

        public static async Task<MongoScope> CreateAsync()
        {
            var connectionString = Environment.GetEnvironmentVariable("MDM_TEST_MONGO")
                ?? "mongodb://localhost:27017";
            var settings = MongoClientSettings.FromConnectionString(connectionString);
#pragma warning disable CS0618
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var databaseName = $"diten_mdm_audit_worker_tests_{Guid.NewGuid():N}";
            var database = client.GetDatabase(databaseName);
            await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1));
            return new MongoScope(client, database, databaseName, new ManualTimeProvider(DateTimeOffset.UtcNow));
        }

        public AuditIntentDeliveryRepository Delivery(Guid tenantId)
            => new(Database, Tenant(tenantId), Clock);

        public CodeReservationRepository ReservationBusiness(Guid tenantId)
            => new(Database, Tenant(tenantId));

        public GlobalProductRepository ProductBusiness(Guid tenantId)
            => new(Database, Tenant(tenantId));

        public async ValueTask DisposeAsync()
        {
            await _client.DropDatabaseAsync(_databaseName);
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
