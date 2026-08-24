using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductItemSkuMasterMongoTests
{
    [Fact]
    public async Task First_gsku_create_persists_verified_selections_and_replay_returns_same_pair()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var parent = await InsertParentAsync(scope, scope.TenantA);
        var reservation = await scope.Reservations(scope.TenantA).ReserveAsync(
            CodeBearingEntityType.Gsku, "gsku-reserve-1", "actor", "corr");
        var resolver = new VerifiedResolver();
        var handler = CreateFirstGskuHandler(scope, scope.TenantA, resolver);
        var request = new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
        {
            GlobalProductId = parent.Id,
            GskuReservationId = reservation.Id,
            ExpectedReservationVersion = reservation.Version,
            CreationCommandId = " first-gsku-command ",
            PackQuantity = 10m,
            PackUomCode = "C62"
        };

        var first = await handler.Handle(new CreateFirstGskuDraftCommand(request), CancellationToken.None);
        var replay = await handler.Handle(new CreateFirstGskuDraftCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(replay.IsSuccessful);
        Assert.Equal("REV-001", first.Data!.RevisionIdentifier);
        Assert.Equal(first.Data.ProductDefinitionRevisionId, replay.Data!.ProductDefinitionRevisionId);
        Assert.Equal(first.Data.GskuId, replay.Data.GskuId);
        Assert.Equal("FIRST-GSKU-COMMAND", first.Data.CreationCommandId);
        Assert.Equal(1, resolver.CallCount);
        var stored = await scope.Database.GetCollection<Gsku>("mdm_gskus")
            .Find(Builders<Gsku>.Filter.Eq(x => x.Id, first.Data.GskuId)).SingleAsync();
        Assert.Equal("pack-applicability", stored.PackApplicabilitySelection.SetCode);
        Assert.Equal("SCALAR_QUANTITY_APPLIES", stored.PackApplicabilitySelection.ValueCode);
        Assert.Equal("uom", stored.PackUomSelection.SetCode);
        Assert.Equal("C62", stored.PackUomSelection.ValueCode);
        Assert.Equal(ReferenceCatalogResolutionMode.Latest, stored.PackUomSelection.ResolutionMode);
        Assert.Single(stored.AuditIntents);
        Assert.Equal(1, await scope.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .CountDocumentsAsync(Builders<ProductDefinitionRevision>.Filter.Empty));
        Assert.Equal(1, await scope.Database.GetCollection<Gsku>("mdm_gskus")
            .CountDocumentsAsync(Builders<Gsku>.Filter.Empty));

        var updater = new UpdateGskuDraftHandler(
            new GskuRepository(scope.Database, scope.Context(scope.TenantA)),
            new ProductDefinitionRevisionRepository(scope.Database, scope.Context(scope.TenantA)),
            scope.Reservations(scope.TenantA),
            resolver,
            new TestActorContext());
        var updateRequest = new ProductItemSkuMasterModels.UpdateGskuDraftRequest
        {
            GskuId = first.Data.GskuId,
            ExpectedVersion = 0,
            PackQuantity = 2.125m,
            PackUomCode = "KGM"
        };
        var updated = await updater.Handle(new UpdateGskuDraftCommand(updateRequest), CancellationToken.None);
        var stale = await updater.Handle(new UpdateGskuDraftCommand(updateRequest), CancellationToken.None);
        Assert.True(updated.IsSuccessful);
        Assert.Equal(1, updated.Data!.Version);
        Assert.False(stale.IsSuccessful);
        Assert.Contains("CONCURRENCY_CONFLICT", stale.Errors);

        var delivery = new AuditIntentDeliveryRepository(
            scope.Database,
            scope.Context(scope.TenantA),
            TimeProvider.System);
        var work = await delivery.DiscoverEligibleAsync(10);
        Assert.Contains(work, x => x.Locator.AggregateType == AuditAggregateType.ProductDefinitionRevision);
        Assert.Contains(work, x => x.Locator.AggregateType == AuditAggregateType.Gsku);
        foreach (var item in work.Where(x => x.Locator.AggregateType is
                     AuditAggregateType.ProductDefinitionRevision or AuditAggregateType.Gsku))
        {
            var claim = Assert.IsType<AuditIntentClaim>(
                await delivery.TryClaimAsync(item.Locator, item.ClaimGeneration, "test-worker", TimeSpan.FromMinutes(1)));
            const string contractVersion = "v1";
            var acknowledgement = new AuditIntentAcknowledgement(
                "central-ack",
                AuditIntentContract.BuildCentralIdempotencyKey(item.Locator.TenantId, item.Locator.IntentId, contractVersion),
                contractVersion,
                DateTimeOffset.UtcNow);
            Assert.True(await delivery.MarkDeliveredAsync(claim, acknowledgement));
            Assert.True(await delivery.CompactDeliveredAsync(claim, "receipt-" + item.Locator.IntentId.ToString("N")));
        }
        var revisionAfterDelivery = await scope.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .Find(Builders<ProductDefinitionRevision>.Filter.Eq(x => x.Id, first.Data.ProductDefinitionRevisionId)).SingleAsync();
        var gskuAfterDelivery = await scope.Database.GetCollection<Gsku>("mdm_gskus")
            .Find(Builders<Gsku>.Filter.Eq(x => x.Id, first.Data.GskuId)).SingleAsync();
        Assert.Equal(0, revisionAfterDelivery.Version);
        Assert.Equal(1, gskuAfterDelivery.Version);
        Assert.NotEmpty(revisionAfterDelivery.AuditIntentReceipts);
        Assert.NotEmpty(gskuAfterDelivery.AuditIntentReceipts);
    }

    [Fact]
    public async Task Concurrent_first_gsku_commands_allocate_unique_parent_ordinals_and_soft_delete_never_reuses()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var parent = await InsertParentAsync(scope, scope.TenantA);
        var reservations = new List<CodeReservation>();
        for (var index = 0; index < 6; index++)
        {
            reservations.Add(await scope.Reservations(scope.TenantA).ReserveAsync(
                CodeBearingEntityType.Gsku, $"gsku-reserve-{index}", "actor", "corr"));
        }

        var tasks = reservations.Select((reservation, index) => CreateFirstGskuHandler(scope, scope.TenantA, new VerifiedResolver())
            .Handle(new CreateFirstGskuDraftCommand(new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
            {
                GlobalProductId = parent.Id,
                GskuReservationId = reservation.Id,
                ExpectedReservationVersion = reservation.Version,
                CreationCommandId = $"concurrent-{index}",
                PackQuantity = 1.250m,
                PackUomCode = "KGM"
            }), CancellationToken.None));
        var results = await Task.WhenAll(tasks);

        Assert.All(results, result => Assert.True(result.IsSuccessful, string.Join(',', result.Errors)));
        Assert.Equal(6, results.Select(x => x.Data!.RevisionIdentifier).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 6).Select(x => $"REV-{x:D3}"),
            results.Select(x => x.Data!.RevisionIdentifier).OrderBy(x => x));
        var firstId = results.Single(x => x.Data!.RevisionIdentifier == "REV-001").Data!.ProductDefinitionRevisionId;
        await scope.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").UpdateOneAsync(
            Builders<ProductDefinitionRevision>.Filter.Eq(x => x.Id, firstId),
            Builders<ProductDefinitionRevision>.Update.Set(x => x.IsDeleted, true).Set(x => x.DeletedAt, DateTimeOffset.UtcNow));
        var nextReservation = await scope.Reservations(scope.TenantA).ReserveAsync(
            CodeBearingEntityType.Gsku, "gsku-reserve-next", "actor", "corr");
        var next = await CreateFirstGskuHandler(scope, scope.TenantA, new VerifiedResolver()).Handle(
            new CreateFirstGskuDraftCommand(new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
            {
                GlobalProductId = parent.Id,
                GskuReservationId = nextReservation.Id,
                ExpectedReservationVersion = nextReservation.Version,
                CreationCommandId = "after-soft-delete",
                PackQuantity = 1m,
                PackUomCode = "C62"
            }), CancellationToken.None);
        Assert.Equal("REV-007", next.Data!.RevisionIdentifier);
    }

    [Fact]
    public async Task First_gsku_parent_and_provider_failures_are_fail_closed_without_writes()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var tenantAParent = await InsertParentAsync(scope, scope.TenantA);
        var reservation = await scope.Reservations(scope.TenantB).ReserveAsync(
            CodeBearingEntityType.Gsku, "tenant-b-reserve", "actor", "corr");
        var handler = CreateFirstGskuHandler(scope, scope.TenantB, new VerifiedResolver());
        var crossTenant = await handler.Handle(new CreateFirstGskuDraftCommand(new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
        {
            GlobalProductId = tenantAParent.Id,
            GskuReservationId = reservation.Id,
            ExpectedReservationVersion = reservation.Version,
            CreationCommandId = "cross-tenant",
            PackQuantity = 1m,
            PackUomCode = "C62"
        }), CancellationToken.None);
        Assert.Equal(404, crossTenant.StatusCode);
        Assert.Contains("PARENT_NOT_FOUND", crossTenant.Errors);

        var tenantBParent = await InsertParentAsync(scope, scope.TenantB);
        var providerFailure = await CreateFirstGskuHandler(scope, scope.TenantB, new VerifiedResolver(false)).Handle(
            new CreateFirstGskuDraftCommand(new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
            {
                GlobalProductId = tenantBParent.Id,
                GskuReservationId = reservation.Id,
                ExpectedReservationVersion = reservation.Version,
                CreationCommandId = "provider-failure",
                PackQuantity = 1m,
                PackUomCode = "C62"
            }), CancellationToken.None);
        Assert.False(providerFailure.IsSuccessful);
        Assert.Equal(503, providerFailure.StatusCode);
        Assert.Equal(0, await scope.Database.GetCollection<Gsku>("mdm_gskus")
            .CountDocumentsAsync(Builders<Gsku>.Filter.Empty));
        var unchanged = await scope.Reservations(scope.TenantB).GetByIdAsync(reservation.Id);
        Assert.Equal(CodeReservationState.Reserved, unchanged!.ReservationState);
    }

    [Fact]
    public void First_gsku_validator_rejects_provider_evidence_unknown_fields_and_invalid_precision()
    {
        var validator = new CreateFirstGskuDraftValidator();
        var request = new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
        {
            GlobalProductId = Guid.NewGuid(),
            GskuReservationId = Guid.NewGuid(),
            CreationCommandId = "cmd",
            PackQuantity = 1.5m,
            PackUomCode = "C62",
            UnmappedFields = new Dictionary<string, JsonElement>
            {
                ["CatalogVersionId"] = JsonDocument.Parse("\"forbidden\"").RootElement.Clone()
            }
        };
        var result = validator.Validate(new CreateFirstGskuDraftCommand(request));
        Assert.Contains(result.Errors, x => x.ErrorMessage == "PACK_QUANTITY_PRECISION_EXCEEDED");
        Assert.Contains(result.Errors, x => x.ErrorMessage == "REFERENCE_CATALOG_EVIDENCE_CLIENT_OVERRIDE_FORBIDDEN");
        Assert.Equal(1, (int)AuditAggregateType.CodeReservation);
        Assert.Equal(2, (int)AuditAggregateType.GlobalProduct);
        Assert.Equal(5, (int)ProductAuditOperation.GlobalProductDraftCreated);
        Assert.Equal(6, (int)ProductAuditOperation.ProductDefinitionRevisionDraftCreated);
    }

    [Theory]
    [InlineData(CodeBearingEntityType.Gsku)]
    [InlineData(CodeBearingEntityType.Lsku)]
    [InlineData(CodeBearingEntityType.FinishedGood)]
    public async Task Common_ledger_unique_index_rejects_same_tenant_code_across_entity_types(
        CodeBearingEntityType collidingEntityType)
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var repository = scope.Reservations(scope.TenantA);
        var original = await repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "reserve-unique", "actor", "corr");
        var collection = scope.Database.GetCollection<CodeReservation>("mdm_code_reservations");
        var collision = new CodeReservation
        {
            Id = Guid.NewGuid(),
            TenantId = scope.TenantA,
            EntityType = collidingEntityType,
            ReservedCode = original.ReservedCode,
            ReservationCommandId = "reserve-collision",
            ReservedAt = DateTimeOffset.UtcNow,
            ReservedByActorId = "actor"
        };

        var exception = await Assert.ThrowsAsync<MongoWriteException>(
            () => collection.InsertOneAsync(collision));

        Assert.Equal(ServerErrorCategory.DuplicateKey, exception.WriteError?.Category);
    }

    [Fact]
    public async Task Allocator_uses_one_tenant_namespace_for_all_four_entity_types()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var repository = scope.Reservations(scope.TenantA);
        var reservations = new List<CodeReservation>();

        foreach (var entityType in Enum.GetValues<CodeBearingEntityType>())
        {
            reservations.Add(await repository.ReserveAsync(
                entityType, $"reserve-{entityType}", "actor", "corr"));
        }

        Assert.Equal(4, reservations.Select(x => x.ReservedCode).Distinct(StringComparer.Ordinal).Count());
        Assert.All(reservations, reservation => Assert.Equal(scope.TenantA, reservation.TenantId));
        Assert.All(reservations, reservation => Assert.Single(reservation.AuditIntents));
    }

    [Fact]
    public async Task Counter_is_tenant_owned_guid_scoped_and_has_tenant_first_unique_index()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var tenantAFirst = await scope.Reservations(scope.TenantA).ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "tenant-a-counter", "actor", "corr");
        var tenantBFirst = await scope.Reservations(scope.TenantB).ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "tenant-b-counter", "actor", "corr");
        var collection = scope.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_canonical_code_counters");
        var documents = await collection.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Empty).ToListAsync();
        using var indexCursor = await collection.Indexes.ListAsync();
        var indexes = await indexCursor.ToListAsync();
        var tenantIndex = Assert.Single(
            indexes,
            index => index["name"].AsString == "ux_mdm_canonical_code_counters_tenant");

        Assert.Equal("GP-000000000001", tenantAFirst.ReservedCode);
        Assert.Equal("GP-000000000001", tenantBFirst.ReservedCode);
        Assert.Equal(2, documents.Count);
        Assert.Contains(documents, document => document["TenantId"].AsGuid == scope.TenantA);
        Assert.Contains(documents, document => document["TenantId"].AsGuid == scope.TenantB);
        Assert.All(documents, document =>
        {
            Assert.False(document.Contains("TenantKey"));
            Assert.False(document["IsDeleted"].AsBoolean);
            Assert.True(document.Contains("CreatedAt"));
            Assert.True(document.Contains("UpdatedAt"));
            Assert.True(document.Contains("Version"));
        });
        Assert.Equal("TenantId", tenantIndex["key"].AsBsonDocument.GetElement(0).Name);
        Assert.True(tenantIndex["unique"].AsBoolean);
    }

    [Fact]
    public async Task Concurrent_reserve_and_consume_are_idempotent_and_expected_version_is_enforced()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var repository = scope.Reservations(scope.TenantA);
        var reserveTasks = Enumerable.Range(0, 4)
            .Select(_ => repository.ReserveAsync(
                CodeBearingEntityType.GlobalProduct, "same-reserve", "actor", "corr"));
        var reserved = await Task.WhenAll(reserveTasks);

        Assert.Single(reserved.Select(x => x.Id).Distinct());
        var reservation = reserved[0];

        var consumeTasks = Enumerable.Range(0, 4)
            .Select(_ => repository.ConsumeForIdentityAsync(
                reservation.Id,
                CodeBearingEntityType.GlobalProduct,
                Guid.NewGuid(),
                0,
                "same-consume",
                "actor",
                "corr"));
        var consumed = await Task.WhenAll(consumeTasks);

        Assert.All(consumed, result => Assert.True(result.Succeeded));
        Assert.Single(consumed.Select(x => x.Reservation!.ConsumedEntityId).Distinct());

        var stale = await repository.ConfirmIdentityBindingAsync(
            reservation.Id,
            consumed[0].Reservation!.ConsumedEntityId!.Value,
            0,
            "different-confirm",
            "actor",
            "corr");
        Assert.False(stale.Succeeded);
        Assert.Equal("CONCURRENCY_CONFLICT", stale.ErrorCode);
    }

    [Fact]
    public async Task Concurrent_initial_counter_upsert_recovers_without_duplicate_key()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var repository = scope.Reservations(scope.TenantA);

        var reservations = await Task.WhenAll(Enumerable.Range(0, 16).Select(index =>
            repository.ReserveAsync(
                CodeBearingEntityType.GlobalProduct,
                $"parallel-reserve-{index}",
                "actor",
                "corr")));

        Assert.Equal(16, reservations.Select(item => item.Id).Distinct().Count());
        Assert.Equal(16, reservations.Select(item => item.ReservedCode).Distinct().Count());
        Assert.All(reservations, item => Assert.Equal(scope.TenantA, item.TenantId));

        var counters = scope.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_canonical_code_counters");
        var counter = Assert.Single(await counters.Find(FilterDefinition<MongoDB.Bson.BsonDocument>.Empty).ToListAsync());
        Assert.Equal(scope.TenantA, counter["TenantId"].AsGuid);
        Assert.Equal(16, counter["NextSequence"].AsInt64);
    }

    [Fact]
    public void Strict_write_contract_rejects_client_tenant_and_direct_canonical_code()
    {
        var reservationId = Guid.NewGuid();
        var tenantJson = $$"""
            {"reservationId":"{{reservationId}}","expectedReservationVersion":0,"idempotencyKey":"cmd","tenantId":"{{Guid.NewGuid()}}"}
            """;
        var codeJson = $$"""
            {"reservationId":"{{reservationId}}","expectedReservationVersion":0,"idempotencyKey":"cmd","canonicalCode":"GP-000000000001"}
            """;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var tenantRequest = JsonSerializer.Deserialize<ProductItemSkuMasterModels.CreateGlobalProductDraftRequest>(tenantJson, options)!;
        var codeRequest = JsonSerializer.Deserialize<ProductItemSkuMasterModels.CreateGlobalProductDraftRequest>(codeJson, options)!;
        var validator = new CreateGlobalProductDraftValidator();

        var tenantResult = validator.Validate(new CreateGlobalProductDraftCommand(tenantRequest));
        var codeResult = validator.Validate(new CreateGlobalProductDraftCommand(codeRequest));

        Assert.Contains(tenantResult.Errors, error => error.ErrorMessage == "TENANT_ID_CLIENT_INPUT_FORBIDDEN");
        Assert.Contains(codeResult.Errors, error => error.ErrorMessage == "CANONICAL_CODE_ASSIGNMENT_FORBIDDEN");
    }

    [Fact]
    public async Task Global_product_cannot_persist_without_matching_consumed_reservation()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var repository = scope.GlobalProducts(scope.TenantA);
        var entity = new GlobalProduct
        {
            Id = Guid.NewGuid(),
            TenantId = scope.TenantA,
            CanonicalCode = "GP-999999999999",
            CodeReservationId = Guid.NewGuid(),
            AuditIntents = [CreateTestIntent(scope.TenantA, Guid.NewGuid())]
        };

        var result = await repository.CreateDraftAsync(entity);

        Assert.False(result.Succeeded);
        Assert.Equal("CODE_RESERVATION_REQUIRED", result.ErrorCode);
        Assert.Null(await repository.GetByIdAsync(entity.Id));
    }

    [Fact]
    public async Task Ambiguous_identity_write_keeps_reservation_pending_without_automatic_burn()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var reservations = scope.Reservations(scope.TenantA);
        var reservation = await reservations.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "burn-reserve", "actor", "corr");
        var handler = new CreateGlobalProductDraftHandler(
            reservations,
            new ThrowingGlobalProductRepository(),
            scope.Context(scope.TenantA),
            new TestActorContext());
        var request = new ProductItemSkuMasterModels.CreateGlobalProductDraftRequest
        {
            GlobalProductName = "Ambiguous Product",
            ReservationId = reservation.Id,
            ExpectedReservationVersion = 0,
            IdempotencyKey = "burn-create"
        };

        var response = await handler.Handle(new CreateGlobalProductDraftCommand(request), CancellationToken.None);
        var pending = await reservations.GetByIdAsync(reservation.Id);

        Assert.False(response.IsSuccessful);
        Assert.Equal(202, response.StatusCode);
        Assert.Contains("GLOBAL_PRODUCT_BINDING_RECONCILIATION_REQUIRED", response.Errors);
        Assert.NotNull(pending);
        Assert.Equal(CodeReservationState.Consumed, pending!.ReservationState);
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, pending.BindingState);
        Assert.Null(pending.BurnedAt);
        Assert.DoesNotContain(
            pending.AuditIntents,
            intent => intent.Operation == ProductAuditOperation.CodeBurned);

        var replay = await handler.Handle(new CreateGlobalProductDraftCommand(request), CancellationToken.None);
        Assert.False(replay.IsSuccessful);
        Assert.Equal(202, replay.StatusCode);
        Assert.Equal(pending.ConsumedEntityId, (await reservations.GetByIdAsync(reservation.Id))!.ConsumedEntityId);
    }

    [Fact]
    public async Task Successful_global_product_create_preserves_local_intents_and_idempotent_binding()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var reservations = scope.Reservations(scope.TenantA);
        var products = scope.GlobalProducts(scope.TenantA);
        var reservation = await reservations.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "product-reserve", "actor", "corr");
        var handler = new CreateGlobalProductDraftHandler(
            reservations,
            products,
            scope.Context(scope.TenantA),
            new TestActorContext());
        var request = new ProductItemSkuMasterModels.CreateGlobalProductDraftRequest
        {
            GlobalProductName = "Successful Product",
            ReservationId = reservation.Id,
            ExpectedReservationVersion = 0,
            IdempotencyKey = "product-create"
        };

        var first = await handler.Handle(new CreateGlobalProductDraftCommand(request), CancellationToken.None);
        var replay = await handler.Handle(new CreateGlobalProductDraftCommand(request), CancellationToken.None);
        var storedProduct = await products.GetByIdAsync(first.Data!.GlobalProductId);
        var storedReservation = await reservations.GetByIdAsync(reservation.Id);

        Assert.True(first.IsSuccessful);
        Assert.True(replay.IsSuccessful);
        Assert.Equal(first.Data.GlobalProductId, replay.Data!.GlobalProductId);
        Assert.NotNull(storedProduct);
        Assert.Single(storedProduct!.AuditIntents);
        Assert.Equal(ProductAuditOperation.GlobalProductDraftCreated, storedProduct.AuditIntents[0].Operation);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, storedProduct.LifecycleStatus);
        Assert.Null(await scope.GlobalProducts(scope.TenantB).GetByIdAsync(storedProduct.Id));
        Assert.NotNull(storedReservation);
        Assert.Equal(CodeReservationBindingState.Confirmed, storedReservation!.BindingState);
        Assert.Equal(3, storedReservation.AuditIntents.Count);
        Assert.Equal(
            new[]
            {
                ProductAuditOperation.CodeReserved,
                ProductAuditOperation.CodeConsumed,
                ProductAuditOperation.CodeBindingConfirmed
            },
            storedReservation.AuditIntents.Select(intent => intent.Operation));
        Assert.All(storedReservation.AuditIntents, intent => Assert.Equal(scope.TenantA, intent.TenantId));
    }

    [Fact]
    public async Task Cross_tenant_lookup_and_mutation_do_not_reveal_or_change_reservation()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var tenantARepository = scope.Reservations(scope.TenantA);
        var tenantBRepository = scope.Reservations(scope.TenantB);
        var reservation = await tenantARepository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "tenant-a-reserve", "actor", "corr");

        Assert.Null(await tenantBRepository.GetByIdAsync(reservation.Id));
        var crossTenant = await tenantBRepository.ConsumeForIdentityAsync(
            reservation.Id,
            CodeBearingEntityType.GlobalProduct,
            Guid.NewGuid(),
            0,
            "cross-tenant",
            "actor",
            "corr");

        Assert.False(crossTenant.Succeeded);
        Assert.Equal("CODE_RESERVATION_REQUIRED", crossTenant.ErrorCode);
        var unchanged = await tenantARepository.GetByIdAsync(reservation.Id);
        Assert.Equal(CodeReservationState.Reserved, unchanged!.ReservationState);
        Assert.Equal(0, unchanged.Version);
    }

    [Fact]
    public async Task Audit_intent_capacity_fails_closed_without_mutating_business_state()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var repository = scope.Reservations(scope.TenantA);
        var reservation = await repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "capacity-reserve", "actor", "corr");
        var collection = scope.Database.GetCollection<CodeReservation>("mdm_code_reservations");
        var fullBuffer = Enumerable.Range(0, AuditIntentLimits.MaxPerAggregate)
            .Select(index =>
            {
                var intent = CreateTestIntent(scope.TenantA, reservation.Id);
                intent.Sequence = index + 1;
                intent.IdempotencyKey = $"capacity-{index}";
                return intent;
            })
            .ToList();
        await collection.UpdateOneAsync(
            Builders<CodeReservation>.Filter.Eq(x => x.Id, reservation.Id),
            Builders<CodeReservation>.Update.Set(x => x.AuditIntents, fullBuffer));

        var result = await repository.ConsumeForIdentityAsync(
            reservation.Id,
            CodeBearingEntityType.GlobalProduct,
            Guid.NewGuid(),
            0,
            "capacity-consume",
            "actor",
            "corr");
        var unchanged = await repository.GetByIdAsync(reservation.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("AUDIT_INTENT_CAPACITY_EXCEEDED", result.ErrorCode);
        Assert.Equal(CodeReservationState.Reserved, unchanged!.ReservationState);
        Assert.Equal(0, unchanged.Version);
        Assert.Equal(AuditIntentLimits.MaxPerAggregate, unchanged.AuditIntents.Count);
    }

    [Fact]
    public async Task Consume_reserves_resolution_intent_capacity_before_identity_write()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var reservations = scope.Reservations(scope.TenantA);
        var products = scope.GlobalProducts(scope.TenantA);
        var reservation = await reservations.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "resolution-capacity-reserve", "actor", "corr");
        var collection = scope.Database.GetCollection<CodeReservation>("mdm_code_reservations");
        var bufferWithoutResolutionSlot = Enumerable.Range(0, AuditIntentLimits.MaxPerAggregate - 1)
            .Select(index =>
            {
                var intent = CreateTestIntent(scope.TenantA, reservation.Id);
                intent.Sequence = index + 1;
                intent.IdempotencyKey = $"resolution-capacity-{index}";
                return intent;
            })
            .ToList();
        await collection.UpdateOneAsync(
            Builders<CodeReservation>.Filter.Eq(x => x.Id, reservation.Id),
            Builders<CodeReservation>.Update.Set(x => x.AuditIntents, bufferWithoutResolutionSlot));
        var handler = new CreateGlobalProductDraftHandler(
            reservations,
            products,
            scope.Context(scope.TenantA),
            new TestActorContext());

        var response = await handler.Handle(
            new CreateGlobalProductDraftCommand(new ProductItemSkuMasterModels.CreateGlobalProductDraftRequest
            {
                GlobalProductName = "Audit Intent Product",
                ReservationId = reservation.Id,
                ExpectedReservationVersion = 0,
                IdempotencyKey = "resolution-capacity-create"
            }),
            CancellationToken.None);
        var unchanged = await reservations.GetByIdAsync(reservation.Id);

        Assert.False(response.IsSuccessful);
        Assert.Contains("AUDIT_INTENT_CAPACITY_EXCEEDED", response.Errors);
        Assert.Null(await products.GetByReservationIdAsync(reservation.Id));
        Assert.Equal(CodeReservationState.Reserved, unchanged!.ReservationState);
        Assert.Equal(0, unchanged.Version);
        Assert.Equal(AuditIntentLimits.MaxPerAggregate - 1, unchanged.AuditIntents.Count);
    }

    [Fact]
    public async Task Persisted_product_with_pending_confirmation_is_not_burned_and_replay_reconciles_idempotently()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var innerReservations = scope.Reservations(scope.TenantA);
        var products = scope.GlobalProducts(scope.TenantA);
        var reservation = await innerReservations.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "confirm-recovery-reserve", "actor", "corr");
        var reservations = new FailFirstConfirmationReservationRepository(innerReservations);
        var handler = new CreateGlobalProductDraftHandler(
            reservations,
            products,
            scope.Context(scope.TenantA),
            new TestActorContext());
        var command = new CreateGlobalProductDraftCommand(
            new ProductItemSkuMasterModels.CreateGlobalProductDraftRequest
            {
                GlobalProductName = "Pending Replay Product",
                ReservationId = reservation.Id,
                ExpectedReservationVersion = 0,
                IdempotencyKey = "confirm-recovery-create"
            });

        var pending = await handler.Handle(command, CancellationToken.None);
        var pendingReservation = await innerReservations.GetByIdAsync(reservation.Id);

        Assert.True(pending.IsSuccessful);
        Assert.Equal(202, pending.StatusCode);
        Assert.True(pending.Data!.BindingReconciliationRequired);
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, pending.Data.CodeBindingState);
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, pendingReservation!.BindingState);
        Assert.DoesNotContain(
            pendingReservation.AuditIntents,
            intent => intent.Operation == ProductAuditOperation.CodeBurned);
        Assert.NotNull(await products.GetByReservationIdAsync(reservation.Id));

        var reconciled = await handler.Handle(command, CancellationToken.None);
        var confirmedReservation = await innerReservations.GetByIdAsync(reservation.Id);

        Assert.True(reconciled.IsSuccessful);
        Assert.Equal(201, reconciled.StatusCode);
        Assert.False(reconciled.Data!.BindingReconciliationRequired);
        Assert.Equal(CodeReservationBindingState.Confirmed, reconciled.Data.CodeBindingState);
        Assert.Equal(pending.Data.GlobalProductId, reconciled.Data.GlobalProductId);
        Assert.Equal(CodeReservationBindingState.Confirmed, confirmedReservation!.BindingState);
        Assert.DoesNotContain(
            confirmedReservation.AuditIntents,
            intent => intent.Operation == ProductAuditOperation.CodeBurned);
        Assert.Equal(3, confirmedReservation.AuditIntents.Count);
    }

    [Fact]
    public async Task Persist_then_exception_with_delayed_visibility_never_burns_and_replay_confirms()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var reservations = scope.Reservations(scope.TenantA);
        var innerProducts = scope.GlobalProducts(scope.TenantA);
        var products = new PersistThenHideOnceGlobalProductRepository(innerProducts);
        var reservation = await reservations.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "persist-then-throw-reserve", "actor", "corr");
        var handler = new CreateGlobalProductDraftHandler(
            reservations,
            products,
            scope.Context(scope.TenantA),
            new TestActorContext());
        var command = new CreateGlobalProductDraftCommand(
            new ProductItemSkuMasterModels.CreateGlobalProductDraftRequest
            {
                GlobalProductName = "Confirmed Replay Product",
                ReservationId = reservation.Id,
                ExpectedReservationVersion = 0,
                IdempotencyKey = "persist-then-throw-create"
            });

        var ambiguous = await handler.Handle(command, CancellationToken.None);
        var persisted = await innerProducts.GetByReservationIdAsync(reservation.Id);
        var pending = await reservations.GetByIdAsync(reservation.Id);

        Assert.False(ambiguous.IsSuccessful);
        Assert.Equal(202, ambiguous.StatusCode);
        Assert.Contains("GLOBAL_PRODUCT_BINDING_RECONCILIATION_REQUIRED", ambiguous.Errors);
        Assert.NotNull(persisted);
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, pending!.BindingState);
        Assert.DoesNotContain(pending.AuditIntents, intent => intent.Operation == ProductAuditOperation.CodeBurned);

        var replay = await handler.Handle(command, CancellationToken.None);
        var confirmed = await reservations.GetByIdAsync(reservation.Id);

        Assert.True(replay.IsSuccessful);
        Assert.Equal(201, replay.StatusCode);
        Assert.Equal(persisted!.Id, replay.Data!.GlobalProductId);
        Assert.Equal(CodeReservationBindingState.Confirmed, confirmed!.BindingState);
        Assert.DoesNotContain(confirmed.AuditIntents, intent => intent.Operation == ProductAuditOperation.CodeBurned);
    }

    [Fact]
    public async Task Concurrent_consumes_allow_only_one_reservation_for_the_same_identity()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var repository = scope.Reservations(scope.TenantA);
        var first = await repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "identity-conflict-reserve-1", "actor", "corr");
        var second = await repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "identity-conflict-reserve-2", "actor", "corr");
        var identityId = Guid.NewGuid();

        var results = await Task.WhenAll(
            repository.ConsumeForIdentityAsync(
                first.Id, CodeBearingEntityType.GlobalProduct, identityId, 0,
                "identity-conflict-consume-1", "actor", "corr"),
            repository.ConsumeForIdentityAsync(
                second.Id, CodeBearingEntityType.GlobalProduct, identityId, 0,
                "identity-conflict-consume-2", "actor", "corr"));

        Assert.Single(results, result => result.Succeeded);
        var rejected = Assert.Single(results, result => !result.Succeeded);
        Assert.Equal("IDENTITY_RESERVATION_CONFLICT", rejected.ErrorCode);
    }

    [Fact]
    public async Task Concurrent_consumes_allow_one_tenant_wide_consume_command_only()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var repository = scope.Reservations(scope.TenantA);
        var first = await repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "command-conflict-reserve-1", "actor", "corr");
        var second = await repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "command-conflict-reserve-2", "actor", "corr");

        var results = await Task.WhenAll(
            repository.ConsumeForIdentityAsync(
                first.Id, CodeBearingEntityType.GlobalProduct, Guid.NewGuid(), 0,
                "shared-consume-command", "actor", "corr"),
            repository.ConsumeForIdentityAsync(
                second.Id, CodeBearingEntityType.GlobalProduct, Guid.NewGuid(), 0,
                "shared-consume-command", "actor", "corr"));

        Assert.Single(results, result => result.Succeeded);
        var rejected = Assert.Single(results, result => !result.Succeeded);
        Assert.Equal("IDEMPOTENCY_KEY_CONFLICT", rejected.ErrorCode);
    }

    [Fact]
    public async Task Confirmation_failure_rereads_confirmed_state_as_idempotent_success()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var innerReservations = scope.Reservations(scope.TenantA);
        var products = scope.GlobalProducts(scope.TenantA);
        var reservation = await innerReservations.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "confirmed-reread-reserve", "actor", "corr");
        var reservations = new ConfirmationFailureReservationRepository(
            innerReservations,
            scope.Database,
            scope.TenantA,
            ConfirmationFailureMode.ConfirmedButReportedFailure);
        var handler = new CreateGlobalProductDraftHandler(
            reservations, products, scope.Context(scope.TenantA), new TestActorContext());

        var response = await handler.Handle(
            new CreateGlobalProductDraftCommand(new ProductItemSkuMasterModels.CreateGlobalProductDraftRequest
            {
                GlobalProductName = "Confirmation Read Product",
                ReservationId = reservation.Id,
                ExpectedReservationVersion = 0,
                IdempotencyKey = "confirmed-reread-create"
            }),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(CodeReservationBindingState.Confirmed, response.Data!.CodeBindingState);
        Assert.False(response.Data.BindingReconciliationRequired);
    }

    [Fact]
    public async Task Confirmation_failure_rereads_burned_state_as_invariant_violation()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var innerReservations = scope.Reservations(scope.TenantA);
        var products = scope.GlobalProducts(scope.TenantA);
        var reservation = await innerReservations.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "burned-reread-reserve", "actor", "corr");
        var reservations = new ConfirmationFailureReservationRepository(
            innerReservations,
            scope.Database,
            scope.TenantA,
            ConfirmationFailureMode.BurnedInvariant);
        var handler = new CreateGlobalProductDraftHandler(
            reservations, products, scope.Context(scope.TenantA), new TestActorContext());

        var response = await handler.Handle(
            new CreateGlobalProductDraftCommand(new ProductItemSkuMasterModels.CreateGlobalProductDraftRequest
            {
                GlobalProductName = "Burned Read Product",
                ReservationId = reservation.Id,
                ExpectedReservationVersion = 0,
                IdempotencyKey = "burned-reread-create"
            }),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(500, response.StatusCode);
        Assert.Contains("GLOBAL_PRODUCT_BINDING_INVARIANT_VIOLATION", response.Errors);
        Assert.NotNull(await products.GetByReservationIdAsync(reservation.Id));
    }

    [Fact]
    public async Task Soft_deleted_reservation_is_not_returned_by_normal_idempotency_lookup()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var repository = scope.Reservations(scope.TenantA);
        var reservation = await repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "soft-delete-reserve", "actor", "corr");
        var collection = scope.Database.GetCollection<CodeReservation>("mdm_code_reservations");
        await collection.UpdateOneAsync(
            Builders<CodeReservation>.Filter.Eq(x => x.Id, reservation.Id),
            Builders<CodeReservation>.Update
                .Set(x => x.IsDeleted, true)
                .Set(x => x.DeletedAt, DateTimeOffset.UtcNow));

        Assert.Null(await repository.GetByIdAsync(reservation.Id));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "soft-delete-reserve", "actor", "corr"));
        Assert.Equal("RESERVATION_IDEMPOTENCY_KEY_TOMBSTONED", exception.Message);

        var next = await repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "soft-delete-next", "actor", "corr");
        Assert.NotEqual(reservation.ReservedCode, next.ReservedCode);
        Assert.Equal("GP-000000000002", next.ReservedCode);
    }

    [Fact]
    public async Task Soft_deleted_consumed_identity_maps_to_stable_conflict_without_payload_leak()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var repository = scope.Reservations(scope.TenantA);
        var identityId = Guid.NewGuid();
        var tombstone = await repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "identity-tombstone-reserve", "actor", "corr");
        var consumed = await repository.ConsumeForIdentityAsync(
            tombstone.Id,
            CodeBearingEntityType.GlobalProduct,
            identityId,
            0,
            "identity-tombstone-consume",
            "actor",
            "corr");
        Assert.True(consumed.Succeeded);
        await SoftDeleteReservationAsync(scope.Database, tombstone.Id);
        Assert.Null(await repository.GetByIdAsync(tombstone.Id));
        var candidate = await repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "identity-tombstone-candidate", "actor", "corr");

        var result = await repository.ConsumeForIdentityAsync(
            candidate.Id,
            CodeBearingEntityType.GlobalProduct,
            identityId,
            0,
            "identity-tombstone-new-consume",
            "actor",
            "corr");

        Assert.False(result.Succeeded);
        Assert.Equal("IDENTITY_RESERVATION_CONFLICT", result.ErrorCode);
        Assert.Equal(candidate.Id, result.Reservation!.Id);
        Assert.NotEqual(tombstone.Id, result.Reservation.Id);
        Assert.Equal(CodeReservationState.Reserved, (await repository.GetByIdAsync(candidate.Id))!.ReservationState);
    }

    [Fact]
    public async Task Soft_deleted_consume_command_maps_to_stable_idempotency_conflict_without_payload_leak()
    {
        await using var scope = await MongoTestScope.CreateAsync();
        var repository = scope.Reservations(scope.TenantA);
        var tombstone = await repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "command-tombstone-reserve", "actor", "corr");
        var consumed = await repository.ConsumeForIdentityAsync(
            tombstone.Id,
            CodeBearingEntityType.GlobalProduct,
            Guid.NewGuid(),
            0,
            "command-tombstone-consume",
            "actor",
            "corr");
        Assert.True(consumed.Succeeded);
        await SoftDeleteReservationAsync(scope.Database, tombstone.Id);
        Assert.Null(await repository.GetByIdAsync(tombstone.Id));
        var candidate = await repository.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "command-tombstone-candidate", "actor", "corr");

        var result = await repository.ConsumeForIdentityAsync(
            candidate.Id,
            CodeBearingEntityType.GlobalProduct,
            Guid.NewGuid(),
            0,
            "command-tombstone-consume",
            "actor",
            "corr");

        Assert.False(result.Succeeded);
        Assert.Equal("IDEMPOTENCY_KEY_CONFLICT", result.ErrorCode);
        Assert.Equal(candidate.Id, result.Reservation!.Id);
        Assert.NotEqual(tombstone.Id, result.Reservation.Id);
        Assert.Equal(CodeReservationState.Reserved, (await repository.GetByIdAsync(candidate.Id))!.ReservationState);
    }

    [Fact]
    public void Domain_layer_has_no_mongodb_driver_or_bson_imports()
    {
        var root = FindRepositoryRoot();
        var domainPath = Path.Combine(root, "services", "Diten.MdmService", "src", "Diten.MdmService.Domain");
        var violations = Directory.EnumerateFiles(domainPath, "*.cs", SearchOption.AllDirectories)
            .Where(file =>
            {
                var text = File.ReadAllText(file);
                return text.Contains("MongoDB.Driver", StringComparison.Ordinal)
                    || text.Contains("MongoDB.Bson", StringComparison.Ordinal);
            })
            .ToList();

        Assert.Empty(violations);
    }

    private static LocalAuditIntent CreateTestIntent(Guid tenantId, Guid aggregateId)
        => new()
        {
            IntentId = Guid.NewGuid(),
            TenantId = tenantId,
            AggregateType = AuditAggregateType.GlobalProduct,
            AggregateId = aggregateId,
            PreVersion = -1,
            PostVersion = 0,
            Operation = ProductAuditOperation.GlobalProductDraftCreated,
            ActorId = "actor",
            CorrelationId = "corr",
            CausationId = "cmd",
            CommandId = "cmd",
            Sequence = 1,
            TimestampUtc = DateTimeOffset.UtcNow,
            EvidenceHash = "HASH",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = "intent"
        };

    private static Task SoftDeleteReservationAsync(IMongoDatabase database, Guid reservationId)
        => database.GetCollection<CodeReservation>("mdm_code_reservations").UpdateOneAsync(
            Builders<CodeReservation>.Filter.Eq(x => x.Id, reservationId),
            Builders<CodeReservation>.Update
                .Set(x => x.IsDeleted, true)
                .Set(x => x.DeletedAt, DateTimeOffset.UtcNow));

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static async Task<GlobalProduct> InsertParentAsync(MongoTestScope scope, Guid tenantId)
    {
        _ = scope.GlobalProducts(tenantId);
        var parent = new GlobalProduct
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CanonicalCode = "GP-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            GlobalProductName = "Parent " + Guid.NewGuid().ToString("N"),
            GlobalProductNameNormalized = Guid.NewGuid().ToString("N"),
            CodeReservationId = Guid.NewGuid(),
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft
        };
        await scope.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(parent);
        return parent;
    }

    private static CreateFirstGskuDraftHandler CreateFirstGskuHandler(
        MongoTestScope scope,
        Guid tenantId,
        IVerifiedGskuReferenceResolver resolver)
        => new(
            scope.GlobalProducts(tenantId),
            new ProductDefinitionRevisionRepository(scope.Database, scope.Context(tenantId)),
            new GskuRepository(scope.Database, scope.Context(tenantId)),
            scope.Reservations(tenantId),
            resolver,
            scope.Context(tenantId),
            new TestActorContext());

    private sealed class VerifiedResolver(bool succeeds = true) : IVerifiedGskuReferenceResolver
    {
        private int _callCount;
        public int CallCount => _callCount;

        public Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(
            string packApplicabilityValueCode,
            string uomValueCode,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            if (!succeeds)
            {
                return Task.FromResult(VerifiedGskuReferenceResolveResult.Fail(503, "REFERENCE_DATA_CONTRACT_UNAVAILABLE"));
            }

            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(VerifiedGskuReferenceResolveResult.Success([
                new VerifiedGskuReferenceSelection("pack-applicability", packApplicabilityValueCode,
                    Guid.NewGuid(), 1, "LATEST", now, false, true),
                new VerifiedGskuReferenceSelection("uom", uomValueCode,
                    Guid.NewGuid(), 1, "LATEST", now, false, true)
            ]));
        }
    }

    private sealed class TestActorContext : IProductIdentityActorContext
    {
        public string ActorId => "test-actor";
    }

    private sealed class ThrowingGlobalProductRepository : IGlobalProductRepository
    {
        public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<GlobalProduct?>(null);

        public Task<GlobalProduct?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default)
            => Task.FromResult<GlobalProduct?>(null);

        public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<GlobalProductPage> GetPageAsync(
            int pageNumber,
            int pageSize,
            string? normalizedSearch,
            ProductIdentityLifecycleStatus? lifecycleStatus,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new GlobalProductPage([], 0));

        public Task<GlobalProductCreateResult> CreateDraftAsync(
            GlobalProduct globalProduct,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated identity write failure.");
    }

    private sealed class PersistThenHideOnceGlobalProductRepository : IGlobalProductRepository
    {
        private readonly IGlobalProductRepository _inner;
        private bool _throwAfterFirstPersist = true;
        private bool _hideFirstLookup = true;

        public PersistThenHideOnceGlobalProductRepository(IGlobalProductRepository inner)
        {
            _inner = inner;
        }

        public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(id, cancellationToken);

        public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken = default)
            => _inner.NameExistsAsync(normalizedName, cancellationToken);

        public Task<GlobalProductPage> GetPageAsync(
            int pageNumber,
            int pageSize,
            string? normalizedSearch,
            ProductIdentityLifecycleStatus? lifecycleStatus,
            CancellationToken cancellationToken = default)
            => _inner.GetPageAsync(pageNumber, pageSize, normalizedSearch, lifecycleStatus, cancellationToken);

        public async Task<GlobalProduct?> GetByReservationIdAsync(
            Guid reservationId,
            CancellationToken cancellationToken = default)
        {
            if (_hideFirstLookup)
            {
                _hideFirstLookup = false;
                return null;
            }

            return await _inner.GetByReservationIdAsync(reservationId, cancellationToken);
        }

        public async Task<GlobalProductCreateResult> CreateDraftAsync(
            GlobalProduct globalProduct,
            CancellationToken cancellationToken = default)
        {
            var result = await _inner.CreateDraftAsync(globalProduct, cancellationToken);
            if (_throwAfterFirstPersist)
            {
                _throwAfterFirstPersist = false;
                throw new InvalidOperationException("Simulated ambiguous acknowledgement after persistence.");
            }

            return result;
        }
    }

    private sealed class FailFirstConfirmationReservationRepository : ICodeReservationRepository
    {
        private readonly ICodeReservationRepository _inner;
        private int _confirmationAttempts;

        public FailFirstConfirmationReservationRepository(ICodeReservationRepository inner)
        {
            _inner = inner;
        }

        public Task<CodeReservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(id, cancellationToken);

        public Task<CodeReservation> ReserveAsync(
            CodeBearingEntityType entityType,
            string idempotencyKey,
            string actorId,
            string correlationId,
            CancellationToken cancellationToken = default)
            => _inner.ReserveAsync(entityType, idempotencyKey, actorId, correlationId, cancellationToken);

        public Task<ReservationOperationResult> ConsumeForIdentityAsync(
            Guid reservationId,
            CodeBearingEntityType expectedEntityType,
            Guid identityId,
            int expectedVersion,
            string idempotencyKey,
            string actorId,
            string correlationId,
            CancellationToken cancellationToken = default)
            => _inner.ConsumeForIdentityAsync(
                reservationId,
                expectedEntityType,
                identityId,
                expectedVersion,
                idempotencyKey,
                actorId,
                correlationId,
                cancellationToken);

        public async Task<ReservationOperationResult> ConfirmIdentityBindingAsync(
            Guid reservationId,
            Guid identityId,
            int expectedVersion,
            string idempotencyKey,
            string actorId,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _confirmationAttempts) == 1)
            {
                return new(false, await _inner.GetByIdAsync(reservationId, cancellationToken),
                    "CODE_RESERVATION_CONFIRMATION_PENDING");
            }

            return await _inner.ConfirmIdentityBindingAsync(
                reservationId,
                identityId,
                expectedVersion,
                idempotencyKey,
                actorId,
                correlationId,
                cancellationToken);
        }

    }

    private enum ConfirmationFailureMode
    {
        ConfirmedButReportedFailure,
        BurnedInvariant
    }

    private sealed class ConfirmationFailureReservationRepository : ICodeReservationRepository
    {
        private readonly ICodeReservationRepository _inner;
        private readonly IMongoCollection<CodeReservation> _collection;
        private readonly Guid _tenantId;
        private readonly ConfirmationFailureMode _mode;

        public ConfirmationFailureReservationRepository(
            ICodeReservationRepository inner,
            IMongoDatabase database,
            Guid tenantId,
            ConfirmationFailureMode mode)
        {
            _inner = inner;
            _collection = database.GetCollection<CodeReservation>("mdm_code_reservations");
            _tenantId = tenantId;
            _mode = mode;
        }

        public Task<CodeReservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(id, cancellationToken);

        public Task<CodeReservation> ReserveAsync(
            CodeBearingEntityType entityType,
            string idempotencyKey,
            string actorId,
            string correlationId,
            CancellationToken cancellationToken = default)
            => _inner.ReserveAsync(entityType, idempotencyKey, actorId, correlationId, cancellationToken);

        public Task<ReservationOperationResult> ConsumeForIdentityAsync(
            Guid reservationId,
            CodeBearingEntityType expectedEntityType,
            Guid identityId,
            int expectedVersion,
            string idempotencyKey,
            string actorId,
            string correlationId,
            CancellationToken cancellationToken = default)
            => _inner.ConsumeForIdentityAsync(
                reservationId, expectedEntityType, identityId, expectedVersion,
                idempotencyKey, actorId, correlationId, cancellationToken);

        public async Task<ReservationOperationResult> ConfirmIdentityBindingAsync(
            Guid reservationId,
            Guid identityId,
            int expectedVersion,
            string idempotencyKey,
            string actorId,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            if (_mode == ConfirmationFailureMode.ConfirmedButReportedFailure)
            {
                await _inner.ConfirmIdentityBindingAsync(
                    reservationId, identityId, expectedVersion, idempotencyKey,
                    actorId, correlationId, cancellationToken);
            }
            else
            {
                await _collection.UpdateOneAsync(
                    Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
                    & Builders<CodeReservation>.Filter.Eq(x => x.Id, reservationId),
                    Builders<CodeReservation>.Update
                        .Set(x => x.BindingState, CodeReservationBindingState.Burned)
                        .Inc(x => x.Version, 1),
                    cancellationToken: cancellationToken);
            }

            return new(false, await _inner.GetByIdAsync(reservationId, cancellationToken),
                "SIMULATED_CONFIRMATION_FAILURE");
        }
    }

    private sealed class MongoTestScope : IAsyncDisposable
    {
        private readonly IMongoClient _client;

        private MongoTestScope(IMongoClient client, IMongoDatabase database, string databaseName)
        {
            _client = client;
            Database = database;
            DatabaseName = databaseName;
        }

        public Guid TenantA { get; } = Guid.NewGuid();
        public Guid TenantB { get; } = Guid.NewGuid();
        public IMongoDatabase Database { get; }
        private string DatabaseName { get; }

        public static async Task<MongoTestScope> CreateAsync()
        {
            var uri = Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? "mongodb://localhost:27017";
            var settings = MongoClientSettings.FromConnectionString(uri);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
#pragma warning disable CS0618 // Driver 2.x requires client-level Standard representation to match production wiring.
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var databaseName = "DitenERP_MOD0290_Test_" + Guid.NewGuid().ToString("N");
            var database = client.GetDatabase(databaseName);
            await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));
            return new MongoTestScope(client, database, databaseName);
        }

        public TenantContext Context(Guid tenantId)
        {
            var context = new TenantContext();
            context.SetTenant(tenantId);
            return context;
        }

        public CodeReservationRepository Reservations(Guid tenantId)
            => new(Database, Context(tenantId));

        public GlobalProductRepository GlobalProducts(Guid tenantId)
            => new(Database, Context(tenantId));

        public async ValueTask DisposeAsync()
            => await _client.DropDatabaseAsync(DatabaseName);
    }
}
