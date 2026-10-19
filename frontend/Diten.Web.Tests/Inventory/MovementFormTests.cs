using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Models.Inventory;
using Diten.Web.Services;
using Diten.Web.Views.Inventory.StockLedger;
using Diten.Web.Views.Inventory.StockMovements;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// MOD-0173-FU01 UI-2 — the S-3 movement form, "Correct" (KD-1) and the stock entry (KD-2), measured on the REAL rules
/// and the REAL controller with a Gateway handler that answers what Gateway 5000 + MOD-0173 answer:
/// <list type="bullet">
/// <item>the direction matrix (pack §12): what each of the 7 manual types requires, forbids and posts;</item>
/// <item>one submit per form: the key is the FORM's, so a second post replays instead of posting twice (pack §13);</item>
/// <item>every MOD-0173 refusal lands on its field / form / page, in the reader's language, nothing posted;</item>
/// <item>Correct = REVERSAL + new movement, two keys, an explicit partial state, a retry with the same keys;</item>
/// <item>"Correct" is offered only on a manual, unreversed, non-reversal row of a manual type;</item>
/// <item>the stock entry page offers inbound types only and StockLedger has no write path (KD-2).</item>
/// </list>
/// </summary>
public sealed class MovementFormTests
{
    private const string Gateway = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private const string Item = "b1f2c3d4-0000-0000-0000-000000000001";
    private const string SkuId = "c3d4e5f6-0000-0000-0000-000000000002";
    private const string Sku = "Lsku:" + SkuId;
    private const string Wh1 = "11111111-1111-1111-1111-111111111111";
    private const string Wh2 = "22222222-2222-2222-2222-222222222222";
    private const string Bin1 = "33333333-3333-3333-3333-333333333333";
    private const string Bin2 = "44444444-4444-4444-4444-444444444444";
    private const string Now = "2026-10-02T09:00:00.000Z";

    private static readonly string[] ReadAndCreate = [InventoryPermissionKeys.Read, InventoryPermissionKeys.CreateMovements];

    // ── the direction matrix (pack §12) ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("OPENING_BALANCE")]
    [InlineData("COUNT_GAIN")]
    public void An_inbound_needs_target_warehouse_location_and_status_and_never_posts_a_source(string type)
    {
        var bare = MovementRules.Check(Form(type) with { ToWarehouseId = null, ToLocationId = null, ToStockStatus = null }, MovementRules.ManualTypes, "k");
        Assert.Equal(["toLocationId", "toStockStatus", "toWarehouseId"], bare.Errors.Keys.OrderBy(k => k));

        var ok = MovementRules.Check(Form(type) with { Source = $"{Wh2}|{Bin2}|AVAILABLE|" }, MovementRules.ManualTypes, "k");
        Assert.True(ok.IsValid);
        Assert.Equal(Wh1, ok.Payload!["toWarehouseId"]);
        Assert.Equal("QUALITY_INSPECTION", ok.Payload["toStockStatus"]);
        // The source the screen hid is ignored, never sent (the service would refuse an inbound with a from side).
        foreach (var absent in new[] { "fromWarehouseId", "fromLocationId", "fromStockStatus" })
            Assert.False(ok.Payload.ContainsKey(absent), absent);
    }

    [Theory]
    [InlineData("COUNT_LOSS")]
    [InlineData("SCRAP")]
    [InlineData("WRITE_OFF")]
    public void An_outbound_needs_a_source_row_and_never_posts_a_target(string type)
    {
        Assert.Equal("ValRequired", MovementRules.Check(Form(type) with { Source = null }, MovementRules.ManualTypes, "k").Errors["source"]);

        var ok = MovementRules.Check(Form(type) with { Source = $"{Wh1}|{Bin1}|QUARANTINE|LOT-7" }, MovementRules.ManualTypes, "k");
        Assert.True(ok.IsValid);
        Assert.Equal(Bin1, ok.Payload!["fromLocationId"]);
        Assert.Equal("QUARANTINE", ok.Payload["fromStockStatus"]);
        // The lot of an outbound is the SOURCE row's, not a typed one.
        Assert.Equal("LOT-7", ok.Payload["lotId"]);
        foreach (var absent in new[] { "toWarehouseId", "toLocationId", "toStockStatus" })
            Assert.False(ok.Payload.ContainsKey(absent), absent);
    }

    [Fact]
    public void A_warehouse_transfer_needs_another_warehouse_and_keeps_the_source_status()
    {
        var same = MovementRules.Check(Form("WAREHOUSE_TRANSFER") with { Source = $"{Wh1}|{Bin1}|AVAILABLE|", ToWarehouseId = Wh1 }, MovementRules.ManualTypes, "k");
        Assert.Equal("ValSameWarehouse", same.Errors["toWarehouseId"]);

        var ok = MovementRules.Check(Form("WAREHOUSE_TRANSFER") with { Source = $"{Wh1}|{Bin1}|AVAILABLE|", ToWarehouseId = Wh2, ToLocationId = Bin2 }, MovementRules.ManualTypes, "k");
        Assert.True(ok.IsValid);
        Assert.False(ok.Payload!.ContainsKey("toStockStatus"));
        Assert.Equal(Wh2, ok.Payload["toWarehouseId"]);
    }

