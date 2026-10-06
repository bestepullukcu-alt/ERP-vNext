using System.Globalization;
using Diten.ProcurementService.Api.Tests.Sourcing;
using Diten.ProcurementService.Api.Tests.Suppliers;
using Diten.ProcurementService.Application.Features.PurchaseOrder;
using Diten.ProcurementService.Application.Features.PurchaseOrder.Commands;
using Diten.ProcurementService.Application.Features.PurchaseOrder.Handlers.CommandHandlers;
using Diten.ProcurementService.Application.Features.PurchaseOrder.Handlers.QueryHandlers;
using Diten.ProcurementService.Application.Features.PurchaseOrder.Queries;
using Diten.ProcurementService.Application.Features.PurchaseOrder.Validators;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Xunit;
using PurchaseOrderEntity = Diten.ProcurementService.Domain.Entities.PurchaseOrder;
using RequisitionEntity = Diten.ProcurementService.Domain.Entities.Requisition;

namespace Diten.ProcurementService.Api.Tests.RequisitionPo;

/// <summary>
/// MOD-0141 Purchase Order backend behaviour tests. Each pins a contract/pack rule against PRODUCTION
/// handlers/validators over the tenant+LE-scoped FakePurchaseOrderRepository + the real Supplier seam
/// (FakeSupplierRepository, fail-closed) + the requisition link seam (FakeRequisitionRepository) + the PRODUCT-MASTER
/// consume seam (FakeProductReferenceValidator). The Mongo filter itself is pinned in PurchaseOrderRepository.cs.
/// </summary>
public sealed class PurchaseOrderBackendTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid LeA = Guid.NewGuid();
    private static readonly Guid LeB = Guid.NewGuid();

    private const string Item1 = "b1f2c3d4-0000-0000-0000-000000000001";
    private const string Item2 = "b1f2c3d4-0000-0000-0000-000000000002";

    private sealed class Fixture
    {
        public List<PurchaseOrderEntity> PoStore = new();
        public List<RequisitionEntity> RequisitionStore = new();
        public List<Supplier> SupplierStore = new();
        public required IPurchaseOrderRepository PurchaseOrders;
        public required IRequisitionRepository Requisitions;
        public required ISupplierRepository Suppliers;
        public required FakeProductReferenceValidator Products;

        public CreatePurchaseOrderHandler Create() => new(PurchaseOrders, Suppliers, Requisitions, Products);
        public ApprovePurchaseOrderHandler Approve() => new(PurchaseOrders);
        public DeletePurchaseOrderHandler Delete() => new(PurchaseOrders);
        public BulkDeletePurchaseOrderHandler BulkDelete() => new(PurchaseOrders);
    }

    private static Fixture NewFixture(IEnumerable<string>? knownItems = null, Guid? le = null)
    {
        var poStore = new List<PurchaseOrderEntity>();
        var requisitionStore = new List<RequisitionEntity>();
        var supplierStore = new List<Supplier>();
        var legalEntity = le ?? LeA;
        return new Fixture
        {
            PoStore = poStore,
            RequisitionStore = requisitionStore,
            SupplierStore = supplierStore,
            PurchaseOrders = new FakePurchaseOrderRepository(poStore, TenantA, legalEntity),
            Requisitions = new FakeRequisitionRepository(requisitionStore, TenantA, legalEntity),
            Suppliers = new FakeSupplierRepository(supplierStore, TenantA, legalEntity),
            Products = new FakeProductReferenceValidator(knownItems ?? new[] { Item1, Item2 })
        };
    }

    private static async Task SeedSupplier(Fixture f, string supplierId)
        => await f.Suppliers.CreateAsync(new Supplier { SupplierId = supplierId, Name = supplierId, Status = SupplierStatus.Active }, default);

    private static async Task<string> SeedApprovedRequisition(Fixture f)
    {
        var req = await f.Requisitions.CreateAsync(new RequisitionEntity
        {
            RequisitionId = "REQ-APPR" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(),
            Status = RequisitionStatus.Approved,
            Lines = new List<RequisitionLine> { new() { ItemId = Item1, Quantity = "1", UomId = "BOX" } }
        }, default);
        return req.RequisitionId;
    }

    private static CreatePurchaseOrderCommand NewCreate(
        string supplierId = "SUP-1",
        string? requisitionId = null,
        string currency = "TRY",
        List<PoLineInput>? lines = null,
        string? idem = null)
        => new(supplierId, requisitionId, currency,
            lines ?? new List<PoLineInput> { new(Item1, null, "100.000", "BOX", "12.5000") },
            null, null, idem);

    private static decimal Dec(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    // ── CREATE: draft happy path + server-computed money ────────────────────────────────────────
    [Fact]
    public async Task Create_po_starts_in_draft_and_computes_money()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");

        var result = await f.Create().Handle(NewCreate(), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(PoStatus.Draft, result.Data!.Status);
        Assert.StartsWith("PO-", result.Data.PoId);
        Assert.Single(result.Data.Lines);
        Assert.StartsWith("POL-", result.Data.Lines[0].PoLineId); // server-assigned; GRN 0142 poLineId ile eşleşir
        // lineAmount = 100.000 × 12.5000 = 1250 (server-computed, Decimal string; float YASAK)
        Assert.Equal(1250m, Dec(result.Data.Lines[0].LineAmount));
        Assert.Equal(1250m, Dec(result.Data.TotalAmount));
    }

    [Fact]
    public async Task Create_po_totalAmount_sums_all_lines()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");

        var lines = new List<PoLineInput>
        {
            new(Item1, null, "2", "BOX", "10.00"),   // 20.00
            new(Item2, null, "3", "BOX", "5.5000")   // 16.5000
        };
        var result = await f.Create().Handle(NewCreate(lines: lines), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(20m, Dec(result.Data!.Lines[0].LineAmount));
        Assert.Equal(16.5m, Dec(result.Data.Lines[1].LineAmount));
        Assert.Equal(36.5m, Dec(result.Data.TotalAmount)); // server-computed Σ lineAmount
    }

    // ── VALIDATION: missing supplier/currency, empty lines, float qty, negative/float price ───────
    [Fact]
    public void Validator_rejects_missing_fields_and_bad_decimals()
    {
        var validator = new CreatePurchaseOrderValidator();

        Assert.False(validator.Validate(NewCreate(supplierId: "")).IsValid);   // missing supplier
        Assert.False(validator.Validate(NewCreate(currency: "")).IsValid);     // missing currency
        Assert.False(validator.Validate(NewCreate(lines: new List<PoLineInput>())).IsValid); // empty lines
        Assert.False(validator.Validate(NewCreate(lines: new List<PoLineInput> { new(Item1, null, "1.0e3", "BOX", "1") })).IsValid); // float qty
        Assert.False(validator.Validate(NewCreate(lines: new List<PoLineInput> { new(Item1, null, "1", "BOX", "-1") })).IsValid);    // negative price

        // Vacuity control (K3): a well-formed command passes.
        Assert.True(validator.Validate(NewCreate()).IsValid);
    }

    [Fact]
    public async Task Create_handler_returns_422_on_empty_lines()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var result = await f.Create().Handle(NewCreate(lines: new List<PoLineInput>()), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(422, result.StatusCode);
        Assert.Contains("VALIDATION_FAILED", result.Errors);

        // Vacuity control: a valid create succeeds in the same setup.
        var ok = await f.Create().Handle(NewCreate(), default);
        Assert.Equal(201, ok.StatusCode);
    }

    // ── CREATE: unknown supplier → 404 (fail-closed) + control ──────────────────────────────────
    [Fact]
    public async Task Create_with_unknown_supplier_is_404_fail_closed()
    {
        var f = NewFixture();
        // No supplier seeded → SUP-404 unknown.
        var unknown = await f.Create().Handle(NewCreate(supplierId: "SUP-404"), default);
        Assert.False(unknown.IsSuccessful);
        Assert.Equal(404, unknown.StatusCode);
        Assert.Contains("UNKNOWN_REFERENCE", unknown.Errors);
        Assert.Empty(f.PoStore); // fail-closed: no record written

        // Vacuity control: once the supplier exists, the same create succeeds.
        await SeedSupplier(f, "SUP-404");
        var ok = await f.Create().Handle(NewCreate(supplierId: "SUP-404"), default);
        Assert.Equal(201, ok.StatusCode);
    }

    // ── CREATE: unknown item (0290 seam) → 404 (fail-closed) + control ──────────────────────────
    [Fact]
    public async Task Create_with_unknown_item_is_404_fail_closed()
    {
        var f = NewFixture(knownItems: new[] { Item1 }); // Item2 is NOT known
        await SeedSupplier(f, "SUP-1");

        var unknown = await f.Create().Handle(
            NewCreate(lines: new List<PoLineInput> { new(Item2, null, "5", "BOX", "1") }), default);
        Assert.False(unknown.IsSuccessful);
        Assert.Equal(404, unknown.StatusCode);
        Assert.Contains("UNKNOWN_REFERENCE", unknown.Errors);

        // Vacuity control: a known item passes.
        var ok = await f.Create().Handle(
            NewCreate(lines: new List<PoLineInput> { new(Item1, null, "5", "BOX", "1") }), default);
        Assert.Equal(201, ok.StatusCode);
    }

    // ── CREATE: requisitionId must reference an Approved requisition ────────────────────────────
    [Fact]
    public async Task Create_with_non_approved_requisition_is_422()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");

        // Unknown requisition reference → 422.
        var unknownReq = await f.Create().Handle(NewCreate(requisitionId: "REQ-NONE"), default);
        Assert.False(unknownReq.IsSuccessful);
        Assert.Equal(422, unknownReq.StatusCode);

        // Draft (not Approved) requisition → 422.
        var draft = await f.Requisitions.CreateAsync(new RequisitionEntity
        {
            RequisitionId = "REQ-DRAFT",
            Status = RequisitionStatus.Draft,
            Lines = new List<RequisitionLine> { new() { ItemId = Item1, Quantity = "1", UomId = "BOX" } }
        }, default);
        var draftRef = await f.Create().Handle(NewCreate(requisitionId: draft.RequisitionId), default);
        Assert.Equal(422, draftRef.StatusCode);

        // Control: an Approved requisition reference succeeds.
        var approvedCode = await SeedApprovedRequisition(f);
        var ok = await f.Create().Handle(NewCreate(requisitionId: approvedCode), default);
        Assert.Equal(201, ok.StatusCode);
        Assert.Equal(approvedCode, ok.Data!.RequisitionId);
    }

    // ── APPROVE: draft→approved (+ workflowInstanceId); repeat → 409 ─────────────────────────────
    [Fact]
    public async Task Approve_moves_draft_to_approved_then_409_on_repeat()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.PoId;

        var approved = await f.Approve().Handle(new ApprovePurchaseOrderCommand(code, null), default);
        Assert.True(approved.IsSuccessful);
        Assert.Equal(PoStatus.Approved, approved.Data!.Status);
        Assert.False(string.IsNullOrWhiteSpace(approved.Data.WorkflowInstanceId)); // MOD-0023 seam assigned

        // Approving an already-Approved PO (no idempotency key) → 409 INVALID_STATE.
        var again = await f.Approve().Handle(new ApprovePurchaseOrderCommand(code, null), default);
        Assert.False(again.IsSuccessful);
        Assert.Equal(409, again.StatusCode);
        Assert.Contains("INVALID_STATE", again.Errors);
    }

    [Fact]
    public async Task Approve_is_idempotent_on_key_no_second_side_effect()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.PoId;

        var first = await f.Approve().Handle(new ApprovePurchaseOrderCommand(code, "app-1"), default);
        var replay = await f.Approve().Handle(new ApprovePurchaseOrderCommand(code, "app-1"), default);

        Assert.True(first.IsSuccessful);
        Assert.True(replay.IsSuccessful);
        Assert.Equal(PoStatus.Approved, replay.Data!.Status);
        // Same version: the replay did not re-apply the transition.
        Assert.Equal(first.Data!.Version, replay.Data.Version);
    }

    [Fact]
    public async Task Approve_unknown_po_is_404()
    {
        var f = NewFixture();
        var result = await f.Approve().Handle(new ApprovePurchaseOrderCommand("PO-NONE", null), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    // ── DELETE: only Draft; non-Draft → 409; soft-deleted invisible ─────────────────────────────
    [Fact]
    public async Task Delete_draft_soft_deletes_and_hides_it()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.PoId;

        var del = await f.Delete().Handle(new DeletePurchaseOrderCommand(code), default);
        Assert.True(del.IsSuccessful);

        var byId = await new GetPurchaseOrderByIdHandler(f.PurchaseOrders).Handle(new GetPurchaseOrderByIdQuery(code), default);
        Assert.Equal(404, byId.StatusCode);
    }

    [Fact]
    public async Task Delete_non_draft_po_is_409()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.PoId;
        await f.Approve().Handle(new ApprovePurchaseOrderCommand(code, null), default);

        var del = await f.Delete().Handle(new DeletePurchaseOrderCommand(code), default);
        Assert.False(del.IsSuccessful);
        Assert.Equal(409, del.StatusCode);
        Assert.Contains("INVALID_STATE", del.Errors);

        // Still visible/present (not deleted).
        var byId = await new GetPurchaseOrderByIdHandler(f.PurchaseOrders).Handle(new GetPurchaseOrderByIdQuery(code), default);
        Assert.True(byId.IsSuccessful);
    }

    [Fact]
    public async Task BulkDelete_removes_only_draft_pos()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var draft = (await f.Create().Handle(NewCreate(), default)).Data!.PoId;
        var approved = (await f.Create().Handle(NewCreate(), default)).Data!.PoId;
        await f.Approve().Handle(new ApprovePurchaseOrderCommand(approved, null), default);

        var result = await f.BulkDelete().Handle(new BulkDeletePurchaseOrderCommand(new List<string> { draft, approved }), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data); // only the Draft one deleted

        var list = await new GetPurchaseOrderListHandler(f.PurchaseOrders).Handle(new GetPurchaseOrderListQuery(), default);
        Assert.DoesNotContain(list.Data!.Items, i => i.PoId == draft);
        Assert.Contains(list.Data.Items, i => i.PoId == approved);
    }

    // ── LIST: supplierId filter ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task List_filters_by_supplier()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        await SeedSupplier(f, "SUP-2");
        await f.Create().Handle(NewCreate(supplierId: "SUP-1"), default);
        await f.Create().Handle(NewCreate(supplierId: "SUP-2"), default);

        var onlyOne = await new GetPurchaseOrderListHandler(f.PurchaseOrders).Handle(new GetPurchaseOrderListQuery(SupplierId: "SUP-1"), default);
        Assert.All(onlyOne.Data!.Items, i => Assert.Equal("SUP-1", i.SupplierId));
        Assert.Single(onlyOne.Data.Items);
    }

    // ── CREATE: idempotent replay → no duplicate ────────────────────────────────────────────────
    [Fact]
    public async Task Create_is_idempotent_on_key()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var first = await f.Create().Handle(NewCreate(idem: "idem-1"), default);
        var second = await f.Create().Handle(NewCreate(idem: "idem-1"), default);

        Assert.Equal(first.Data!.PoId, second.Data!.PoId);
        Assert.Single(f.PoStore);
    }

    // ── TENANT + LE ISOLATION: cross-LE read → 404 (+ owning-LE control) ────────────────────────
    [Fact]
    public async Task Cross_legal_entity_read_returns_404()
    {
        var poStore = new List<PurchaseOrderEntity>();
        var reqStore = new List<RequisitionEntity>();
        var supStore = new List<Supplier>();
        var repoA = new FakePurchaseOrderRepository(poStore, TenantA, LeA);
        var repoB = new FakePurchaseOrderRepository(poStore, TenantA, LeB);
        var reqA = new FakeRequisitionRepository(reqStore, TenantA, LeA);
        var supA = new FakeSupplierRepository(supStore, TenantA, LeA);
        var products = new FakeProductReferenceValidator(new[] { Item1, Item2 });
        await supA.CreateAsync(new Supplier { SupplierId = "SUP-1", Name = "SUP-1", Status = SupplierStatus.Active }, default);

        var created = await new CreatePurchaseOrderHandler(repoA, supA, reqA, products).Handle(NewCreate(), default);
        var code = created.Data!.PoId;

        // Same store, same tenant, DIFFERENT legal entity → invisible.
        var readB = await new GetPurchaseOrderByIdHandler(repoB).Handle(new GetPurchaseOrderByIdQuery(code), default);
        Assert.False(readB.IsSuccessful);
        Assert.Equal(404, readB.StatusCode);

        // Control: the owning LE can read it (isolation is not vacuously hiding everything).
        var readA = await new GetPurchaseOrderByIdHandler(repoA).Handle(new GetPurchaseOrderByIdQuery(code), default);
        Assert.True(readA.IsSuccessful);
    }

    // ── TENANT + LE ISOLATION: cross-LE approve is blocked (→ 404, not silent transition) ───────
    [Fact]
    public async Task Cross_legal_entity_approve_is_blocked()
    {
        var poStore = new List<PurchaseOrderEntity>();
        var reqStore = new List<RequisitionEntity>();
        var supStore = new List<Supplier>();
        var repoA = new FakePurchaseOrderRepository(poStore, TenantA, LeA);
        var repoB = new FakePurchaseOrderRepository(poStore, TenantA, LeB);
        var reqA = new FakeRequisitionRepository(reqStore, TenantA, LeA);
        var supA = new FakeSupplierRepository(supStore, TenantA, LeA);
        var products = new FakeProductReferenceValidator(new[] { Item1, Item2 });
        await supA.CreateAsync(new Supplier { SupplierId = "SUP-1", Name = "SUP-1", Status = SupplierStatus.Active }, default);

        var created = await new CreatePurchaseOrderHandler(repoA, supA, reqA, products).Handle(NewCreate(), default);
        var code = created.Data!.PoId;

        var approveB = await new ApprovePurchaseOrderHandler(repoB).Handle(new ApprovePurchaseOrderCommand(code, null), default);
        Assert.False(approveB.IsSuccessful);
        Assert.Equal(404, approveB.StatusCode);

        // The record under LE-A is untouched (still Draft).
        var readA = await new GetPurchaseOrderByIdHandler(repoA).Handle(new GetPurchaseOrderByIdQuery(code), default);
        Assert.Equal(PoStatus.Draft, readA.Data!.Status);
    }
}
