using Diten.ProcurementService.Api.Tests.RequisitionPo;
using Diten.ProcurementService.Api.Tests.Sourcing;
using Diten.ProcurementService.Application.Features.Grn;
using Diten.ProcurementService.Application.Features.Grn.Commands;
using Diten.ProcurementService.Application.Features.Grn.Handlers.CommandHandlers;
using Diten.ProcurementService.Application.Features.Grn.Handlers.QueryHandlers;
using Diten.ProcurementService.Application.Features.Grn.Queries;
using Diten.ProcurementService.Application.Features.Grn.Validators;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Xunit;
using GoodsReceiptEntity = Diten.ProcurementService.Domain.Entities.GoodsReceipt;
using PurchaseOrderEntity = Diten.ProcurementService.Domain.Entities.PurchaseOrder;

namespace Diten.ProcurementService.Api.Tests.Grn;

/// <summary>
/// MOD-0142 Receiving (GRN) backend behaviour tests — THE CRITICAL SLICE (INVENTORY posting). Each pins a
/// contract/pack rule against PRODUCTION handlers/validators over the tenant+LE-scoped FakeGrnRepository, the
/// PRODUCT-MASTER consume seam (FakeProductReferenceValidator, fail-closed), the upstream PO seam
/// (FakePurchaseOrderRepository) and the INVENTORY posting seam (FakeInventoryPostingClient). The Mongo filter is
/// pinned in GrnRepository.cs. Central rule: SHADOW STOCK FORBIDDEN — stock only moves via the INVENTORY seam
/// (GOODS_RECEIPT_PO); the GRN persists only the returned inventoryTransactionId, never a balance.
/// </summary>
public sealed class GrnBackendTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid LeA = Guid.NewGuid();
    private static readonly Guid LeB = Guid.NewGuid();

    private const string Item1 = "b1f2c3d4-0000-0000-0000-000000000001";
    private const string Item2 = "b1f2c3d4-0000-0000-0000-000000000002";
    private const string Sku1 = "c3d4e5f6-0000-0000-0000-000000000001";

    private sealed class Fixture
    {
        public List<GoodsReceiptEntity> GrnStore = new();
        public List<PurchaseOrderEntity> PoStore = new();
        public required IGrnRepository Grns;
        public required IPurchaseOrderRepository PurchaseOrders;
        public required FakeProductReferenceValidator Products;
        public required FakeInventoryPostingClient Inventory;

        public RecordGrnHandler Record() => new(Grns, PurchaseOrders, Products, Inventory);
        public ReverseGrnHandler Reverse() => new(Grns, Inventory);
        public DeleteGrnHandler Delete() => new(Grns);
        public BulkDeleteGrnHandler BulkDelete() => new(Grns);
    }

    private static Fixture NewFixture(IEnumerable<string>? knownItems = null, Guid? le = null)
    {
        var grnStore = new List<GoodsReceiptEntity>();
        var poStore = new List<PurchaseOrderEntity>();
        var legalEntity = le ?? LeA;
        return new Fixture
        {
            GrnStore = grnStore,
            PoStore = poStore,
            Grns = new FakeGrnRepository(grnStore, TenantA, legalEntity),
            PurchaseOrders = new FakePurchaseOrderRepository(poStore, TenantA, legalEntity),
            Products = new FakeProductReferenceValidator(knownItems ?? new[] { Item1, Item2 }),
            Inventory = new FakeInventoryPostingClient()
        };
    }

    private static async Task SeedPo(Fixture f, string poId)
        => await f.PurchaseOrders.CreateAsync(new PurchaseOrderEntity { PoId = poId, SupplierId = "SUP-1", Currency = "TRY" }, default);

    private static GrnLineInput Line(
        string itemId = Item1,
        string skuId = Sku1,
        string quantity = "100.000",
        string skuLevel = "Lsku",
        string toStockStatus = "QUALITY_INSPECTION",
        string? lotNumber = "L2026-0455",
        string? poLineId = "3")
        => new(poLineId, itemId, skuId, skuLevel, lotNumber, null, quantity, "BOX", toStockStatus);

    private static RecordGrnCommand NewRecord(
        string? poId = null,
        string warehouseId = "wh-01",
        string? locationId = "loc-A-12-3",
        List<GrnLineInput>? lines = null,
        string? idem = null)
        => new(poId, warehouseId, locationId, lines ?? new List<GrnLineInput> { Line() }, null, null, idem);

    // ── G2A / NO SHADOW STOCK (CRITICAL): recordGrn posts to INVENTORY (GOODS_RECEIPT_PO) + stores txn id ──────
    [Fact]
    public async Task Record_grn_posts_to_inventory_and_persists_transaction_id()
    {
        var f = NewFixture();

        var result = await f.Record().Handle(NewRecord(idem: "grn-abc"), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(GrnStatus.Posted, result.Data!.Status); // Posted only AFTER a successful INVENTORY post
        Assert.StartsWith("GRN-", result.Data.GrnId);

        // The INVENTORY seam was called once per line, with the canonical GOODS_RECEIPT_PO movement type + GRN source.
        Assert.Single(f.Inventory.Calls);
        var movement = f.Inventory.Calls[0];
        Assert.Equal("GOODS_RECEIPT_PO", movement.MovementType);
        Assert.Equal("MOD-0142", movement.SourceModule);
        Assert.Equal("GOODS_RECEIPT", movement.SourceType);
        Assert.Equal(result.Data.GrnId, movement.SourceDocumentId); // sourceDocumentId = GrnId (§19)
        Assert.Equal("3", movement.SourceLineId);                    // sourceLineId = poLineId

        // The returned inventoryTransactionId is persisted on the GRN line (a REFERENCE — inventory truth is 0173).
        var stored = f.GrnStore.Single();
        Assert.False(string.IsNullOrWhiteSpace(stored.Lines[0].InventoryTransactionId));
        Assert.StartsWith("txn-", stored.Lines[0].InventoryTransactionId);
        Assert.Equal(stored.Lines[0].InventoryTransactionId, result.Data.Lines[0].InventoryTransactionId);

        // NO SHADOW STOCK (vacuity-style): neither the GRN header nor the line type owns any balance/on-hand/ledger
        // property — the only stock number that exists lives in the INVENTORY seam call, not on the GRN.
        AssertNoBalanceField(typeof(GoodsReceipt));
        AssertNoBalanceField(typeof(GrnLine));
    }

    private static void AssertNoBalanceField(Type type)
    {
        foreach (var p in type.GetProperties())
        {
            var n = p.Name.ToLowerInvariant();
            Assert.False(
                n.Contains("balance") || n == "onhand" || n.Contains("ledger") || n.Contains("available") || n.Contains("stockonhand"),
                $"{type.Name}.{p.Name} looks like a shadow-stock balance — forbidden (MOD-0142 §8). Inventory truth is MOD-0173.");
        }
    }

    // ── G2A: GoodsReceived event emitted per line (contract GoodsReceivedEvent, includes inventoryTransactionId) ──
    [Fact]
    public async Task Record_grn_emits_goods_received_event_per_line()
    {
        var f = NewFixture();
        var lines = new List<GrnLineInput> { Line(itemId: Item1, poLineId: "1"), Line(itemId: Item2, poLineId: "2") };

        var result = await f.Record().Handle(NewRecord(poId: null, lines: lines), default);
        Assert.Equal(201, result.StatusCode);

        var stored = f.GrnStore.Single();
        Assert.Equal(2, stored.EmittedEvents.Count);
        Assert.All(stored.EmittedEvents, e =>
        {
            Assert.Equal(stored.GrnId, e.GrnId);
            Assert.False(string.IsNullOrWhiteSpace(e.InventoryTransactionId)); // event carries the txn id (0143/0175 consume)
        });
        Assert.Equal(2, f.Inventory.CallCount); // one INVENTORY movement per line
    }

    // ── IDEMPOTENCY replay → no duplicate INVENTORY movement ────────────────────────────────────────────────
    [Fact]
    public async Task Record_grn_is_idempotent_no_duplicate_movement()
    {
        var f = NewFixture();

        var first = await f.Record().Handle(NewRecord(idem: "grn-1"), default);
        var replay = await f.Record().Handle(NewRecord(idem: "grn-1"), default);

        Assert.Equal(first.Data!.GrnId, replay.Data!.GrnId); // same GRN returned
        Assert.Single(f.GrnStore);                            // no duplicate record
        Assert.Equal(1, f.Inventory.CallCount);               // seam called ONCE per unique Idempotency-Key (no 2nd movement)
    }

    // ── FAIL-CLOSED: unknown item (0290 seam) → 422 UNKNOWN_ITEM + NO inventory post ─────────────────────────
    [Fact]
    public async Task Record_grn_unknown_item_is_422_fail_closed_no_post()
    {
        var f = NewFixture(knownItems: new[] { Item1 }); // Item2 NOT known

        var unknown = await f.Record().Handle(
            NewRecord(lines: new List<GrnLineInput> { Line(itemId: Item2) }), default);

        Assert.False(unknown.IsSuccessful);
        Assert.Equal(422, unknown.StatusCode);
        Assert.Contains("UNKNOWN_ITEM", unknown.Errors);
        Assert.Empty(f.GrnStore);              // fail-closed: no GRN written
        Assert.Equal(0, f.Inventory.CallCount); // silent-pass forbidden: NO inventory movement on a rejected item

        // Vacuity control: a known item posts and succeeds in the same setup.
        var ok = await f.Record().Handle(NewRecord(lines: new List<GrnLineInput> { Line(itemId: Item1) }), default);
        Assert.Equal(201, ok.StatusCode);
        Assert.Equal(1, f.Inventory.CallCount);
    }

    // ── VALIDATION: warehouse/lines/decimal/enum + vacuity ──────────────────────────────────────────────────
    [Fact]
    public void Validator_rejects_missing_fields_and_bad_values()
    {
        var validator = new RecordGrnValidator();

        Assert.False(validator.Validate(NewRecord(warehouseId: "")).IsValid);                        // missing warehouse
        Assert.False(validator.Validate(NewRecord(lines: new List<GrnLineInput>())).IsValid);        // empty lines
        Assert.False(validator.Validate(NewRecord(lines: new List<GrnLineInput> { Line(quantity: "1.0e3") })).IsValid);      // float qty
        Assert.False(validator.Validate(NewRecord(lines: new List<GrnLineInput> { Line(quantity: "-5") })).IsValid);         // non-positive qty
        Assert.False(validator.Validate(NewRecord(lines: new List<GrnLineInput> { Line(skuLevel: "BOGUS") })).IsValid);      // bad skuLevel
        Assert.False(validator.Validate(NewRecord(lines: new List<GrnLineInput> { Line(toStockStatus: "NOPE") })).IsValid);  // bad toStockStatus

        // Vacuity control (K3): a well-formed command passes.
        Assert.True(validator.Validate(NewRecord()).IsValid);
    }

    [Fact]
    public async Task Record_handler_returns_422_on_empty_lines_and_missing_warehouse()
    {
        var f = NewFixture();

        var emptyLines = await f.Record().Handle(NewRecord(lines: new List<GrnLineInput>()), default);
        Assert.Equal(422, emptyLines.StatusCode);
        Assert.Contains("VALIDATION_FAILED", emptyLines.Errors);

        var noWarehouse = await f.Record().Handle(NewRecord(warehouseId: ""), default);
        Assert.Equal(422, noWarehouse.StatusCode);

        Assert.Equal(0, f.Inventory.CallCount); // nothing posted on a rejected request

        // Vacuity control: a valid record succeeds in the same setup.
        var ok = await f.Record().Handle(NewRecord(), default);
        Assert.Equal(201, ok.StatusCode);
    }

    // ── poId given must reference an existing PO (intra-service) ─────────────────────────────────────────────
    [Fact]
    public async Task Record_grn_unknown_po_is_422()
    {
        var f = NewFixture();

        var unknown = await f.Record().Handle(NewRecord(poId: "PO-NONE"), default);
        Assert.False(unknown.IsSuccessful);
        Assert.Equal(422, unknown.StatusCode);
        Assert.Contains("UNKNOWN_PO", unknown.Errors);
        Assert.Equal(0, f.Inventory.CallCount);

        // Control: once the PO exists, the same record succeeds and posts to inventory.
        await SeedPo(f, "PO-NONE");
        var ok = await f.Record().Handle(NewRecord(poId: "PO-NONE"), default);
        Assert.Equal(201, ok.StatusCode);
        Assert.Equal("PO-NONE", ok.Data!.PoId);
    }

    // ── getGrn: unknown → 404 UNKNOWN_GRN ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Get_unknown_grn_is_404()
    {
        var f = NewFixture();
        var result = await new GetGrnByIdHandler(f.Grns).Handle(new GetGrnByIdQuery("GRN-NONE"), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("UNKNOWN_GRN", result.Errors);
    }

    // ── REVERSAL: Posted → Reversed, posts INVENTORY REVERSAL; replay → no 2nd reversal ─────────────────────
    [Fact]
    public async Task Reverse_grn_posts_reversal_and_is_idempotent()
    {
        var f = NewFixture();
        var lines = new List<GrnLineInput> { Line(itemId: Item1, poLineId: "1"), Line(itemId: Item2, poLineId: "2") };
        var grnId = (await f.Record().Handle(NewRecord(lines: lines), default)).Data!.GrnId;
        var postsAfterReceipt = f.Inventory.CallCount; // 2 GOODS_RECEIPT_PO

        var reversed = await f.Reverse().Handle(new ReverseGrnCommand(grnId, "rev-1"), default);
        Assert.True(reversed.IsSuccessful);
        Assert.Equal(GrnStatus.Reversed, reversed.Data!.Status);

        // Each line posted a REVERSAL movement.
        var reversalCalls = f.Inventory.Calls.Skip(postsAfterReceipt).ToList();
        Assert.Equal(2, reversalCalls.Count);
        Assert.All(reversalCalls, m => Assert.Equal("REVERSAL", m.MovementType));

        // Idempotent replay (same key, already Reversed) → NO second REVERSAL movement.
        var callsBeforeReplay = f.Inventory.CallCount;
        var replay = await f.Reverse().Handle(new ReverseGrnCommand(grnId, "rev-1"), default);
        Assert.True(replay.IsSuccessful);
        Assert.Equal(GrnStatus.Reversed, replay.Data!.Status);
        Assert.Equal(callsBeforeReplay, f.Inventory.CallCount);
    }

    [Fact]
    public async Task Reverse_unknown_grn_is_404()
    {
        var f = NewFixture();
        var result = await f.Reverse().Handle(new ReverseGrnCommand("GRN-NONE", null), default);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("UNKNOWN_GRN", result.Errors);
    }

    // ── SOFT DELETE: a Posted GRN cannot be deleted (→ 409); a Draft can, and is then hidden ────────────────
    [Fact]
    public async Task Delete_posted_grn_is_409_but_draft_soft_deletes_and_hides()
    {
        var f = NewFixture();
        var posted = (await f.Record().Handle(NewRecord(), default)).Data!.GrnId;

        // Posted GRN → 409 (correction is via reverse, append-only; INVENTORY movement already exists).
        var del = await f.Delete().Handle(new DeleteGrnCommand(posted), default);
        Assert.False(del.IsSuccessful);
        Assert.Equal(409, del.StatusCode);
        Assert.Contains("INVALID_STATE", del.Errors);

        // A Draft GRN (seeded directly) soft-deletes and becomes invisible.
        var draft = await f.Grns.CreateAsync(new GoodsReceiptEntity { GrnId = "GRN-DRAFT", WarehouseId = "wh-01", Status = GrnStatus.Draft }, default);
        var delDraft = await f.Delete().Handle(new DeleteGrnCommand(draft.GrnId), default);
        Assert.True(delDraft.IsSuccessful);

        var read = await new GetGrnByIdHandler(f.Grns).Handle(new GetGrnByIdQuery("GRN-DRAFT"), default);
        Assert.Equal(404, read.StatusCode);
    }

    // ── TENANT + LE ISOLATION: cross-LE read → 404 (+ owning-LE control) ─────────────────────────────────────
    [Fact]
    public async Task Cross_legal_entity_read_returns_404()
    {
        var grnStore = new List<GoodsReceiptEntity>();
        var poStore = new List<PurchaseOrderEntity>();
        var repoA = new FakeGrnRepository(grnStore, TenantA, LeA);
        var repoB = new FakeGrnRepository(grnStore, TenantA, LeB);
        var poA = new FakePurchaseOrderRepository(poStore, TenantA, LeA);
        var products = new FakeProductReferenceValidator(new[] { Item1, Item2 });
        var inventory = new FakeInventoryPostingClient();

        var created = await new RecordGrnHandler(repoA, poA, products, inventory).Handle(NewRecord(), default);
        var grnId = created.Data!.GrnId;

        // Same store, same tenant, DIFFERENT legal entity → invisible.
        var readB = await new GetGrnByIdHandler(repoB).Handle(new GetGrnByIdQuery(grnId), default);
        Assert.False(readB.IsSuccessful);
        Assert.Equal(404, readB.StatusCode);

        // Control: the owning LE can read it (isolation is not vacuously hiding everything).
        var readA = await new GetGrnByIdHandler(repoA).Handle(new GetGrnByIdQuery(grnId), default);
        Assert.True(readA.IsSuccessful);
    }

    // ── TENANT + LE ISOLATION: cross-LE reverse is blocked (→ 404, no silent state change) ──────────────────
    [Fact]
    public async Task Cross_legal_entity_reverse_is_blocked()
    {
        var grnStore = new List<GoodsReceiptEntity>();
        var poStore = new List<PurchaseOrderEntity>();
        var repoA = new FakeGrnRepository(grnStore, TenantA, LeA);
        var repoB = new FakeGrnRepository(grnStore, TenantA, LeB);
        var poA = new FakePurchaseOrderRepository(poStore, TenantA, LeA);
        var products = new FakeProductReferenceValidator(new[] { Item1, Item2 });
        var inventory = new FakeInventoryPostingClient();

        var created = await new RecordGrnHandler(repoA, poA, products, inventory).Handle(NewRecord(), default);
        var grnId = created.Data!.GrnId;

        var reverseB = await new ReverseGrnHandler(repoB, inventory).Handle(new ReverseGrnCommand(grnId, null), default);
        Assert.False(reverseB.IsSuccessful);
        Assert.Equal(404, reverseB.StatusCode);

        // The record under LE-A is untouched (still Posted).
        var readA = await new GetGrnByIdHandler(repoA).Handle(new GetGrnByIdQuery(grnId), default);
        Assert.Equal(GrnStatus.Posted, readA.Data!.Status);
    }

    // ── BULK DELETE: only Draft removed ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task BulkDelete_removes_only_draft_grns()
    {
        var f = NewFixture();
        var posted = (await f.Record().Handle(NewRecord(), default)).Data!.GrnId;
        var draft = (await f.Grns.CreateAsync(new GoodsReceiptEntity { GrnId = "GRN-DRAFT", WarehouseId = "wh-01", Status = GrnStatus.Draft }, default)).GrnId;

        var result = await f.BulkDelete().Handle(new BulkDeleteGrnCommand(new List<string> { posted, draft }), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data); // only the Draft one deleted

        var list = await new GetGrnListHandler(f.Grns).Handle(new GetGrnListQuery(), default);
        Assert.DoesNotContain(list.Data!.Items, i => i.GrnId == draft);
        Assert.Contains(list.Data.Items, i => i.GrnId == posted);
    }
}