    [Fact]
    public void A_location_transfer_stays_in_the_source_warehouse_and_needs_another_location()
    {
        var ok = MovementRules.Check(Form("LOCATION_TRANSFER") with { Source = $"{Wh1}|{Bin1}|AVAILABLE|", ToWarehouseId = null, ToLocationId = Bin2 }, MovementRules.ManualTypes, "k");
        Assert.True(ok.IsValid);
        Assert.Equal(Wh1, ok.Payload!["toWarehouseId"]);

        var sameBin = MovementRules.Check(Form("LOCATION_TRANSFER") with { Source = $"{Wh1}|{Bin1}|AVAILABLE|", ToLocationId = Bin1 }, MovementRules.ManualTypes, "k");
        Assert.Equal("ValSameLocation", sameBin.Errors["toLocationId"]);

        var otherWarehouse = MovementRules.Check(Form("LOCATION_TRANSFER") with { Source = $"{Wh1}|{Bin1}|AVAILABLE|", ToWarehouseId = Wh2, ToLocationId = Bin2 }, MovementRules.ManualTypes, "k");
        Assert.Equal("ValSameWarehouseRequired", otherWarehouse.Errors["toWarehouseId"]);
    }

    [Fact]
    public void Status_transfer_and_automatic_types_are_not_accepted_and_the_entry_page_takes_inbound_only()
    {
        foreach (var type in new[] { "STATUS_TRANSFER", "GOODS_RECEIPT_PO", "REVERSAL", "CUSTOMER_RETURN" })
            Assert.Equal("ValMovementType", MovementRules.Check(Form(type), MovementRules.ManualTypes, "k").Errors["movementType"]);

        Assert.Equal(["OPENING_BALANCE", "COUNT_GAIN"], MovementRules.EntryTypes);
        Assert.Equal("ValMovementType", MovementRules.Check(Form("COUNT_LOSS") with { Source = $"{Wh1}|{Bin1}|AVAILABLE|" }, MovementRules.EntryTypes, "k").Errors["movementType"]);
        Assert.Equal(["AVAILABLE", "QUALITY_INSPECTION", "QUARANTINE"], MovementRules.InboundStatuses);
        Assert.Equal("ValRequired", MovementRules.Check(Form("OPENING_BALANCE") with { ToStockStatus = "BLOCKED" }, MovementRules.ManualTypes, "k").Errors["toStockStatus"]);
    }

    [Theory]
    [InlineData(" lot-2026/a.1 ", "LOT-2026/A.1", true)]
    [InlineData("", null, true)]
    [InlineData("LOT 1", null, false)]
    [InlineData("ÇÜ-1", null, false)]
    [InlineData("A23456789012345678901234567890123456789012", null, false)]
    public void A_typed_lot_is_trimmed_upper_cased_and_limited_to_the_W_UI_02_format(string raw, string? expected, bool valid)
    {
        Assert.Equal(valid, MovementRules.TryNormalizeLot(raw, out var lot));
        Assert.Equal(expected, lot);
    }

    [Fact]
    public void Quantity_serials_and_dates_are_shape_checked()
    {
        foreach (var bad in new[] { "0", "-1", "1,5", "1.1234567", "1e3", "" })
            Assert.Equal("ValQuantity", MovementRules.Check(Form("OPENING_BALANCE") with { Quantity = bad }, MovementRules.ManualTypes, "k").Errors["quantity"]);
        Assert.Equal("ValSerialDuplicate", MovementRules.Check(Form("OPENING_BALANCE") with { SerialNumbers = "S1\nS2\n S1 " }, MovementRules.ManualTypes, "k").Errors["serialNumbers"]);
        Assert.Equal("ValDate", MovementRules.Check(Form("OPENING_BALANCE") with { TransactionDate = "2026-10-02 09:00" }, MovementRules.ManualTypes, "k").Errors["transactionDate"]);

        var ok = MovementRules.Check(Form("OPENING_BALANCE") with { SerialNumbers = "S1\n\n S2 \n" }, MovementRules.ManualTypes, "k");
        Assert.Equal(new[] { "S1", "S2" }, (IEnumerable<string>)ok.Payload!["serialIds"]!);
        Assert.Equal("2026-10-02T09:00:00Z", ok.Payload["transactionDate"]);
        Assert.Equal("MOD-0173", ok.Payload["sourceModule"]);
        Assert.Equal("MANUAL_MOVEMENT", ok.Payload["sourceType"]);
        // never tenantId / legalEntityId (parent §8: 400)
        Assert.DoesNotContain(ok.Payload.Keys, k => k.Contains("tenant", StringComparison.OrdinalIgnoreCase) || k.Contains("legalEntity", StringComparison.OrdinalIgnoreCase));
    }

    // ── the post: one key per form, refusals placed, receipt ─────────────────────────────────────────────

