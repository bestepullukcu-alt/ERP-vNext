using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;
using Diten.MdmService.Application.Tests.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using Diten.Shared.Core;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 — the four stock-behaviour commands through their real handlers and the real
/// store, on the test-owned replica set: O-8 (first fill / lock / Draft free), completeness, FG ≤ GSKU, GTIN, the
/// operation block (CT decision A), legacy documents without the fields (R2), the first-fill race (R3) and the GTIN
/// unique index (R4).
/// </summary>
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class StockBehaviourMongoTests : IClassFixture<AuditIntentTemporalMongoFixture>, IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IMongoDatabase _database;

    public StockBehaviourMongoTests(AuditIntentTemporalMongoFixture mongo)
    {
        var settings = MongoClientSettings.FromConnectionString(mongo.ReplicaConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _database = new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
    }

    private IMongoCollection<Gsku> Gskus => _database.GetCollection<Gsku>("mdm_gskus");
    private IMongoCollection<Lsku> Lskus => _database.GetCollection<Lsku>("mdm_lskus");
    private IMongoCollection<FinishedGood> FinishedGoods => _database.GetCollection<FinishedGood>("mdm_finished_goods");

    public async Task InitializeAsync()
    {
        // Test-owned replica set only — never the shared dev Mongo on 27017.
        Assert.All(_database.Client.Settings.Servers, server => Assert.NotEqual(27017, server.Port));
        await ProductStockBehaviourRepository.EnsureGtinIndexAsync(_database);
    }

    public async Task DisposeAsync()
    {
        foreach (var name in new[] { "mdm_global_products", "mdm_product_definition_revisions", "mdm_gskus", "mdm_lskus",
                     "mdm_finished_goods", FirstGskuIdentityWorkflowOperationRepository.CollectionName,
                     LskuIdentityWorkflowOperationRepository.CollectionName,
                     FinishedGoodIdentityWorkflowOperationRepository.CollectionName })
            await _database.GetCollection<BsonDocument>(name).DeleteManyAsync(new BsonDocument("TenantId", new BsonBinaryData(_tenantId, GuidRepresentation.Standard)));
    }

    // ---------------------------------------------------------------- O-8

    [Fact]
    public async Task A_draft_gsku_takes_every_field_and_moves_its_version_and_the_pack_edge_is_never_stored()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft, packQuantity: 30m, packUom: "C62");

        var response = await SetGsku(gsku, baseUom: "C62", shelfLife: 730, storage: ["COOL_2_8", "PROTECT_FROM_LIGHT"],
            lot: true, stockable: true, conversions: [Edge("XCS", "XBX", "12")]);

        Assert.True(response.IsSuccessful, string.Join(",", response.Errors ?? []));
        var stored = await Gskus.Find(x => x.Id == gsku.Id).SingleAsync();
        Assert.Equal(gsku.Version + 1, stored.Version);
        Assert.Equal(("C62", 730, true, true), (stored.BaseUomCode, stored.ShelfLifeDays, stored.LotControlled, stored.Stockable));
        Assert.Equal(["COOL_2_8", "PROTECT_FROM_LIGHT"], stored.StorageConditionCodes);
        var conversion = Assert.Single(stored.UomConversions!);
        Assert.Equal(("XCS", "XBX", "12", "1"), (conversion.FromUom, conversion.ToUom, conversion.Numerator, conversion.Denominator));
        Assert.Contains(stored.StockReferenceSelections!, proof => proof is { SetCode: "item-uom", ValueCode: "C62" });
        Assert.Contains(stored.StockReferenceSelections!, proof => proof is { SetCode: "storage-condition", ValueCode: "COOL_2_8" });
    }

    [Fact]
    public async Task On_an_approved_gsku_the_first_fill_is_free_and_changing_a_filled_value_is_locked()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.IdentityApproved);

        var first = await SetGsku(gsku, shelfLife: 730);
        Assert.True(first.IsSuccessful, string.Join(",", first.Errors ?? []));
        var change = await SetGsku(gsku, version: first.Data!.Version, shelfLife: 365);
        var same = await SetGsku(gsku, version: first.Data.Version, shelfLife: 730);

        Assert.Equal(409, change.StatusCode);
        Assert.Contains(StockBehaviourStoreCodes.FieldLocked, change.Errors!);
        Assert.True(same.IsSuccessful);
        Assert.False(same.Data!.Changed);
        var stored = await Gskus.Find(x => x.Id == gsku.Id).SingleAsync();
        Assert.Equal((730, first.Data.Version), (stored.ShelfLifeDays, stored.Version));
    }

    [Fact]
    public async Task On_a_draft_gsku_a_filled_value_may_change()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        var first = await SetGsku(gsku, shelfLife: 730);
        var change = await SetGsku(gsku, version: first.Data!.Version, shelfLife: 365);

        Assert.True(change.IsSuccessful, string.Join(",", change.Errors ?? []));
        Assert.Equal(365, (await Gskus.Find(x => x.Id == gsku.Id).SingleAsync()).ShelfLifeDays);
    }

    [Fact]
    public async Task A_legacy_gsku_without_any_stock_field_reads_empty_and_takes_its_first_fill()
    {
        // R2: a document written before S3 carries none of the fields — not null, ABSENT. The first fill's condition is
        // Exists(false) | Eq(null), never Eq(false) / Size(0).
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        await Gskus.UpdateOneAsync(x => x.Id == gsku.Id, Builders<Gsku>.Update.Combine(
            new[] { "BaseUomCode", "UomConversions", "ShelfLifeDays", "StorageConditionCodes", "LotControlled", "Stockable",
                    "StockBehaviourCommand", "StockReferenceSelections", "IdentitySubmitReservations" }
                .Select(field => Builders<Gsku>.Update.Unset(field))));
        var raw = await _database.GetCollection<BsonDocument>("mdm_gskus")
            .Find(new BsonDocument("_id", new BsonBinaryData(gsku.Id, GuidRepresentation.Standard))).SingleAsync();
        Assert.False(raw.Contains("ShelfLifeDays") || raw.Contains("Stockable") || raw.Contains("LotControlled"));

        var read = await Gskus.Find(x => x.Id == gsku.Id).SingleAsync();
        Assert.Null(read.ShelfLifeDays);
        Assert.Null(read.Stockable);
        Assert.Null(read.StorageConditionCodes);

        var response = await SetGsku(gsku, baseUom: "C62", shelfLife: 730, storage: ["AMBIENT_15_25"], lot: false);
        Assert.True(response.IsSuccessful, string.Join(",", response.Errors ?? []));
        var stored = await Gskus.Find(x => x.Id == gsku.Id).SingleAsync();
        Assert.Equal((730, false, gsku.Version + 1), (stored.ShelfLifeDays, stored.LotControlled, stored.Version));
    }

    [Fact]
    public async Task A_legacy_finished_good_without_any_stock_field_takes_its_first_fill()
    {
        var (gsku, finishedGood) = await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        await FinishedGoods.UpdateOneAsync(x => x.Id == finishedGood.Id, Builders<FinishedGood>.Update.Combine(
            new[] { "UomConversions", "ShelfLifeDays", "StorageConditionCodes", "LotControlled", "SerialControlled", "Stockable",
                    "Gtin", "StockBehaviourCommand", "GtinCommand", "StockReferenceSelections", "DraftCancellation" }
                .Select(field => Builders<FinishedGood>.Update.Unset(field))));

        var response = await SetFinishedGood(finishedGood, lot: true, serial: true);
        var gtin = await AssignGtin(finishedGood, response.Data!.Version, "04006381333931");

        Assert.True(gtin.IsSuccessful, string.Join(",", gtin.Errors ?? []));
        var stored = await FinishedGoods.Find(x => x.Id == finishedGood.Id).SingleAsync();
        Assert.Equal((true, true, "04006381333931"), (stored.LotControlled, stored.SerialControlled, stored.Gtin));
        Assert.NotNull(gsku);
    }

    [Fact]
    public async Task Two_first_fills_at_once_exactly_one_wins_and_the_other_gets_a_named_409()
    {
        // R3: both commands read the empty field, both reach the store together (barrier); the first fill is written
        // only while the field is still empty, so the second matches nothing and is told why.
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var gate = new BarrierStore(new ProductStockBehaviourRepository(_database, Tenant()), 2);

        var results = await Task.WhenAll(
            SetGsku(gsku, shelfLife: 730, store: gate),
            SetGsku(gsku, shelfLife: 365, store: gate));

        Assert.Single(results, result => result.IsSuccessful);
        var loser = Assert.Single(results, result => !result.IsSuccessful);
        Assert.Equal(409, loser.StatusCode);
        Assert.Contains(loser.Errors!.Single(),
            new[] { StockBehaviourStoreCodes.FieldLocked, StockBehaviourStoreCodes.ConcurrencyConflict });
        var stored = await Gskus.Find(x => x.Id == gsku.Id).SingleAsync();
        Assert.Equal(gsku.Version + 1, stored.Version);
    }

    [Fact]
    public async Task A_stock_edit_and_a_draft_edit_at_once_exactly_one_wins()
    {
        // CT test (3): the stock edit moves the version, so a draft edit that read the same version falls in its CAS.
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        var gate = new BarrierStore(new ProductStockBehaviourRepository(_database, Tenant()), 2);
        var draft = await Gskus.Find(x => x.Id == gsku.Id).SingleAsync();
        draft.PackQuantity = 60m;
        draft.AuditIntents.Add(Intent(draft, ProductAuditOperation.GskuDraftUpdated, draft.Version));

        var stock = SetGsku(gsku, lot: true, store: gate);
        var edit = Task.Run(async () =>
        {
            await gate.ArriveAsync();
            return await new GskuRepository(_database, Tenant()).UpdateDraftAsync(draft, gsku.Version);
        });
        var stockResult = await stock;
        var editResult = await edit;

        Assert.True(stockResult.IsSuccessful ^ editResult.Succeeded,
            $"stock={stockResult.IsSuccessful}:{string.Join(",", stockResult.Errors ?? [])} edit={editResult.Succeeded}:{editResult.ErrorCode}");
        Assert.Equal(gsku.Version + 1, (await Gskus.Find(x => x.Id == gsku.Id).SingleAsync()).Version);
    }

    // ---------------------------------------------------------------- operation block (CT decision A)

    [Fact]
    public async Task An_open_identity_submit_blocks_the_stock_edit_by_name_and_the_record_does_not_move()
    {
        // CT test (1) — and the sabotage anchor "no open operation".
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        await SeedGskuIdentityOperationAsync(gsku, FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted);

        var response = await SetGsku(gsku, lot: true);

        Assert.Equal(409, response.StatusCode);
        Assert.Contains(StockBehaviourStoreCodes.BlockedByOperation, response.Errors!);
        var stored = await Gskus.Find(x => x.Id == gsku.Id).SingleAsync();
        Assert.Equal((gsku.Version, (bool?)null), (stored.Version, stored.LotControlled));
    }

    [Fact]
    public async Task Once_the_identity_operation_is_finished_the_stock_edit_goes_through()
    {
        // CT test (2).
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        var operationId = await SeedGskuIdentityOperationAsync(gsku, FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted);
        Assert.Equal(409, (await SetGsku(gsku, lot: true)).StatusCode);

        await _database.GetCollection<FirstGskuIdentityWorkflowOperation>(FirstGskuIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(x => x.OperationId == operationId,
                Builders<FirstGskuIdentityWorkflowOperation>.Update.Set(x => x.Checkpoint, FirstGskuIdentityWorkflowCheckpoint.Completed));
        var response = await SetGsku(gsku, lot: true);

        Assert.True(response.IsSuccessful, string.Join(",", response.Errors ?? []));
    }

    [Fact]
    public async Task A_stock_edit_before_the_submit_is_carried_through_submit_and_approval()
    {
        // CT test (4): the stock edit moves the draft to v+1; the submit is made of v+1 and its decision applies.
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        var revision = await _database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .Find(x => x.Id == gsku.ProductDefinitionRevisionId).SingleAsync();
        var edited = await SetGsku(gsku, baseUom: "C62", shelfLife: 730, lot: true);
        Assert.True(edited.IsSuccessful, string.Join(",", edited.Errors ?? []));
        var version = edited.Data!.Version;

        var gskus = new GskuRepository(_database, Tenant());
        var binding = Binding(revision.Id, gsku.Id);
        var current = await Gskus.Find(x => x.Id == gsku.Id).SingleAsync();
        var pending = await gskus.MarkIdentityPendingAsync(gsku.Id, version, binding,
            Intent(current, ProductAuditOperation.GskuIdentitySubmitted, version));
        Assert.True(pending.Succeeded, pending.ErrorCode);
        binding.TerminalDecision = Decision(binding);
        var approved = await gskus.ApproveIdentityAsync(gsku.Id, version + 1, binding,
            Intent(pending.Aggregate!, ProductAuditOperation.GskuIdentityApproved, version + 1));

        Assert.True(approved.Succeeded, approved.ErrorCode);
        var stored = await Gskus.Find(x => x.Id == gsku.Id).SingleAsync();
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, stored.LifecycleStatus);
        Assert.Equal(("C62", 730, true), (stored.BaseUomCode, stored.ShelfLifeDays, stored.LotControlled));
    }

    [Theory]
    [InlineData(ProductIdentityLifecycleStatus.PendingIdentityApproval)]
    [InlineData(ProductIdentityLifecycleStatus.Retired)]
    public async Task A_pending_or_retired_record_is_refused_by_name(ProductIdentityLifecycleStatus status)
    {
        var gsku = await SeedGskuAsync(status);
        var response = await SetGsku(gsku, lot: true);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains(StockBehaviourStoreCodes.BlockedByOperation, response.Errors!);
    }

    [Fact]
    public async Task A_gsku_under_a_lifecycle_operation_is_refused_by_name()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        await Gskus.UpdateOneAsync(x => x.Id == gsku.Id, Builders<Gsku>.Update.Set(x => x.ActiveLifecycleOperation,
            new GskuActiveLifecycleOperationBinding(GskuLifecycleOperationKind.Correction, Guid.NewGuid(), gsku.Version)));
        var response = await SetGsku(gsku, lot: true);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains(StockBehaviourStoreCodes.BlockedByOperation, response.Errors!);
    }

    [Fact]
    public async Task An_lsku_under_a_submit_fence_or_an_unfinished_identity_operation_is_refused_by_name()
    {
        var (_, lsku) = await SeedLskuAsync(ProductIdentityLifecycleStatus.Draft);
        await Lskus.UpdateOneAsync(x => x.Id == lsku.Id, Builders<Lsku>.Update.Set(x => x.ActiveLifecycleOperation,
            new LskuActiveLifecycleOperationBinding(LskuLifecycleOperationKind.IdentitySubmit, Guid.NewGuid(), lsku.Version)));
        Assert.Contains(StockBehaviourStoreCodes.BlockedByOperation, (await SetLsku(lsku, serial: true)).Errors!);

        var (_, other) = await SeedLskuAsync(ProductIdentityLifecycleStatus.Draft);
        await _database.GetCollection<LskuIdentityWorkflowOperation>(LskuIdentityWorkflowOperationRepository.CollectionName)
            .InsertOneAsync(new LskuIdentityWorkflowOperation
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, LskuId = other.Id, GskuId = other.GskuId,
                ExpectedLskuVersion = other.Version, Checkpoint = LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
            });
        Assert.Contains(StockBehaviourStoreCodes.BlockedByOperation, (await SetLsku(other, serial: true)).Errors!);
    }

    [Fact]
    public async Task A_finished_good_with_an_unfinished_identity_operation_is_refused_by_name()
    {
        var (_, finishedGood) = await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.Draft);
        await _database.GetCollection<BsonDocument>(FinishedGoodIdentityWorkflowOperationRepository.CollectionName)
            .InsertOneAsync(new BsonDocument
            {
                { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "TenantId", new BsonBinaryData(_tenantId, GuidRepresentation.Standard) },
                { "IsDeleted", false },
                { "FinishedGoodId", new BsonBinaryData(finishedGood.Id, GuidRepresentation.Standard) },
                { "ExpectedFinishedGoodVersion", finishedGood.Version },
                { "Checkpoint", (int)FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown }
            });

        var response = await SetFinishedGood(finishedGood, lot: true);

        Assert.Equal(409, response.StatusCode);
        Assert.Contains(StockBehaviourStoreCodes.BlockedByOperation, response.Errors!);
    }

    // ---------------------------------------------------------------- completeness, FG ≤ GSKU, units, GTIN

    [Fact]
    public async Task Stockable_true_with_a_required_field_missing_is_refused_and_nothing_is_written()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        var response = await SetGsku(gsku, baseUom: "C62", shelfLife: 730, lot: true, stockable: true);

        Assert.Equal(422, response.StatusCode);
        Assert.Contains(StockBehaviourRules.Incomplete, response.Errors!);
        Assert.Equal(gsku.Version, (await Gskus.Find(x => x.Id == gsku.Id).SingleAsync()).Version);
    }

    [Fact]
    public async Task A_finished_good_shelf_life_longer_than_its_gsku_is_refused_by_name()
    {
        var (gsku, finishedGood) = await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.Draft);
        var parent = await SetGsku(gsku, shelfLife: 730);
        Assert.True(parent.IsSuccessful, string.Join(",", parent.Errors ?? []));

        var longer = await SetFinishedGood(finishedGood, shelfLife: 731);
        var equal = await SetFinishedGood(finishedGood, shelfLife: 730);

        Assert.Equal(422, longer.StatusCode);
        Assert.Contains(StockBehaviourRules.ShelfLifeExceedsGsku, longer.Errors!);
        Assert.True(equal.IsSuccessful, string.Join(",", equal.Errors ?? []));
    }

    [Fact]
    public async Task A_finished_good_own_shelf_life_waits_for_the_gsku_one()
    {
        var (_, finishedGood) = await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.Draft);
        var response = await SetFinishedGood(finishedGood, shelfLife: 100);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains(StockBehaviourErrors.GskuShelfLifeMissing, response.Errors!);
    }

    [Fact]
    public async Task A_complete_finished_good_becomes_stockable_with_its_gsku_base_unit_and_shelf_life()
    {
        var (gsku, finishedGood) = await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.Draft);
        Assert.True((await SetGsku(gsku, baseUom: "C62", shelfLife: 730)).IsSuccessful);
        var gtin = await AssignGtin(finishedGood, finishedGood.Version, "00012345678905");
        Assert.True(gtin.IsSuccessful, string.Join(",", gtin.Errors ?? []));

        var stockable = await SetFinishedGood(finishedGood, version: gtin.Data!.Version,
            conversions: [Edge("XCS", "XBX", "12")], storage: ["COOL_2_8"], lot: true, serial: true, stockable: true);

        Assert.True(stockable.IsSuccessful, string.Join(",", stockable.Errors ?? []));
        var stored = await FinishedGoods.Find(x => x.Id == finishedGood.Id).SingleAsync();
        Assert.True(stored.Stockable);
        Assert.Null(stored.ShelfLifeDays); // D-2: read from the GSKU, not copied.
    }

    [Fact]
    public async Task A_finished_good_without_its_gtin_cannot_be_stockable()
    {
        var (gsku, finishedGood) = await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.Draft);
        Assert.True((await SetGsku(gsku, baseUom: "C62", shelfLife: 730)).IsSuccessful);
        var response = await SetFinishedGood(finishedGood, conversions: [Edge("XCS", "XBX", "12")], storage: ["COOL_2_8"],
            lot: true, serial: true, stockable: true);
        Assert.Equal(422, response.StatusCode);
        Assert.Contains(StockBehaviourRules.Incomplete, response.Errors!);
    }

    [Fact]
    public async Task A_conversion_with_a_zero_numerator_is_refused_by_name()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        var response = await SetGsku(gsku, baseUom: "C62", conversions: [Edge("XCS", "XBX", "0")]);
        Assert.Equal(422, response.StatusCode);
        Assert.Contains(StockBehaviourRules.ConversionInvalid, response.Errors!);
    }

    [Fact]
    public async Task A_conversion_re_entering_the_derived_pack_edge_is_a_duplicate_pair()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft, packQuantity: 30m, packUom: "C62");
        var response = await SetGsku(gsku, baseUom: "C62", conversions: [Edge("C62", "XBX", "1", "30")]);
        Assert.Equal(422, response.StatusCode);
        Assert.Contains(StockBehaviourRules.ConversionInvalid, response.Errors!);
    }

    [Fact]
    public async Task A_pack_unit_outside_item_uom_is_refused_by_its_own_name()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft, packQuantity: 5m, packUom: "EA");
        var response = await SetGsku(gsku, baseUom: "C62");
        Assert.Equal(422, response.StatusCode);
        Assert.Contains(StockBehaviourRules.PackUomUnknown, response.Errors!);
    }

    [Fact]
    public async Task Two_temperature_conditions_are_refused_by_the_lists_own_attribute()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        var response = await SetGsku(gsku, storage: ["COOL_2_8", "FROZEN_BELOW_MINUS_20"]);
        Assert.Equal(422, response.StatusCode);
        Assert.Contains(StockBehaviourRules.StorageConditionConflict, response.Errors!);
    }

    [Fact]
    public async Task Without_the_item_lists_a_code_edit_is_a_named_503_and_a_flag_edit_still_works()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        var unavailable = new Infrastructure.ReferenceData.UnavailableItemReferenceResolver();

        var codes = await SetGsku(gsku, baseUom: "C62", references: unavailable);
        var flag = await SetGsku(gsku, lot: true, references: unavailable);

        Assert.Equal(503, codes.StatusCode);
        Assert.Contains(StockBehaviourErrors.ReferenceListUnavailable, codes.Errors!);
        Assert.True(flag.IsSuccessful, string.Join(",", flag.Errors ?? []));
    }

    [Fact]
    public async Task A_gtin_with_a_wrong_check_digit_is_refused_by_name()
    {
        var (_, finishedGood) = await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.Draft);
        var response = await AssignGtin(finishedGood, finishedGood.Version, "04006381333932");
        Assert.Equal(422, response.StatusCode);
        Assert.Contains(StockBehaviourRules.GtinInvalid, response.Errors!);
    }

    [Fact]
    public async Task A_gtin_held_by_another_live_finished_good_is_refused_and_a_retired_ones_is_free()
    {
        var (_, first) = await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var (_, second) = await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        Assert.True((await AssignGtin(first, first.Version, "04006381333931")).IsSuccessful);

        var taken = await AssignGtin(second, second.Version, "04006381333931");
        Assert.Equal(409, taken.StatusCode);
        Assert.Contains(StockBehaviourStoreCodes.GtinConflict, taken.Errors!);

        await FinishedGoods.UpdateOneAsync(x => x.Id == first.Id,
            Builders<FinishedGood>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired));
        Assert.True((await AssignGtin(second, second.Version, "04006381333931")).IsSuccessful);
    }

    [Fact]
    public async Task The_unique_index_refuses_a_gtin_race_the_pre_check_cannot_see_and_null_gtins_never_collide()
    {
        // R4: the store write bypassing the pre-check (the race) meets the partial unique index.
        var (_, first) = await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.Draft);
        var (_, second) = await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.Draft);
        await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.Draft); // Gtin null, written as null by the serializer.
        var store = new ProductStockBehaviourRepository(_database, Tenant());
        Assert.True((await store.WriteFinishedGoodAsync(GtinWrite(first, "00012345678905"))).Succeeded);

        var raced = await store.WriteFinishedGoodAsync(GtinWrite(second, "00012345678905"));

        Assert.False(raced.Succeeded);
        Assert.Equal(StockBehaviourStoreCodes.GtinConflict, raced.ErrorCode);
        var index = (await (await FinishedGoods.Indexes.ListAsync()).ToListAsync())
            .Single(item => item["name"] == ProductStockBehaviourRepository.GtinIndexName);
        Assert.True(index["unique"].AsBoolean);
        Assert.DoesNotContain("$ne", index["partialFilterExpression"].ToJson());
    }

    // ---------------------------------------------------------------- LSKU, permission, scope, replay

    [Fact]
    public async Task An_lsku_stores_only_its_own_serial_control()
    {
        var (_, lsku) = await SeedLskuAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var response = await SetLsku(lsku, serial: true);
        var change = await SetLsku(lsku, version: response.Data!.Version, serial: false);

        Assert.True(response.IsSuccessful);
        Assert.Contains(StockBehaviourStoreCodes.FieldLocked, change.Errors!);
        Assert.True((await Lskus.Find(x => x.Id == lsku.Id).SingleAsync()).SerialControlled);
    }

    [Fact]
    public async Task Each_command_asks_its_own_temporary_key_and_a_caller_without_it_gets_403()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        var (_, lsku) = await SeedLskuAsync(ProductIdentityLifecycleStatus.Draft);
        var (_, finishedGood) = await SeedFinishedGoodAsync(ProductIdentityLifecycleStatus.Draft);
        Assert.Equal("mdm.gskus.update", StockBehaviourPermissions.EditGsku);
        Assert.Equal("mdm.lskus.create", StockBehaviourPermissions.EditLsku);
        Assert.Equal("mdm.finished-goods.create", StockBehaviourPermissions.EditFinishedGood);

        // Each command with every OTHER stock key but not its own: refused.
        Assert.Equal(403, (await SetGsku(gsku, lot: true, actor: new Maker(StockBehaviourPermissions.EditLsku, StockBehaviourPermissions.EditFinishedGood))).StatusCode);
        Assert.Equal(403, (await SetLsku(lsku, serial: true, actor: new Maker(StockBehaviourPermissions.EditGsku, StockBehaviourPermissions.EditFinishedGood))).StatusCode);
        Assert.Equal(403, (await SetFinishedGood(finishedGood, lot: true, actor: new Maker(StockBehaviourPermissions.EditGsku, StockBehaviourPermissions.EditLsku))).StatusCode);
        Assert.Equal(403, (await AssignGtin(finishedGood, finishedGood.Version, "00012345678905", actor: new Maker(StockBehaviourPermissions.EditGsku))).StatusCode);

        // With only its own key: through.
        Assert.True((await SetGsku(gsku, lot: true, actor: new Maker(StockBehaviourPermissions.EditGsku))).IsSuccessful);
        Assert.True((await SetLsku(lsku, serial: true, actor: new Maker(StockBehaviourPermissions.EditLsku))).IsSuccessful);
        var fg = await SetFinishedGood(finishedGood, lot: true, actor: new Maker(StockBehaviourPermissions.EditFinishedGood));
        Assert.True(fg.IsSuccessful);
        Assert.True((await AssignGtin(finishedGood, fg.Data!.Version, "00012345678905", actor: new Maker(StockBehaviourPermissions.EditFinishedGood))).IsSuccessful);
    }

    [Fact]
    public async Task In_an_enforced_tenant_a_record_outside_the_callers_scope_is_not_found()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        var response = await SetGsku(gsku, lot: true, enforced: true);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(gsku.Version, (await Gskus.Find(x => x.Id == gsku.Id).SingleAsync()).Version);
    }

    [Fact]
    public async Task The_same_command_again_is_a_replay_and_the_same_key_with_other_content_is_refused()
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        var operationId = Guid.NewGuid();
        var first = await SetGsku(gsku, shelfLife: 730, operationId: operationId);
        var replay = await SetGsku(gsku, shelfLife: 730, operationId: operationId);
        var reused = await SetGsku(gsku, shelfLife: 365, operationId: operationId);

        Assert.True(first.IsSuccessful);
        Assert.True(replay.Data!.IsReplay);
        Assert.Equal(first.Data!.Version, replay.Data.Version);
        Assert.Equal(409, reused.StatusCode);
        Assert.Contains(StockBehaviourStoreCodes.IdempotencyConflict, reused.Errors!);
        Assert.Equal(first.Data.Version, (await Gskus.Find(x => x.Id == gsku.Id).SingleAsync()).Version);
    }

    // ---------------------------------------------------------------- plumbing

    private static ItemUomConversion Edge(string from, string to, string numerator, string denominator = "1") =>
        new() { FromUom = from, ToUom = to, Numerator = numerator, Denominator = denominator };

    private TenantContext Tenant()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(_tenantId);
        return tenant;
    }

    private StockBehaviourContext Context(IProductIdentityLifecycleActorContext? actor, bool enforced)
    {
        var tenant = Tenant();
        var scope = enforced
            ? ProductLegalEntityScopeTestFixture.Enforced(tenant, [Guid.NewGuid()], new NoPolicies())
            : ProductLegalEntityScopeTestFixture.Preparation(tenant);
        return new StockBehaviourContext(tenant,
            actor ?? new Maker(StockBehaviourPermissions.EditGsku, StockBehaviourPermissions.EditLsku, StockBehaviourPermissions.EditFinishedGood),
            new GskuRepository(_database, tenant), new ProductDefinitionRevisionRepository(_database, tenant),
            new GlobalProductRepository(_database, tenant), scope.Rollouts, scope.Policies, scope.Candidates, TimeProvider.System);
    }

    private Task<Response<StockBehaviourResult>> SetGsku(Gsku gsku, int? version = null, string? baseUom = null,
        IReadOnlyList<ItemUomConversion>? conversions = null, int? shelfLife = null, IReadOnlyList<string>? storage = null,
        bool? lot = null, bool? stockable = null, IProductStockBehaviourRepository? store = null,
        IItemReferenceResolver? references = null, IProductIdentityLifecycleActorContext? actor = null,
        bool enforced = false, Guid? operationId = null) =>
        new SetGskuStockBehaviourHandler(Context(actor, enforced), references ?? new ListFake(),
                store ?? new ProductStockBehaviourRepository(_database, Tenant()))
            .Handle(new SetGskuStockBehaviourCommand(gsku.Id, version ?? gsku.Version, operationId ?? Guid.NewGuid(),
                baseUom, conversions, shelfLife, storage, lot, stockable), default);

    private Task<Response<StockBehaviourResult>> SetLsku(Lsku lsku, int? version = null, bool? serial = null,
        IProductIdentityLifecycleActorContext? actor = null) =>
        new SetLskuStockBehaviourHandler(Context(actor, false), new LskuRepository(_database, Tenant()),
                new ProductStockBehaviourRepository(_database, Tenant()))
            .Handle(new SetLskuStockBehaviourCommand(lsku.Id, version ?? lsku.Version, Guid.NewGuid(), serial), default);

    private Task<Response<StockBehaviourResult>> SetFinishedGood(FinishedGood finishedGood, int? version = null,
        IReadOnlyList<ItemUomConversion>? conversions = null, int? shelfLife = null, IReadOnlyList<string>? storage = null,
        bool? lot = null, bool? serial = null, bool? stockable = null, IProductIdentityLifecycleActorContext? actor = null) =>
        new SetFinishedGoodStockBehaviourHandler(Context(actor, false), new FinishedGoodRepository(_database, Tenant()),
                new ListFake(), new ProductStockBehaviourRepository(_database, Tenant()))
            .Handle(new SetFinishedGoodStockBehaviourCommand(finishedGood.Id, version ?? finishedGood.Version, Guid.NewGuid(),
                conversions, shelfLife, storage, lot, serial, stockable), default);

    private Task<Response<StockBehaviourResult>> AssignGtin(FinishedGood finishedGood, int version, string gtin,
        IProductIdentityLifecycleActorContext? actor = null) =>
        new AssignFinishedGoodGtinHandler(Context(actor, false), new FinishedGoodRepository(_database, Tenant()),
                new ProductStockBehaviourRepository(_database, Tenant()))
            .Handle(new AssignFinishedGoodGtinCommand(finishedGood.Id, version, Guid.NewGuid(), gtin), default);

    private StockBehaviourWrite GtinWrite(FinishedGood finishedGood, string gtin) => new(
        finishedGood.Id, finishedGood.Version, finishedGood.LifecycleStatus,
        new Dictionary<string, object?> { [StockBehaviourFields.Gtin] = gtin }, new HashSet<string>(),
        new StockBehaviourCommandMark(Guid.NewGuid(), new string('a', 64), finishedGood.Version + 1), DateTimeOffset.UtcNow,
        MarksGtin: true);

    private async Task<Gsku> SeedGskuAsync(ProductIdentityLifecycleStatus status, decimal packQuantity = 1m, string packUom = "C62")
    {
        var tenant = Tenant();
        _ = new GskuRepository(_database, tenant);
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, CanonicalCode = "GP-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            GlobalProductName = "Stock Parent", GlobalProductNameNormalized = "STOCK PARENT " + Guid.NewGuid().ToString("N"),
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
        var revision = new ProductDefinitionRevision
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, GlobalProductId = product.Id, RevisionIdentifier = "REV-001",
            CreationCommandId = "REV:" + Guid.NewGuid().ToString("N"),
            LifecycleStatus = status == ProductIdentityLifecycleStatus.Draft ? ProductIdentityLifecycleStatus.Draft : ProductIdentityLifecycleStatus.IdentityApproved
        };
        await _database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(product);
        await _database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").InsertOneAsync(revision);
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProductDefinitionRevisionId = revision.Id,
            CanonicalCode = "GS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("N"), PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES",
            PackQuantity = packQuantity, PackUomCode = packUom, LifecycleStatus = status, Version = 3
        };
        await Gskus.InsertOneAsync(gsku);
        return gsku;
    }

    private async Task<(Gsku Gsku, Lsku Lsku)> SeedLskuAsync(ProductIdentityLifecycleStatus status)
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        _ = new LskuRepository(_database, Tenant());
        var lsku = new Lsku
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, GskuId = gsku.Id,
            CanonicalCode = "LS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("N"), MarketCode = "M" + Guid.NewGuid().ToString("N")[..6],
            LifecycleStatus = status, Version = 2
        };
        await Lskus.InsertOneAsync(lsku);
        return (gsku, lsku);
    }

    private async Task<(Gsku Gsku, FinishedGood FinishedGood)> SeedFinishedGoodAsync(ProductIdentityLifecycleStatus status)
    {
        var gsku = await SeedGskuAsync(ProductIdentityLifecycleStatus.Draft);
        _ = new FinishedGoodRepository(_database, Tenant());
        var finishedGood = new FinishedGood
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, GskuId = gsku.Id,
            CanonicalCode = "FG-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("N"), LifecycleStatus = status, Version = 1
        };
        await FinishedGoods.InsertOneAsync(finishedGood);
        return (gsku, finishedGood);
    }

    private async Task<Guid> SeedGskuIdentityOperationAsync(Gsku gsku, FirstGskuIdentityWorkflowCheckpoint checkpoint)
    {
        var operationId = Guid.NewGuid();
        await _database.GetCollection<FirstGskuIdentityWorkflowOperation>(FirstGskuIdentityWorkflowOperationRepository.CollectionName)
            .InsertOneAsync(new FirstGskuIdentityWorkflowOperation
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, OperationId = operationId, GskuId = gsku.Id,
                ProductDefinitionRevisionId = gsku.ProductDefinitionRevisionId, ExpectedGskuVersion = gsku.Version,
                Checkpoint = checkpoint, CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
            });
        // The reservation touches the GSKU in the same transaction (FIX2 item 10).
        await Gskus.UpdateOneAsync(x => x.Id == gsku.Id, Builders<Gsku>.Update.Inc(x => x.IdentitySubmitReservations, 1));
        return operationId;
    }

    private static FirstGskuIdentityWorkflowBinding Binding(Guid revisionId, Guid gskuId) => new()
    {
        WorkflowInstanceId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(),
        WorkflowTemplateVersionId = Guid.NewGuid(), ApprovalTaskId = Guid.NewGuid(),
        AssignmentSnapshotId = Guid.NewGuid(), StartTransitionLogId = Guid.NewGuid(),
        ObjectType = "gsku", GskuId = gskuId, ProductDefinitionRevisionId = revisionId,
        ObjectRef = $"GS:{gskuId:D}", SubmitterSubjectId = Guid.NewGuid(),
        StartIdempotencyKey = $"start-{Guid.NewGuid():N}", StartRequestFingerprint = $"fp-{Guid.NewGuid():N}",
        SubmittedAtUtc = DateTimeOffset.UtcNow
    };

    private static ProductIdentityWorkflowDecisionEvidence Decision(FirstGskuIdentityWorkflowBinding binding) => new()
    {
        Decision = ProductIdentityDecisionKind.Approved, WorkflowInstanceId = binding.WorkflowInstanceId,
        ApprovalTaskId = binding.ApprovalTaskId, WorkflowTemplateId = binding.WorkflowTemplateId,
        WorkflowTemplateVersionId = binding.WorkflowTemplateVersionId, ObjectType = binding.ObjectType,
        ObjectId = binding.GskuId, ObjectRef = binding.ObjectRef, DecisionActorSubjectId = Guid.NewGuid(),
        DecisionAtUtc = DateTimeOffset.UtcNow, TransitionSequence = 2, TaskStatus = "Approved", InstanceStatus = "Completed"
    };

    private static LocalAuditIntent Intent(Gsku aggregate, ProductAuditOperation operation, int version)
    {
        var key = Guid.NewGuid().ToString("D");
        return new()
        {
            IntentId = Guid.NewGuid(), TenantId = aggregate.TenantId, AggregateType = AuditAggregateType.Gsku,
            AggregateId = aggregate.Id, PreVersion = version, PostVersion = version + 1, Operation = operation,
            ActorId = Guid.NewGuid().ToString("D"), CorrelationId = key, CausationId = key, CommandId = key,
            Sequence = version + 1, TimestampUtc = DateTimeOffset.UtcNow, EvidenceHash = $"hash-{key}", IdempotencyKey = key
        };
    }

    private sealed class Maker(params string[] keys) : IProductIdentityLifecycleActorContext
    {
        private readonly Guid _subject = Guid.NewGuid();
        public bool TryResolveCanonicalHumanSubject(out Guid subjectId) { subjectId = _subject; return true; }
        public bool HasPermission(string permission) => keys.Contains(permission, StringComparer.Ordinal);
    }

    /// <summary>Holds every write until <c>parties</c> callers are at the store, then lets them go together.</summary>
    private sealed class BarrierStore(IProductStockBehaviourRepository inner, int parties) : IProductStockBehaviourRepository
    {
        private readonly TaskCompletionSource _all = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public async Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrived) == parties) _all.SetResult();
            await _all.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }

        public async Task<StockBehaviourWriteResult> WriteGskuAsync(StockBehaviourWrite write, CancellationToken cancellationToken = default)
        {
            await ArriveAsync();
            return await inner.WriteGskuAsync(write, cancellationToken);
        }

        public Task<StockBehaviourWriteResult> WriteLskuAsync(StockBehaviourWrite write, CancellationToken cancellationToken = default) =>
            inner.WriteLskuAsync(write, cancellationToken);

        public Task<StockBehaviourWriteResult> WriteFinishedGoodAsync(StockBehaviourWrite write, CancellationToken cancellationToken = default) =>
            inner.WriteFinishedGoodAsync(write, cancellationToken);

        public Task<bool> GtinHeldByAnotherAsync(string gtin, Guid finishedGoodId, CancellationToken cancellationToken = default) =>
            inner.GtinHeldByAnotherAsync(gtin, finishedGoodId, cancellationToken);
    }

    /// <summary>The two MOD-0048 item lists as FU01 §4 plans them (S1's fake: ConditionKind on storage conditions).</summary>
    private sealed class ListFake : IItemReferenceResolver
    {
        private static readonly Dictionary<string, Dictionary<string, string?>> Sets = new()
        {
            [ItemReferenceSets.ItemUom] = new() { ["C62"] = null, ["MGM"] = null, ["GRM"] = null, ["KGM"] = null,
                ["MLT"] = null, ["LTR"] = null, ["XBX"] = null, ["XCS"] = null },
            [ItemReferenceSets.StorageCondition] = new() { ["AMBIENT_15_25"] = "TEMPERATURE", ["COOL_2_8"] = "TEMPERATURE",
                ["FROZEN_BELOW_MINUS_20"] = "TEMPERATURE", ["PROTECT_FROM_LIGHT"] = "PROTECTION",
                ["PROTECT_FROM_MOISTURE"] = "PROTECTION" }
        };

        public Task<ItemReferenceResolveResult> ResolveLatestAsync(IReadOnlyList<ItemReferenceRequest> requests,
            CancellationToken cancellationToken = default)
        {
            var missing = requests.Where(request =>
                !Sets.TryGetValue(request.SetCode, out var set) || !set.ContainsKey(request.ValueCode)).ToList();
            if (missing.Count > 0) return Task.FromResult(ItemReferenceResolveResult.Missing(missing));
            return Task.FromResult(ItemReferenceResolveResult.Success(requests.Select(request =>
            {
                var kind = Sets[request.SetCode][request.ValueCode];
                return new ItemReferenceSelection(request.SetCode, request.ValueCode,
                    Guid.Parse("6f1d3c55-0e1b-4c38-9a77-1b1f2a9e0c01"), 1, "LATEST",
                    new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero), false, true,
                    kind is null
                        ? new Dictionary<string, string?>()
                        : new Dictionary<string, string?> { [ItemReferenceSets.ConditionKindAttribute] = kind });
            }).ToList()));
        }
    }

    private sealed class NoPolicies : IProductLegalEntityScopePolicyRepository
    {
        public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(Guid globalProductId, CancellationToken cancellationToken = default) => Task.FromResult<ProductLegalEntityScopePolicy?>(null);
        public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default) => Task.FromResult<ProductLegalEntityScopePolicy?>(null);
        public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(ProductLegalEntityScopePolicy policy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(ProductLegalEntityScopePolicy policy, int expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(IReadOnlyCollection<Guid> globalProductIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
