using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-2 — the rules a task meets before anything is written (pack §8.4, §8.5, §8.12, §12; AC-7, AC-8, AC-10, AC-37, AC-40, AC-41,
/// AC-57, AC-58, AC-61): held stock, recall, the target rules, the storage-condition fit (create, update and confirm), the unchanged source
/// bucket, the open quantity and the GSKU serial flag. Every refusal leaves no task, no event and no movement.
/// </summary>
public sealed class PutawayCreateRulesTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public PutawayCreateRulesTests(WarehouseApiFactory factory) => _factory = factory;

    private static JsonElement Row(JsonElement page, string status)
        => page.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("stockStatus").GetString() == status);

    [Theory]
    [InlineData("QUARANTINE")]
    [InlineData("QUALITY_INSPECTION")]
    [InlineData("BLOCKED")]
    public async Task AC7_held_stock_opens_no_task_and_is_listed_not_eligible(string status)
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(10, status: status);

        var error = await Expect(s.CreateTaskAsync(s.TaskBody(5, status: status)), 422);
        Assert.Equal("WMS_STOCK_NOT_PUTAWAY_ELIGIBLE", Code(error));
        Assert.Empty(s.W.Stored("wms_tasks"));

        var row = Row(await s.CandidatesAsync(), status);
        Assert.False(row.GetProperty("eligible").GetBoolean());
        Assert.Equal("StockHeld", row.GetProperty("notEligibleReason").GetString());
    }

    [Fact]
    public async Task AC8_a_recalled_lot_refuses_create_and_confirm_and_a_failed_recall_read_is_503()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(20);
        var task = await s.TaskAsync(10);

        s.Recall();
        var create = await Expect(s.CreateTaskAsync(s.TaskBody(5)), 422);
        Assert.Equal("LOT_UNDER_RECALL", Code(create));
        Assert.Equal("LOCATION_TRANSFER", create.GetProperty("error").GetProperty("details").GetProperty("movementType").GetString());

        var confirm = await Expect(s.ConfirmAsync(task, 5, s.A01), 422);
        Assert.Equal("LOT_UNDER_RECALL", Code(confirm));
        Assert.Empty(s.TaskMovements(TaskId(task)));
        Assert.Empty((await s.GetTaskAsync(TaskId(task))).GetProperty("confirmations").EnumerateArray());

        var other = await new PutawayScenario(_factory).ReadyAsync();
        await other.ReceiveAsync(20);
        _factory.RecallDown[other.Tenant] = true;
        try
        {
            Assert.Equal("INVENTORY_UNAVAILABLE", await ErrorCode(other.CreateTaskAsync(other.TaskBody(5)), 503));
            Assert.Empty(other.W.Stored("wms_tasks"));
        }
        finally
        {
            _factory.RecallDown.TryRemove(other.Tenant, out _);
        }
    }

    [Fact]
    public async Task AC41_a_recalled_row_is_listed_locked_with_the_case_number()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(10);
        s.Recall();

        var row = Row(await s.CandidatesAsync(), "AVAILABLE");
        Assert.False(row.GetProperty("eligible").GetBoolean());
        Assert.Equal("LotUnderRecall", row.GetProperty("notEligibleReason").GetString());
        Assert.Equal("RecallCase", row.GetProperty("blockingDocument").GetProperty("kind").GetString());
        Assert.Equal("GMG-RCL-000001", row.GetProperty("blockingDocument").GetProperty("number").GetString());
        Assert.Single((await s.CandidatesAsync("&eligible=false")).GetProperty("items").EnumerateArray());
        Assert.Empty((await s.CandidatesAsync("&eligible=true")).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task AC10_target_rules_each_refused_with_its_rule_and_nothing_written()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(10);
        var inactive = s.W.Node(LocationType.Bin, status: LocationStatus.Inactive);
        var zone = s.W.Node(LocationType.Zone);
        var staging = s.W.Node(LocationType.Bin);
        await s.W.CreatedAsync(staging.Id, ["Staging"]);
        var siblingReceiving = s.W.Node(LocationType.Bin, s.Zone);
        var otherWarehouse = Guid.Parse(s.Inventory.LocationC);

        async Task Refused(Guid to, string rule)
        {
            var error = await Expect(s.CreateTaskAsync(s.TaskBody(5, to)), 422);
            Assert.Equal("WMS_TARGET_NOT_ALLOWED", Code(error));
            Assert.Equal(rule, error.GetProperty("error").GetProperty("details").GetProperty("rule").GetString());
        }

        await Refused(inactive.Id, "Inactive");
        await Refused(zone.Id, "NotStockable");
        await Refused(otherWarehouse, "OtherWarehouse");
        await Refused(s.R01, "SameAsSource");
        await Refused(staging.Id, "RoleLocation");
        await Refused(siblingReceiving.Id, "RoleLocation");
        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.CreateTaskAsync(s.TaskBody(5, Guid.NewGuid())), 422));
        Assert.Empty(s.W.Stored("wms_tasks"));

        // the same rules at confirmation
        var task = await s.TaskAsync(5);
        Assert.Equal("WMS_TARGET_NOT_ALLOWED", await ErrorCode(s.ConfirmAsync(task, 5, staging.Id), 422));
        Assert.Empty(s.TaskMovements(TaskId(task)));
    }

    [Fact]
    public async Task AC58_a_receiving_zone_child_is_a_source_never_a_target_and_its_role_cannot_go_while_a_task_is_open()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(10);
        var task = await s.TaskAsync(5);
        Assert.Equal(s.R01.ToString("D"), task.GetProperty("fromLocationId").GetString());

        var role = Assert.Single((await Expect(s.W.GetAsync($"/api/warehouse/location-roles?locationId={s.Zone.Id:D}"), 200)).GetProperty("items").EnumerateArray());
        var etag = ETag(role);
        Assert.Equal("WMS_LOCATION_HAS_OPEN_TASKS", await ErrorCode(s.W.UpdateAsync(Id(role), new() { ["roles"] = Array.Empty<string>(), ["acceptedStorageConditions"] = new[] { "AMBIENT_15_25" } }, etag), 409));
        Assert.Equal("WMS_LOCATION_HAS_OPEN_TASKS", await ErrorCode(s.W.DeleteAsync(Id(role), etag), 409));

        await Expect(s.CancelAsync(TaskId(task), ETagOf(task)), 200);
        await Expect(s.W.DeleteAsync(Id(role), etag), 200);
    }

    [Fact]
    public async Task AC37_storage_condition_fit_on_create_update_and_confirm()
    {
        var s = await new PutawayScenario(_factory, productConditions: ["COOL_2_8"]).ReadyAsync();
        await s.ReceiveAsync(30);
        var ambientShelf = s.W.Node(LocationType.Bin);
        await s.W.CreatedAsync(ambientShelf.Id, [], ["AMBIENT_15_25"]);
        var coolZone = s.W.Node(LocationType.Zone);
        await s.W.CreatedAsync(coolZone.Id, [], ["COOL_2_8"]);
        var coolBin = s.W.Node(LocationType.Bin, coolZone);

        foreach (var shelf in new[] { s.A01, ambientShelf.Id })
        {
            Assert.Equal("WMS_STORAGE_CONDITION_MISMATCH", await ErrorCode(s.CreateTaskAsync(s.TaskBody(5, shelf)), 422));
        }

        var task = await s.TaskAsync(10, coolBin.Id);
        Assert.Equal("WMS_STORAGE_CONDITION_MISMATCH",
            await ErrorCode(s.UpdateTaskAsync(TaskId(task), new() { ["toLocationId"] = s.A01.ToString("D") }, ETagOf(task)), 422));
        var mismatch = await Expect(s.ConfirmAsync(task, 5, ambientShelf.Id, Reason), 422);
        Assert.Equal("WMS_STORAGE_CONDITION_MISMATCH", Code(mismatch));
        Assert.Equal(["COOL_2_8"], Strings(mismatch.GetProperty("error").GetProperty("details").GetProperty("productConditions")));
        var posted = await Expect(s.ConfirmAsync(task, 10, coolBin.Id), 201);
        Assert.Equal("Completed", Status(posted));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("AMBIENT_15_25")]
    public async Task AC37_a_conditionless_or_ambient_product_fits_an_unlabelled_shelf(string? condition)
    {
        var s = await new PutawayScenario(_factory, productConditions: condition is null ? [] : [condition]).ReadyAsync();
        await s.ReceiveAsync(5);
        var task = await s.TaskAsync(5, s.A01);
        Assert.Equal("Completed", Status(await Expect(s.ConfirmAsync(task, 5, s.A01), 201)));
    }

    [Fact]
    public async Task AC37_a_sku_without_stated_conditions_is_unknown()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        _factory.StorageConditions[s.Sku] = null;
        await s.ReceiveAsync(5);
        Assert.Equal("WMS_STORAGE_CONDITIONS_UNKNOWN", await ErrorCode(s.CreateTaskAsync(s.TaskBody(5, s.A01)), 422));
        var task = await s.TaskAsync(5);
        Assert.Equal("WMS_STORAGE_CONDITIONS_UNKNOWN", await ErrorCode(s.ConfirmAsync(task, 5, s.A01), 422));
    }

    [Fact]
    public async Task AC40_the_source_bucket_never_changes_and_planned_quantity_respects_confirmed_and_pending()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(60);
        var task = await s.TaskAsync(60);

        foreach (var member in new[] { "fromLocationId", "warehouseId", "lotId", "stockStatus", "item" })
        {
            Assert.Equal("INVALID_REQUEST", await ErrorCode(s.UpdateTaskAsync(TaskId(task), new() { [member] = "x" }, ETagOf(task)), 400));
        }

        var posted = await Expect(s.ConfirmAsync(task, 40, s.A01), 201);
        Assert.Equal("WMS_QUANTITY_BELOW_CONFIRMED",
            await ErrorCode(s.UpdateTaskAsync(TaskId(task), new() { ["plannedQuantity"] = "30" }, ETagOf(posted)), 422));
        var over = await Expect(s.ConfirmAsync(posted, 21, s.A01), 422);
        Assert.Equal("WMS_QUANTITY_EXCEEDS_REMAINING", Code(over));
        Assert.Equal(20m, D(over.GetProperty("error").GetProperty("details"), "remainingQuantity"));
    }

    [Fact]
    public async Task AC57_a_second_task_beyond_the_open_quantity_is_refused()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(100);
        await s.TaskAsync(60);

        var error = await Expect(s.CreateTaskAsync(s.TaskBody(60)), 422);
        Assert.Equal("WMS_QUANTITY_EXCEEDS_OPEN", Code(error));
        Assert.Equal(40m, D(error.GetProperty("error").GetProperty("details"), "openQuantity"));
    }

    [Fact]
    public async Task AC57_two_concurrent_60s_on_100_the_ledger_refuses_the_second_with_insufficient_stock_and_40_then_posts()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(100);
        var barrier = new AsyncBarrier(2);
        _factory.BeforeTaskWrite[s.Tenant] = barrier.SignalAndWaitAsync;
        JsonElement[] tasks;
        try
        {
            tasks = await Task.WhenAll(Expect(s.CreateTaskAsync(s.TaskBody(60)), 201), Expect(s.CreateTaskAsync(s.TaskBody(60)), 201));
        }
        finally
        {
            _factory.BeforeTaskWrite.TryRemove(s.Tenant, out _);
        }

        Assert.Equal("Completed", Status(await Expect(s.ConfirmAsync(tasks[0], 60, s.A01), 201)));
        var refused = await Expect(s.ConfirmAsync(tasks[1], 60, s.A01), 422);
        Assert.Equal("INSUFFICIENT_STOCK", Code(refused));
        var after = await s.GetTaskAsync(TaskId(tasks[1]));
        Assert.Equal("Refused", ConfirmationStatus(after, 1));
        Assert.Equal(40m, await s.OnHandAsync(s.R01));
        Assert.Equal((1, 1), s.Central("ConfirmPutawayTaskCommand")); // the first confirmation forwarded; the refused one only Abandoned
        Assert.Equal(["Created", "ConfirmationRecorded", "ConfirmationRefused"], s.EventKinds(TaskId(tasks[1])));

        var forty = await Expect(s.ConfirmAsync(after, 40, s.A01), 201);
        Assert.Equal("Posted", ConfirmationStatus(forty, 2));
        Assert.Equal(0m, await s.OnHandAsync(s.R01));
    }

    [Theory]
    [InlineData(false, 201, null)]
    [InlineData(true, 422, "SKU_LEVEL_NOT_STOCKABLE")]
    public async Task AC61_a_gsku_task_reads_the_product_serial_flag(bool productSerial, int status, string? code)
    {
        var s = await new PutawayScenario(_factory, lotControlled: false, level: SkuLevel.Gsku).ReadyAsync();
        _factory.Products.AddItem(s.Item, false, productSerial);
        if (productSerial)
        {
            // the receipt itself is refused for a serial product at GSKU — open the bucket while the product is still unflagged
            _factory.Products.AddItem(s.Item, false, false);
            await s.ReceiveAsync(5);
            _factory.Products.AddItem(s.Item, false, true);
        }
        else
        {
            await s.ReceiveAsync(5);
        }

        using var response = await s.CreateTaskAsync(s.TaskBody(5));
        Assert.Equal(status, (int)response.StatusCode);
        if (code is not null) Assert.Contains(code, await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task AC42_the_suggestion_is_information_only_fitting_bins_only_and_applying_it_is_recorded()
    {
        var s = await new PutawayScenario(_factory, productConditions: ["COOL_2_8"]).ReadyAsync();
        var coolZone = s.W.Node(LocationType.Zone);
        await s.W.CreatedAsync(coolZone.Id, [], ["COOL_2_8"]);
        var coolBin = s.W.Node(LocationType.Bin, coolZone);
        await s.ReceiveAsync(1, s.B01);    // stocked before on an unlabelled bin — not a fit for a COOL product
        await s.ReceiveAsync(1, coolBin.Id);
        await s.ReceiveAsync(10);

        var plain = await s.TaskAsync(5);
        Assert.Equal(coolBin.Id.ToString("D"), plain.GetProperty("suggestedToLocationId").GetString());
        Assert.False(plain.TryGetProperty("toLocationId", out var to) && to.ValueKind == JsonValueKind.String);
        Assert.Equal("Manual", plain.GetProperty("targetSource").GetString());

        var body = s.TaskBody(5, coolBin.Id);
        body["targetSource"] = "SuggestionApplied";
        var applied = await Expect(s.CreateTaskAsync(body), 201);
        Assert.Equal("SuggestionApplied", applied.GetProperty("targetSource").GetString());
        Assert.Equal(["Created", "SuggestionApplied"], s.EventKinds(TaskId(applied)));
    }
}