    [Fact]
    public async Task The_same_form_posted_twice_sends_the_forms_key_both_times_so_the_service_replays()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/movements", 201, """{"transactionId":"t-1","transactionNumber":"INV-2026-0000001","idempotentReplay":false,"balanceSnapshot":{"onHand":"24","available":"24","stockStatus":"AVAILABLE","baseUomId":"ADET","baseQuantity":"24"}}""");
        var key = Guid.NewGuid();
        var form = Form("OPENING_BALANCE") with { IdempotencyKey = key.ToString() };

        await Movements(gateway, ReadAndCreate).PostMovement(form.ToModel(), CancellationToken.None);
        await Movements(gateway, ReadAndCreate).PostMovement(form.ToModel(), CancellationToken.None);

        var posts = gateway.Requests.Where(r => r.Method == "POST").ToList();
        Assert.Equal(2, posts.Count);
        Assert.All(posts, p => Assert.Equal("movement-" + key.ToString("N"), p.IdempotencyKey));
        Assert.All(posts, p => Assert.Equal(p.IdempotencyKey, JsonDocument.Parse(p.Body).RootElement.GetProperty("idempotencyKey").GetString()));
        Assert.Equal(posts[0].Body, posts[1].Body);
    }

    [Fact]
    public async Task A_posted_movement_leaves_its_receipt_for_Details_and_says_where_to_go()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/movements", 200, """{"transactionId":"t-1","transactionNumber":"INV-2026-0000001","idempotentReplay":true,"balanceSnapshot":{"warehouseId":"wh","locationId":"bin","onHand":"24.000","available":"24.000","stockStatus":"QUALITY_INSPECTION","baseUomId":"ADET","baseQuantity":"24"}}""");
        var controller = Movements(gateway, ReadAndCreate);

        var json = Body(await controller.PostMovement(Form("OPENING_BALANCE").ToModel(), CancellationToken.None));

        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("/StockMovements/Details", json.GetProperty("redirect").GetString());
        var receipt = MovementReceiptViewModel.From(controller.TempData);
        Assert.Equal("INV-2026-0000001", receipt.TransactionNumber);
        Assert.True(receipt.IdempotentReplay);
        Assert.Equal("24.000", receipt.BalanceOnHand);
        Assert.Equal("24", receipt.BaseQuantity);
        Assert.Equal("QUALITY_INSPECTION", receipt.BalanceStatus);
    }

    // WP-MVP1-FIX-LIVE2 (finding 9, owner step MOD-0173 makbuz): the receipt names a registered lot by its NUMBER (TRACE listLots of the SKU,
    // the caller's own trace.lots.read — FIX-LIVE1 pattern); without the key or when TRACE cannot say, the id with a hint (sabotage g)
    [Fact]
    public async Task The_receipt_names_a_registered_lot_by_its_number_and_an_unreadable_one_by_its_id_with_a_hint()
    {
        const string lot = "d4e5f6a7-0000-4000-8000-000000000042";
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/movements", 201, $$$"""{"transactionId":"t-9","transactionNumber":"INV-2026-0000042","idempotentReplay":false,"balanceSnapshot":{"lotId":"{{{lot}}}","onHand":"7.5","available":"7.5","stockStatus":"QUALITY_INSPECTION","baseUomId":"ADET","baseQuantity":"7.5"}}""");
        gateway.On("/api/trace/lots", 200, $$$"""{"items":[{"lotId":"{{{lot}}}","lotNumber":"LOT-2026-042"}],"total":1}""");
        var form = Form("OPENING_BALANCE") with { LotId = lot };

        var named = Movements(gateway, [.. ReadAndCreate, "trace.lots.read"]);
        await named.PostMovement(form.ToModel(), CancellationToken.None);
        var receipt = MovementReceiptViewModel.From(named.TempData);
        Assert.Equal(("LOT-2026-042", false, lot), (receipt.LotNumber, receipt.LotUnresolved, receipt.LotId));
        Assert.Equal(("LOT-2026-042", false), (receipt.BalanceLotNumber, receipt.BalanceLotUnresolved));
        var trace = Assert.Single(gateway.Requests, r => r.PathAndQuery.StartsWith("/api/trace/lots?", StringComparison.Ordinal)); // ONE listLots for the SKU
        Assert.Contains($"skuId={SkuId}", trace.PathAndQuery, StringComparison.Ordinal);

        // without the caller's own trace.lots.read: no TRACE call, the id stays with the hint — never a number made up
        gateway.Requests.Clear();
        var plain = Movements(gateway, ReadAndCreate);
        await plain.PostMovement((form with { IdempotencyKey = Guid.NewGuid().ToString() }).ToModel(), CancellationToken.None);
        var bare = MovementReceiptViewModel.From(plain.TempData);
        Assert.Equal((null, true, lot), (bare.LotNumber, bare.LotUnresolved, bare.LotId));
        Assert.DoesNotContain(gateway.Requests, r => r.PathAndQuery.StartsWith("/api/trace/lots?", StringComparison.Ordinal));

        // a typed lot text (pre-MOD-0174) is not "unresolved"
        var typed = Movements(gateway, [.. ReadAndCreate, "trace.lots.read"]);
        await typed.PostMovement((Form("OPENING_BALANCE") with { LotId = "lot-1" }).ToModel(), CancellationToken.None);
        Assert.False(MovementReceiptViewModel.From(typed.TempData).LotUnresolved);
    }

    [Fact]
    public async Task Without_the_create_permission_or_with_a_malformed_form_nothing_reaches_the_gateway()
    {
        var gateway = new FakeGateway();
        var readOnly = await Movements(gateway, [InventoryPermissionKeys.Read]).PostMovement(Form("OPENING_BALANCE").ToModel(), CancellationToken.None);
        var noKey = await Movements(gateway, ReadAndCreate).PostMovement((Form("OPENING_BALANCE") with { IdempotencyKey = "x" }).ToModel(), CancellationToken.None);
        var noTarget = await Movements(gateway, ReadAndCreate).PostMovement((Form("OPENING_BALANCE") with { ToLocationId = null }).ToModel(), CancellationToken.None);
        var entryOutbound = await Movements(gateway, ReadAndCreate).PostMovement((Form("SCRAP") with { Source = $"{Wh1}|{Bin1}|AVAILABLE|", Entry = true }).ToModel(), CancellationToken.None);

        Assert.Equal(403, Status(readOnly));
        Assert.Equal(400, Status(noKey));
        Assert.Equal(400, Status(noTarget));
        Assert.Equal("ValRequired", Body(noTarget).GetProperty("fields").GetProperty("toLocationId").GetString());
        Assert.Equal("ValMovementType", Body(entryOutbound).GetProperty("fields").GetProperty("movementType").GetString());
        Assert.Empty(gateway.Requests);
    }

    [Theory]
    [InlineData("INSUFFICIENT_STOCK", 422, "field", "quantity")]
    [InlineData("LOT_REQUIRED", 422, "field", "lotId")]
    [InlineData("SERIAL_COUNT_MISMATCH", 422, "field", "serialNumbers")]
    [InlineData("UOM_CONVERSION_MISSING", 422, "field", "uomId")]
    [InlineData("UNKNOWN_ITEM", 422, "field", "itemId")]
    [InlineData("SKU_ITEM_MISMATCH", 422, "field", "sku")]
    [InlineData("POSTING_PERIOD_CLOSED", 422, "field", "postingDate")]
    [InlineData("IDEMPOTENCY_KEY_REUSED", 400, "restart", null)]
    [InlineData("PRODUCT_MASTER_UNAVAILABLE", 503, "page", null)]
    [InlineData("LOCATION_MASTER_UNAVAILABLE", 503, "page", null)]
    [InlineData("CROSS_LE_FORBIDDEN", 403, "form", null)]
    public async Task A_service_refusal_lands_where_it_belongs_in_the_readers_language(string code, int status, string scope, string? field)
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/movements", status, $$"""{"error":{"code":"{{code}}","message":"x","details":{"available":"5.000","requested":"500.000"},"correlationId":"c-1"},"contractVersion":"v1"}""");

        var result = await Movements(gateway, ReadAndCreate).PostMovement((Form("SCRAP") with { Source = $"{Wh1}|{Bin1}|AVAILABLE|" }).ToModel(), CancellationToken.None);

        Assert.Equal(status, Status(result));
        var error = Body(result).GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(scope, error.GetProperty("scope").GetString());
        Assert.Equal(field, error.GetProperty("field").GetString());
        Assert.Equal("c-1", error.GetProperty("correlationId").GetString());
        var message = error.GetProperty("message").GetString()!;
        Assert.StartsWith(InventoryErrorMessages.KeyFor(code, status), message, StringComparison.Ordinal);
        if (code == "INSUFFICIENT_STOCK") Assert.EndsWith(InventoryErrorMessages.InsufficientStockAmounts, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_location_is_shown_on_the_source_when_it_is_the_sources_and_on_the_target_otherwise()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/movements", 422, "{\"error\":{\"code\":\"UNKNOWN_LOCATION\",\"message\":\"x\",\"details\":{\"locationId\":\"" + Bin1 + "\"}}}");
        var transfer = Form("LOCATION_TRANSFER") with { Source = $"{Wh1}|{Bin1}|AVAILABLE|", ToLocationId = Bin2 };

        var source = Body(await Movements(gateway, ReadAndCreate).PostMovement(transfer.ToModel(), CancellationToken.None));
        var inbound = Body(await Movements(gateway, ReadAndCreate).PostMovement(Form("OPENING_BALANCE").ToModel(), CancellationToken.None));

        Assert.Equal("source", source.GetProperty("error").GetProperty("field").GetString());
        Assert.Equal("toLocationId", inbound.GetProperty("error").GetProperty("field").GetString());
    }

    // ── Correct (KD-1): REVERSAL + new movement, two keys, explicit partial state ─────────────────────────

    [Fact]
    public async Task Correct_posts_the_reversal_then_the_corrected_movement_under_two_keys()
    {
        var gateway = new FakeGateway();
        gateway.OnMovement(key => key.StartsWith("reversal-", StringComparison.Ordinal)
            ? (201, """{"transactionId":"r-1","transactionNumber":"INV-R","idempotentReplay":false}""")
            : (201, """{"transactionId":"t-2","transactionNumber":"INV-NEW","idempotentReplay":false,"balanceSnapshot":{"onHand":"3"}}"""));
        var correction = Correction(Form("COUNT_LOSS") with { Source = $"{Wh1}|{Bin1}|AVAILABLE|" });
        var controller = Movements(gateway, ReadAndCreate);

        var json = Body(await controller.PostCorrection(correction, CancellationToken.None));

        Assert.True(json.GetProperty("success").GetBoolean());
        var posts = gateway.Requests.Where(r => r.Method == "POST").ToList();
        Assert.Equal(2, posts.Count);
        Assert.Equal("reversal-" + Guid.Parse(correction.ReversalKey!).ToString("N"), posts[0].IdempotencyKey);
        Assert.Equal("movement-" + Guid.Parse(correction.Movement!.IdempotencyKey!).ToString("N"), posts[1].IdempotencyKey);
        using (var reversal = JsonDocument.Parse(posts[0].Body))
        {
            var root = reversal.RootElement;
            Assert.Equal("REVERSAL", root.GetProperty("movementType").GetString());
            Assert.Equal(correction.TransactionId, root.GetProperty("sourceDocumentId").GetString());
            Assert.Equal("7.5", root.GetProperty("quantity").GetString());
            // The reversal's instant was fixed when the page opened: a retry is the same request (a replay).
            Assert.Equal("2026-10-02T08:00:00Z", root.GetProperty("transactionDate").GetString());
            Assert.False(root.TryGetProperty("fromWarehouseId", out _));
        }

        Assert.Equal("COUNT_LOSS", JsonDocument.Parse(posts[1].Body).RootElement.GetProperty("movementType").GetString());
        var receipt = MovementReceiptViewModel.From(controller.TempData);
        Assert.Equal("INV-R", receipt.ReversalNumber);
        Assert.Equal("INV-OLD", receipt.CorrectedNumber);
        Assert.Equal("INV-NEW", receipt.TransactionNumber);
    }

    [Fact]
    public async Task A_refused_reversal_posts_nothing_else()
    {
        var gateway = new FakeGateway();
        gateway.OnMovement(_ => (422, """{"error":{"code":"ALREADY_REVERSED","message":"x"}}"""));

        var result = await Movements(gateway, ReadAndCreate).PostCorrection(Correction(Form("OPENING_BALANCE")), CancellationToken.None);

        Assert.Equal(422, Status(result));
        Assert.Equal("reversal", Body(result).GetProperty("step").GetString());
        Assert.Single(gateway.Requests);
    }

    [Fact]
    public async Task A_refused_corrected_movement_is_an_explicit_partial_correction_and_the_retry_reuses_both_keys()
    {
        var gateway = new FakeGateway();
        gateway.OnMovement(key => key.StartsWith("reversal-", StringComparison.Ordinal)
            ? (201, """{"transactionId":"r-1","transactionNumber":"INV-R","idempotentReplay":false}""")
            : (422, """{"error":{"code":"INSUFFICIENT_STOCK","message":"x","details":{"available":"5","requested":"500"}}}"""));
        var correction = Correction(Form("COUNT_LOSS") with { Source = $"{Wh1}|{Bin1}|AVAILABLE|", Quantity = "500" });

        var first = await Movements(gateway, ReadAndCreate).PostCorrection(correction, CancellationToken.None);
        var json = Body(first);
        Assert.True(json.GetProperty("partial").GetBoolean());
        Assert.Equal("INV-R", json.GetProperty("reversalNumber").GetString());
        Assert.Equal("quantity", json.GetProperty("error").GetProperty("field").GetString());

        // Retry with the SAME keys (the user lowered the quantity): (1) replays, (2) is posted again under its key.
        gateway.OnMovement(key => key.StartsWith("reversal-", StringComparison.Ordinal)
            ? (200, """{"transactionId":"r-1","transactionNumber":"INV-R","idempotentReplay":true}""")
            : (201, """{"transactionId":"t-2","transactionNumber":"INV-NEW","idempotentReplay":false}"""));
        correction.Movement!.Quantity = "4";
        var second = Body(await Movements(gateway, ReadAndCreate).PostCorrection(correction, CancellationToken.None));

        Assert.True(second.GetProperty("success").GetBoolean());
        var keys = gateway.Requests.Where(r => r.Method == "POST").Select(r => r.IdempotencyKey).ToList();
        Assert.Equal(4, keys.Count);
        Assert.Equal(keys[0], keys[2]);
        Assert.Equal(keys[1], keys[3]);
    }

    [Fact]
    public async Task A_malformed_correction_reverses_nothing()
    {
        var gateway = new FakeGateway();
        var bad = Correction(Form("OPENING_BALANCE") with { ToLocationId = null });
        var sameKeys = Correction(Form("OPENING_BALANCE"));
        sameKeys.ReversalKey = sameKeys.Movement!.IdempotencyKey;
        var noOriginal = Correction(Form("OPENING_BALANCE"));
        noOriginal.OriginalQuantity = "1e3";

        Assert.Equal(400, Status(await Movements(gateway, ReadAndCreate).PostCorrection(bad, CancellationToken.None)));
        Assert.Equal(400, Status(await Movements(gateway, ReadAndCreate).PostCorrection(sameKeys, CancellationToken.None)));
        Assert.Equal(400, Status(await Movements(gateway, ReadAndCreate).PostCorrection(noOriginal, CancellationToken.None)));
        Assert.Equal(403, Status(await Movements(gateway, [InventoryPermissionKeys.Read]).PostCorrection(Correction(Form("OPENING_BALANCE")), CancellationToken.None)));
        Assert.Empty(gateway.Requests);
    }

    // ── "Correct" only on the right rows (KD-1) ────────────────────────────────────────────────────────

    [Fact]
    public void Correct_is_offered_only_on_a_manual_unreversed_row_of_a_manual_type()
    {
        string Id() => Guid.NewGuid().ToString();
        var manual = new TransactionItemDto { TransactionId = Id(), MovementType = "COUNT_LOSS", SourceModule = "MOD-0173" };
        var reversed = new TransactionItemDto { TransactionId = Id(), MovementType = "OPENING_BALANCE", SourceModule = "MOD-0173" };
        var reversal = new TransactionItemDto { TransactionId = Id(), MovementType = "REVERSAL", SourceModule = "MOD-0173", SourceDocumentId = reversed.TransactionId };
        var automatic = new TransactionItemDto { TransactionId = Id(), MovementType = "GOODS_RECEIPT_PO", SourceModule = "MOD-0142" };
        var manualStatusTransfer = new TransactionItemDto { TransactionId = Id(), MovementType = "STATUS_TRANSFER", SourceModule = "MOD-0173" };

        var rows = InventoryListAdapter.MapTransactions([manual, reversed, reversal, automatic, manualStatusTransfer], MasterLabels.None);

        Assert.Equal([true, false, false, false, false], rows.Select(r => r.CanCorrect));
    }

    // ── KD-2: the stock entry page writes nothing itself ───────────────────────────────────────────────

    [Fact]
    public void StockLedger_has_no_write_action_the_entry_page_posts_through_the_movement_form()
    {
        var methods = typeof(StockLedgerController).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(methods, m => m.GetCustomAttributes(true).Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute h && !h.HttpMethods.SequenceEqual(["GET"])));
        Assert.Equal("/StockMovements/Create", StockMovementFormViewModel.ForEntry(new QueryCollection()).PostUrl);
        Assert.Equal(MovementRules.EntryTypes, StockMovementFormViewModel.ForEntry(new QueryCollection()).AllowedTypes);
    }

    [Fact]
    public void A_correction_page_without_a_usable_history_row_draws_no_form()
    {
        Assert.True(StockMovementFormViewModel.ForCorrection(new QueryCollection()).OriginalMissing);
        var reversalRow = Query(("id", Guid.NewGuid().ToString()), ("itemId", Item), ("skuId", SkuId), ("skuLevel", "Lsku"), ("type", "REVERSAL"), ("quantity", "1"), ("uomId", "EA"));
        Assert.True(StockMovementFormViewModel.ForCorrection(reversalRow).OriginalMissing);
        var manualRow = Query(("id", Guid.NewGuid().ToString()), ("itemId", Item), ("skuId", SkuId), ("skuLevel", "Lsku"), ("type", "SCRAP"), ("quantity", "1"), ("uomId", "EA"));
        var model = StockMovementFormViewModel.ForCorrection(manualRow);
        Assert.False(model.OriginalMissing);
        Assert.NotEqual(model.IdempotencyKey, model.ReversalKey);
        Assert.Equal("Lsku:" + SkuId, model.Sku);
    }

    // ── lookups ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>WP-0173-PM1: the SKUs of one product are the Active stockable rows of that itemId, from the contract search only.</summary>
    [Fact]
    public async Task The_SKU_list_of_a_product_is_its_active_stockable_rows_from_the_contract_search_only()
    {
        const string gsku = "aaaaaaaa-0000-0000-0000-000000000001";
        var gateway = new FakeGateway();
        gateway.On("/api/product-master/stockable-items", 200, $$$"""
            {"items":[
              {"skuId":"{{{gsku}}}","skuLevel":"Gsku","itemId":"{{{Item}}}","canonicalCode":"GS-1","name":"Amox","lifecycleStatus":"Active"},
              {"skuId":"{{{SkuId}}}","skuLevel":"Lsku","itemId":"{{{Item}}}","canonicalCode":"LS-TR-1","name":"Amox TR","lifecycleStatus":"Active"},
              {"skuId":"{{{Guid.NewGuid()}}}","skuLevel":"Lsku","itemId":"{{{Guid.NewGuid()}}}","canonicalCode":"LS-X","name":"Other","lifecycleStatus":"Active"}
            ],"page":1,"pageSize":100,"totalCount":3,"contractVersion":"v1.1"}
            """);

        var json = Body(await Movements(gateway, ReadAndCreate, ("itemId", Item)).Lookups("skus", null, null, CancellationToken.None));

        Assert.Equal(["Gsku:" + gsku, "Lsku:" + SkuId], json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("value").GetString()));
        Assert.True(json.GetProperty("available").GetBoolean());
        Assert.All(gateway.Requests, r => Assert.Contains(ProductPickerSource.AllowedPaths, p => r.PathAndQuery.StartsWith(p + "?", StringComparison.Ordinal)));
        Assert.Single(gateway.Requests);
    }

    [Fact]
    public async Task Target_locations_are_the_active_stockable_ones_anywhere_under_the_warehouse()
    {
        const string zone = "55555555-5555-5555-5555-555555555555";
        const string inactiveZone = "66666666-0000-0000-0000-000000000006";
        var gateway = new FakeGateway();
        gateway.On("/api/locations/search", 200, $$"""
            {"items":[
              {"locationId":"{{zone}}","code":"Z-A","name":"Zone A","parentId":"{{Wh1}}","status":"Active","isStockable":false},
              {"locationId":"{{Bin1}}","code":"A-01","name":"","parentId":"{{zone}}","status":"Active","isStockable":true},
              {"locationId":"{{Bin2}}","code":"A-02","name":"","parentId":"{{zone}}","status":"Archived","isStockable":true},
              {"locationId":"{{inactiveZone}}","code":"Z-B","name":"","parentId":"{{Wh1}}","status":"Inactive","isStockable":false},
              {"locationId":"77777777-0000-0000-0000-000000000007","code":"B-01","name":"","parentId":"{{inactiveZone}}","status":"Active","isStockable":true},
              {"locationId":"88888888-0000-0000-0000-000000000008","code":"C-01","name":"","parentId":"{{Wh2}}","status":"Active","isStockable":true}
            ],"total":6,"page":1,"pageSize":200}
            """);

        var json = Body(await Movements(gateway, ReadAndCreate).Lookups("targetlocations", null, Wh1, CancellationToken.None));
        var warehouses = await Movements(gateway, ReadAndCreate).Lookups("targetwarehouses", null, null, CancellationToken.None);

        Assert.Equal(["A-01", "B-01"], json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("text").GetString()));
        Assert.Contains(gateway.Requests, r => r.PathAndQuery == "/api/locations/search?page=1&pageSize=200&type=Warehouse&status=Active");
        Assert.IsType<JsonResult>(warehouses);
    }

    [Theory]
    [InlineData("2", "KOLI", "24")]
    [InlineData("1", "PALET", "480")]
    [InlineData("7", "ADET", "7")]
    [InlineData("1", "DESTE", null)]
    public void The_base_quantity_hint_follows_the_declared_conversions_like_the_service(string quantity, string unit, string? expected)
    {
        var facts = new ProductFacts(true, true, false, "ADET", ["ADET", "KOLI", "PALET", "DESTE"],
            [new UomEdge("KOLI", "ADET", 12m, 1m), new UomEdge("PALET", "KOLI", 40m, 1m), new UomEdge("DESTE", "PAKET", 10m, 1m)]);
        Assert.Equal(expected, MovementFormSources.ToBase(quantity, unit, facts));
    }

    [Fact]
    public async Task Without_a_product_master_route_the_form_says_product_data_is_unavailable_and_offers_no_unit()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/product-master", 404, string.Empty);

        var json = Body(await Movements(gateway, ReadAndCreate, ("itemId", Item), ("sku", Sku)).Lookups("product", null, null, CancellationToken.None));

        Assert.False(json.GetProperty("available").GetBoolean());
        Assert.Equal("ProductFactsUnavailable", json.GetProperty("message").GetString());
        Assert.Equal(0, json.GetProperty("units").GetArrayLength());
    }

    [Fact]
    public async Task The_stock_effect_is_before_and_after_in_decimal_on_the_server()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/product-master/products", 200, $$"""{"itemId":"{{Item}}","baseUomId":"ADET","lotControlled":true,"serialControlled":false}""");
        gateway.On("/api/product-master/skus", 200, """{"baseUomId":"ADET","conversions":[{"fromUom":"KOLI","toUom":"ADET","numerator":"12","denominator":"1"}]}""");
        gateway.On("/api/inventory/balance", 200, $$"""{"rows":[{"warehouseId":"{{Wh1}}","locationId":"{{Bin1}}","lotId":"L1","stockStatus":"AVAILABLE","onHand":"30.5","baseUomId":"ADET"}],"total":1}""");
        gateway.On("/api/locations", 404, string.Empty);

        var json = Body(await Movements(gateway, ReadAndCreate, ("itemId", Item), ("sku", Sku), ("type", "SCRAP"), ("quantity", "2"), ("uomId", "KOLI"),
            ("source", $"{Wh1}|{Bin1}|AVAILABLE|L1")).Lookups("effect", null, null, CancellationToken.None));

        Assert.Equal("24", json.GetProperty("baseQuantity").GetString());
        var side = Assert.Single(json.GetProperty("sides").EnumerateArray());
        Assert.Equal("30.5", side.GetProperty("before").GetString());
        Assert.Equal("6.5", side.GetProperty("after").GetString());
        Assert.False(side.GetProperty("negative").GetBoolean());
    }

    // ── WP-0173-PM1: PRODUCT-MASTER v1.1 flags on the form ─────────────────────────────────────────────────

    private static FakeGateway V11(string skuBody, string? productBody = null)
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/product-master/skus/{SkuId}/uom", 200, """{"baseUomId":"ADET","conversions":[]}""");
        gateway.On($"/api/product-master/skus/{SkuId}", 200, skuBody);
        if (productBody is not null) gateway.On("/api/product-master/products", 200, productBody);
        return gateway;
    }

    [Theory]
    [InlineData("""{"skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Lsku","lotControlled":null,"serialControlled":false}""")]
    [InlineData("""{"skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Lsku","serialControlled":false}""")]
    [InlineData("""{"skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Lsku","lotControlled":"yes","serialControlled":false}""")]
    [InlineData("""{"skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Lsku","lotControlled":false,"serialControlled":null}""")]
    public async Task An_unknown_flag_on_the_sku_blocks_the_form(string skuBody)
    {
        var gateway = V11(skuBody);
        var json = Body(await Movements(gateway, ReadAndCreate, ("itemId", Item), ("sku", Sku)).Lookups("product", null, null, CancellationToken.None));

        Assert.True(json.GetProperty("available").GetBoolean());
        Assert.True(json.GetProperty("blocked").GetBoolean());
        Assert.Equal("ProductFlagsUnknown", json.GetProperty("blockedMessage").GetString());
        Assert.Contains(gateway.Requests, r => r.PathAndQuery == $"/api/product-master/skus/{SkuId}?level=Lsku");
        Assert.DoesNotContain(gateway.Requests, r => r.PathAndQuery.StartsWith("/api/product-master/products", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_sku_flags_drive_the_form_and_the_product_flags_are_not_read()
    {
        var gateway = V11("""{"skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Lsku","lotControlled":true,"serialControlled":false}""",
            $$"""{"itemId":"{{Item}}","lotControlled":false,"serialControlled":true}""");
        var json = Body(await Movements(gateway, ReadAndCreate, ("itemId", Item), ("sku", Sku)).Lookups("product", null, null, CancellationToken.None));

        Assert.False(json.GetProperty("blocked").GetBoolean());
        Assert.True(json.GetProperty("lotControlled").GetBoolean());
        Assert.False(json.GetProperty("serialControlled").GetBoolean());
        Assert.DoesNotContain(gateway.Requests, r => r.PathAndQuery.StartsWith("/api/product-master/products", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("null", true)]
    [InlineData("false", false)]
    public async Task A_Gsku_needs_a_product_that_is_explicitly_serial_free(string productSerial, bool blocked)
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/product-master/skus/{SkuId}/uom", 200, """{"baseUomId":"ADET","conversions":[]}""");
        gateway.On($"/api/product-master/skus/{SkuId}", 200, """{"skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Gsku","lotControlled":false,"serialControlled":null}""");
        gateway.On("/api/product-master/products", 200, $$"""{"itemId":"{{Item}}","serialControlled":{{productSerial}}}""");

        var json = Body(await Movements(gateway, ReadAndCreate, ("itemId", Item), ("sku", "Gsku:" + SkuId)).Lookups("product", null, null, CancellationToken.None));

        Assert.Equal(blocked, json.GetProperty("blocked").GetBoolean());
        if (blocked) Assert.Equal("ProductLevelNotStockable", json.GetProperty("blockedMessage").GetString());
    }

    [Fact]
    public async Task An_Item_level_sku_is_a_valid_form_choice()
    {
        var gateway = V11("""{"skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Item","lotControlled":true,"serialControlled":false}""");
        var json = Body(await Movements(gateway, ReadAndCreate, ("itemId", Item), ("sku", "Item:" + SkuId)).Lookups("product", null, null, CancellationToken.None));

        Assert.False(json.GetProperty("blocked").GetBoolean());
        Assert.Contains(gateway.Requests, r => r.PathAndQuery == $"/api/product-master/skus/{SkuId}?level=Item");
    }

    [Fact]
    public void The_form_script_does_not_send_a_blocked_movement()
    {
        var script = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "Inventory", "StockMovements", "form.js"));
        Assert.Contains("} else if (facts.blocked) {", script, StringComparison.Ordinal);
        Assert.Contains("if (state.facts?.blocked) errors.sku = state.facts.blockedMessage", script, StringComparison.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "frontend", "Diten.Web");
    }

    // ── harness ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A valid form of each type: an inbound by default, adapted by the test with <c>with</c>.</summary>
    private sealed record FormValues(
        string? IdempotencyKey, string? MovementType, string? ItemId, string? Sku, string? Source, string? ToWarehouseId,
        string? ToLocationId, string? ToStockStatus, string? LotId, string? SerialNumbers, string? Quantity, string? UomId,
        string? DocumentNo, string? TransactionDate, string? PostingDate, bool Entry)
    {
        public MovementSubmitViewModel ToModel() => new()
        {
            IdempotencyKey = IdempotencyKey, MovementType = MovementType, ItemId = ItemId, Sku = Sku, Source = Source,
            ToWarehouseId = ToWarehouseId, ToLocationId = ToLocationId, ToStockStatus = ToStockStatus, LotId = LotId,
            SerialNumbers = SerialNumbers, Quantity = Quantity, UomId = UomId, DocumentNo = DocumentNo,
            TransactionDate = TransactionDate, PostingDate = PostingDate, Entry = Entry
        };

        public static implicit operator MovementSubmitViewModel(FormValues values) => values.ToModel();
    }

    private static FormValues Form(string type) =>
        new(Guid.NewGuid().ToString(), type, Item, Sku, null, Wh1, Bin1, "QUALITY_INSPECTION", "lot-1", null, "7.5", "ADET", "SAY-1", Now, Now, false);

    private static CorrectionSubmitViewModel Correction(FormValues movement) => new()
    {
        TransactionId = Guid.NewGuid().ToString("D"),
        TransactionNumber = "INV-OLD",
        ReversalKey = Guid.NewGuid().ToString(),
        ReversalAt = "2026-10-02T08:00:00Z",
        OriginalItemId = Item,
        OriginalSkuId = SkuId,
        OriginalSkuLevel = "Lsku",
        OriginalQuantity = "7.5",
        OriginalUomId = "ADET",
        Movement = movement.ToModel()
    };

    private static int Status(IActionResult result) => Assert.IsType<JsonResult>(result).StatusCode ?? 200;

    private static JsonElement Body(IActionResult result) =>
        JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement;

    private static IQueryCollection Query(params (string Key, string Value)[] pairs) =>
        new QueryCollection(pairs.ToDictionary(p => p.Key, p => new StringValues(p.Value)));

    private static StockMovementsController Movements(FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
    {
        var controller = new StockMovementsController(new HttpClient(gateway),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build(),
            new Permissions(permissions), new KeyLocalizer<StockMovementsIndex>(), NullLogger<StockMovementsController>.Instance);
        // The token names its company (W-0173-02), so no selector cookie is needed.
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "55555555-0000-0000-0000-000000000001"), new("legal_entity_id", Guid.NewGuid().ToString()) };
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Query = Query(query);
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new MemoryTempData());
        return controller;
    }

    private sealed class MemoryTempData : ITempDataProvider
    {
        private IDictionary<string, object> _values = new Dictionary<string, object>();
        public IDictionary<string, object> LoadTempData(HttpContext context) => _values;
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) => _values = values;
    }

    private sealed class Permissions(string[] keys) : IPermissionSnapshot
    {
        public bool Has(string permissionKey) => keys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<string> Keys => keys;
    }

    /// <summary>Answers with the KEY, so a test can assert which resx sentence was chosen.</summary>
    private sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class FakeGateway : HttpMessageHandler
    {
        private readonly List<(string Prefix, int Status, string Body)> _routes = [];
        private Func<string, (int Status, string Body)>? _movements;

        public List<(string Method, string PathAndQuery, string? IdempotencyKey, string Body)> Requests { get; } = [];

        public void On(string prefix, int status, string body) => _routes.Add((prefix, status, body));

        /// <summary>POST /api/inventory/movements answered by Idempotency-Key (reversal vs movement).</summary>
        public void OnMovement(Func<string, (int Status, string Body)> answer) => _movements = answer;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pathAndQuery = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
            lock (Requests) Requests.Add((request.Method.Method, pathAndQuery, key, body));

            if (_movements is not null && request.Method == HttpMethod.Post && pathAndQuery == "/api/inventory/movements")
            {
                var (status, text) = _movements(key ?? string.Empty);
                return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
            }

            var route = _routes.FirstOrDefault(r => pathAndQuery.StartsWith(r.Prefix, StringComparison.Ordinal));
            return route.Prefix is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage((HttpStatusCode)route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") };
        }
    }
}
