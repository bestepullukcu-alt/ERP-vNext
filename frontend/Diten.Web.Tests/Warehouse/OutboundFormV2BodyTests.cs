using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.Web.Controllers;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Warehouse;
using Diten.Web.Tests.Contracts;
using Xunit;

namespace Diten.Web.Tests.Warehouse;

/// <summary>
/// MOD-0178-FU01 DL-1, AC-9 (WP-0178-FU01-DL1 §10 S-4 = (b), CT decision 2026-11-13): the save body the SERVICE receives is built by the Web
/// proxy (<see cref="OutboundShipmentsController.CreateBody"/>), so the frozen-schema check runs here, on that production builder, serialized the
/// way the Gateway call serializes it (<see cref="InventoryGatewayClient.Json"/>), against the FROZEN
/// <c>docs/analysis/contracts/wms-bundle.openapi.yaml</c> <c>CreateOutboundShipmentRequest</c> / <c>PreviewOutboundAllocationRequest</c>
/// (<c>additionalProperties: false</c>; <c>lotOverride</c> only on a line with a chosen lot; no <c>null</c> member). The browser half (the lines
/// in table order) is vitest <c>warehouse-fu01-outbound-lines.test.js</c>. Deviation from FU01 pack §17 ":738" ("vitest") is the CT decision.
/// </summary>
public sealed class OutboundFormV2BodyTests
{
    private static readonly WebContractSchemas.ContractFile Wms = new("wms-bundle.openapi.yaml", _ => new Dictionary<string, JsonNode>());

    private const string Warehouse = "11111111-1111-4111-8111-111111111111";
    private const string Item = "33333333-3333-4333-8333-333333333333";
    private const string Sku = "44444444-4444-4444-8444-444444444444";
    private const string LotB = "bbbbbbbb-0000-4000-8000-000000000002";

    /// <summary>What form.js posts for three lines: FEFO, a chosen lot with its reason, a chosen lot whose reason is empty (Warn).</summary>
    private static OutboundWriteRequest Model() => new()
    {
        IdempotencyKey = "6f1c2a8e-1b7d-4c55-9a43-0d6b8f1e2c3a",
        WarehouseId = Warehouse,
        OrderRef = "SO-2026-0042",
        ShipTo = new OutboundAddressRequest { Name = "Klinik", Country = "tr", City = "İstanbul" },
        Lines =
        [
            new OutboundLineRequest { OrderLineId = "1", ItemId = Item, SkuId = Sku, SkuLevel = "Lsku", UomId = "EA", Quantity = "2" },
            new OutboundLineRequest
            {
                OrderLineId = "2", ItemId = Item, SkuId = Sku, SkuLevel = "Lsku", UomId = "EA", Quantity = "1.5",
                LotOverride = new OutboundLotOverrideRequest { LotId = LotB, FefoBypassReason = "Müşteri bu lotu istedi" }
            },
            new OutboundLineRequest
            {
                OrderLineId = "3", ItemId = Item, SkuId = Sku, SkuLevel = "Lsku", UomId = "EA", Quantity = "4",
                LotOverride = new OutboundLotOverrideRequest { LotId = LotB, FefoBypassReason = null }
            }
        ]
    };

    /// <summary>The create body exactly as <c>Create</c> sends it: <c>CreateBody</c> + the canonical key, serialized with the Gateway options.</summary>
    private static string CreateWire(OutboundWriteRequest model)
    {
        var body = OutboundShipmentsController.CreateBody(model, requireShipTo: true);
        Assert.NotNull(body);
        body!["idempotencyKey"] = Guid.Parse(model.IdempotencyKey!).ToString("D");
        return JsonSerializer.Serialize(body, InventoryGatewayClient.Json);
    }

    [Fact]
    public void AC9_the_create_body_is_valid_against_the_frozen_CreateOutboundShipmentRequest()
    {
        var wire = CreateWire(Model());
        var errors = Wms.Validate("CreateOutboundShipmentRequest", wire);
        Assert.True(errors.Count == 0, $"the proxy's create body breaks the frozen schema: {string.Join("; ", errors)}\n{wire}");
    }

    [Fact]
    public void AC9_the_preview_body_is_valid_against_the_frozen_PreviewOutboundAllocationRequest()
    {
        var model = Model();
        model.ShipTo = null; // the panel / table preview without a recipient yet
        var body = OutboundShipmentsController.CreateBody(model, requireShipTo: false);
        Assert.NotNull(body);
        var wire = JsonSerializer.Serialize(body, InventoryGatewayClient.Json);
        var errors = Wms.Validate("PreviewOutboundAllocationRequest", wire);
        Assert.True(errors.Count == 0, $"the proxy's preview body breaks the frozen schema: {string.Join("; ", errors)}\n{wire}");
    }

    [Fact]
    public void AC9_the_lines_keep_the_table_order_lotOverride_only_where_a_lot_was_chosen_and_no_member_is_null()
    {
        var root = JsonNode.Parse(CreateWire(Model()))!.AsObject();
        var lines = root["lines"]!.AsArray();
        Assert.Equal(["1", "2", "3"], lines.Select(l => l!["orderLineId"]!.GetValue<string>()));
        Assert.Equal([false, true, true], lines.Select(l => l!.AsObject().ContainsKey("lotOverride")));
        Assert.Equal("Müşteri bu lotu istedi", lines[1]!["lotOverride"]!["fefoBypassReason"]!.GetValue<string>());
        Assert.False(lines[2]!["lotOverride"]!.AsObject().ContainsKey("fefoBypassReason")); // an empty reason is not sent (Warn)

        var nulls = new List<string>();
        void Walk(JsonNode? node, string at)
        {
            switch (node)
            {
                case null: nulls.Add(at); break;
                case JsonObject obj: foreach (var (key, value) in obj) Walk(value, $"{at}.{key}"); break;
                case JsonArray array: for (var i = 0; i < array.Count; i++) Walk(array[i], $"{at}[{i}]"); break;
            }
        }

        Walk(root, "$");
        Assert.True(nulls.Count == 0, $"null members in the create body: {string.Join(", ", nulls)}");
    }

    [Fact]
    public void AC9_the_schema_check_is_not_vacuous_a_member_the_contract_does_not_name_fails()
    {
        var node = JsonNode.Parse(CreateWire(Model()))!.AsObject();
        node["lines"]![0]!.AsObject()["lotOverride"] = null;
        Assert.NotEmpty(Wms.Validate("CreateOutboundShipmentRequest", node.ToJsonString()));
        var extra = JsonNode.Parse(CreateWire(Model()))!.AsObject();
        extra["customerRef"] = "x"; // DL-2's member is not in WMS-BUNDLE 1.1.0
        Assert.NotEmpty(Wms.Validate("CreateOutboundShipmentRequest", extra.ToJsonString()));
    }
}
