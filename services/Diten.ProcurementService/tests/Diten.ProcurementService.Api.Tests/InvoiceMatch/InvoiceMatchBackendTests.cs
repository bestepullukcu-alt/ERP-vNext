using Diten.ProcurementService.Api.Tests.Grn;
using Diten.ProcurementService.Api.Tests.RequisitionPo;
using Diten.ProcurementService.Api.Tests.Sourcing;
using Diten.ProcurementService.Api.Tests.Suppliers;
using Diten.ProcurementService.Application.Features.InvoiceMatch;
using Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;
using Diten.ProcurementService.Application.Features.InvoiceMatch.Handlers.CommandHandlers;
using Diten.ProcurementService.Application.Features.InvoiceMatch.Handlers.QueryHandlers;
using Diten.ProcurementService.Application.Features.InvoiceMatch.Queries;
using Diten.ProcurementService.Application.Features.InvoiceMatch.Validators;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Xunit;
using GoodsReceiptEntity = Diten.ProcurementService.Domain.Entities.GoodsReceipt;
using InvoiceEntity = Diten.ProcurementService.Domain.Entities.Invoice;
using MatchExceptionEntity = Diten.ProcurementService.Domain.Entities.MatchException;
using PurchaseOrderEntity = Diten.ProcurementService.Domain.Entities.PurchaseOrder;
using SupplierEntity = Diten.ProcurementService.Domain.Entities.Supplier;

namespace Diten.ProcurementService.Api.Tests.InvoiceMatch;

/// <summary>
/// MOD-0143 Invoice Capture &amp; 3-Way Match backend behaviour tests — the G2A golden-flow tail (PO→GRN→0173→invoice
/// 3-way match). Each pins a contract/pack rule against PRODUCTION handlers/validators over the tenant+LE-scoped
/// FakeInvoiceMatchRepository, the consumed SUPPLIER/PO/GRN seams (intra-service), the PRODUCT-MASTER consume seam
/// (fail-closed) and the TOLERANCE POLICY seam (FakeMatchTolerancePolicy). Central rules: tolerance is POLICY-DRIVEN
/// (no baked number in match logic, ASSUMPTION-P2P-01); the module produces match OUTCOMES only and NEVER executes
/// payment (ClearedForPayment is a status flag, AP/payment = Finance/Treasury). The Mongo filter is pinned in
/// InvoiceMatchRepository.cs.
/// </summary>
public sealed class InvoiceMatchBackendTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid LeA = Guid.NewGuid();
    private static readonly Guid LeB = Guid.NewGuid();

    private const string Item1 = "b1f2c3d4-0000-0000-0000-000000000001";
    private const string Item2 = "b1f2c3d4-0000-0000-0000-000000000002";
    private const string Sup1 = "SUP-1";
    private const string Po1 = "PO-1";
    private const string PoLine1 = "POL-1";
    private const string Ccy = "TRY";

    private sealed class Fixture
    {
        public List<InvoiceEntity> InvoiceStore = new();
        public List<MatchExceptionEntity> ExceptionStore = new();
        public List<SupplierEntity> SupplierStore = new();
        public List<PurchaseOrderEntity> PoStore = new();
        public List<GoodsReceiptEntity> GrnStore = new();
        public required IInvoiceMatchRepository Invoices;
        public required ISupplierRepository Suppliers;
        public required IPurchaseOrderRepository PurchaseOrders;
        public required IGrnRepository Grns;
        public required FakeProductReferenceValidator Products;

        public CaptureInvoiceHandler Capture() => new(Invoices, Suppliers, PurchaseOrders, Products);
        public RunThreeWayMatchHandler Match(FakeMatchTolerancePolicy policy) => new(Invoices, PurchaseOrders, Grns, policy);
        public ResolveMatchExceptionHandler Resolve() => new(Invoices, new FakeCurrentUserContext());
        public GetInvoiceByIdHandler Get() => new(Invoices);
        public ListMatchExceptionsHandler ListExceptions() => new(Invoices);
        public DeleteInvoiceHandler Delete() => new(Invoices);
        public BulkDeleteInvoiceHandler BulkDelete() => new(Invoices);
    }

    private static Fixture NewFixture(IEnumerable<string>? knownItems = null, Guid? le = null)
    {
        var legalEntity = le ?? LeA;
        var invoiceStore = new List<InvoiceEntity>();
        var exceptionStore = new List<MatchExceptionEntity>();
        var supplierStore = new List<SupplierEntity>();
        var poStore = new List<PurchaseOrderEntity>();
        var grnStore = new List<GoodsReceiptEntity>();
        return new Fixture
        {
            InvoiceStore = invoiceStore,
            ExceptionStore = exceptionStore,
            SupplierStore = supplierStore,
            PoStore = poStore,
            GrnStore = grnStore,
            Invoices = new FakeInvoiceMatchRepository(invoiceStore, exceptionStore, TenantA, legalEntity),
            Suppliers = new FakeSupplierRepository(supplierStore, TenantA, legalEntity),
            PurchaseOrders = new FakePurchaseOrderRepository(poStore, TenantA, legalEntity),
            Grns = new FakeGrnRepository(grnStore, TenantA, legalEntity),
            Products = new FakeProductReferenceValidator(knownItems ?? new[] { Item1, Item2 })
        };
    }

    private static async Task SeedSupplier(Fixture f)
        => await f.Suppliers.CreateAsync(new SupplierEntity { SupplierId = Sup1 }, default);

    private static async Task SeedPo(Fixture f, string currency = Ccy, string qty = "100", string unitPrice = "10")
        => await f.PurchaseOrders.CreateAsync(new PurchaseOrderEntity
        {
            PoId = Po1,
            SupplierId = Sup1,
            Currency = currency,
            Status = PoStatus.Approved,
            Lines = new List<PoLine>
            {
                new() { PoLineId = PoLine1, ItemId = Item1, Quantity = qty, UnitPrice = unitPrice, LineAmount = "1000" }
            }
        }, default);

    private static async Task SeedGrn(Fixture f, string receivedQty = "100")
        => await f.Grns.CreateAsync(new GoodsReceiptEntity
        {
            GrnId = "GRN-1",
            PoId = Po1,
            WarehouseId = "wh-01",
            Status = GrnStatus.Posted,
            Lines = new List<GrnLine>
            {
                new() { PoLineId = PoLine1, ItemId = Item1, Quantity = receivedQty }
            }
        }, default);

    private static async Task SeedSupplierAndPo(Fixture f, string currency = Ccy, string poQty = "100", string poPrice = "10")
    {
        await SeedSupplier(f);
        await SeedPo(f, currency, poQty, poPrice);
    }

    private static InvoiceLineInput Line(
        string itemId = Item1,
        string quantity = "100",
        string unitPrice = "10",
        string? lineAmount = "1000",
        string? poLineId = PoLine1)
        => new(poLineId, itemId, quantity, unitPrice, lineAmount);

    private static CaptureInvoiceCommand NewCapture(
        string supplierId = Sup1,
        string poId = Po1,
        string invoiceNumber = "INV-2026-1",
        string currency = Ccy,
        List<InvoiceLineInput>? lines = null,
        string? idem = null)
        => new(supplierId, poId, invoiceNumber, currency, lines ?? new List<InvoiceLineInput> { Line() }, null, null, idem);

    private static async Task<string> CaptureAsync(Fixture f, CaptureInvoiceCommand? cmd = null)
    {
        var result = await f.Capture().Handle(cmd ?? NewCapture(), default);
        Assert.Equal(201, result.StatusCode);
        return result.Data!.InvoiceId;
    }

    // ══ CAPTURE ═══════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Capture_happy_path_is_201_captured()
    {
        var f = NewFixture();
        await SeedSupplierAndPo(f);

        var result = await f.Capture().Handle(NewCapture(idem: "cap-1"), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(InvoiceStatus.Captured, result.Data!.Status);
        Assert.StartsWith("INV-", result.Data.InvoiceId);
        Assert.Equal("1000", result.Data.TotalAmount); // server-computed Σ lineAmount
        Assert.Single(f.InvoiceStore);
    }

    [Fact]
    public async Task Capture_unknown_supplier_is_404_fail_closed()
    {
        var f = NewFixture();
        await SeedPo(f); // PO exists but supplier NOT seeded

        var result = await f.Capture().Handle(NewCapture(), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("UNKNOWN_REFERENCE", result.Errors);
        Assert.Empty(f.InvoiceStore); // fail-closed: nothing written

        // Vacuity control: once the supplier exists, the same capture succeeds.
        await SeedSupplier(f);
        var ok = await f.Capture().Handle(NewCapture(), default);
        Assert.Equal(201, ok.StatusCode);
    }

    [Fact]
    public async Task Capture_unknown_po_is_404_fail_closed()
    {
        var f = NewFixture();
        await SeedSupplier(f); // supplier exists but PO NOT seeded

        var result = await f.Capture().Handle(NewCapture(), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("UNKNOWN_REFERENCE", result.Errors);
        Assert.Empty(f.InvoiceStore);

        await SeedPo(f);
        var ok = await f.Capture().Handle(NewCapture(), default);
        Assert.Equal(201, ok.StatusCode);
    }

    [Fact]
    public async Task Capture_unknown_item_is_404_fail_closed()
    {
        var f = NewFixture(knownItems: new[] { Item1 }); // Item2 NOT known
        await SeedSupplierAndPo(f);

        var result = await f.Capture().Handle(
            NewCapture(lines: new List<InvoiceLineInput> { Line(itemId: Item2) }), default);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("UNKNOWN_REFERENCE", result.Errors);
        Assert.Empty(f.InvoiceStore);
    }

    [Fact]
    public async Task Capture_duplicate_supplier_plus_invoicenumber_is_409()
    {
        var f = NewFixture();
        await SeedSupplierAndPo(f);

        var first = await f.Capture().Handle(NewCapture(invoiceNumber: "INV-DUP"), default);
        Assert.Equal(201, first.StatusCode);

        var dup = await f.Capture().Handle(NewCapture(invoiceNumber: "INV-DUP"), default);
        Assert.False(dup.IsSuccessful);
        Assert.Equal(409, dup.StatusCode);
        Assert.Contains("DUPLICATE_INVOICE", dup.Errors);
        Assert.Single(f.InvoiceStore); // no second record
    }

    [Fact]
    public async Task Capture_currency_mismatch_with_po_is_422()
    {
        var f = NewFixture();
        await SeedSupplierAndPo(f, currency: "TRY");

        var result = await f.Capture().Handle(NewCapture(currency: "EUR"), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(422, result.StatusCode);
        Assert.Contains("CURRENCY_MISMATCH", result.Errors);
        Assert.Empty(f.InvoiceStore);

        // Vacuity control: matching currency captures.
        var ok = await f.Capture().Handle(NewCapture(currency: "TRY"), default);
        Assert.Equal(201, ok.StatusCode);
    }

    [Fact]
    public async Task Capture_is_idempotent_no_duplicate()
    {
        var f = NewFixture();
        await SeedSupplierAndPo(f);

        var first = await f.Capture().Handle(NewCapture(idem: "cap-x"), default);
        var replay = await f.Capture().Handle(NewCapture(idem: "cap-x"), default);

        Assert.Equal(first.Data!.InvoiceId, replay.Data!.InvoiceId);
        Assert.Single(f.InvoiceStore); // replay wrote nothing new
    }

    [Fact]
    public void Validator_rejects_missing_fields_and_bad_values()
    {
        var v = new CaptureInvoiceValidator();

        Assert.False(v.Validate(NewCapture(supplierId: "")).IsValid);
        Assert.False(v.Validate(NewCapture(poId: "")).IsValid);
        Assert.False(v.Validate(NewCapture(invoiceNumber: "")).IsValid);
        Assert.False(v.Validate(NewCapture(currency: "")).IsValid);
        Assert.False(v.Validate(NewCapture(lines: new List<InvoiceLineInput>())).IsValid);
        Assert.False(v.Validate(NewCapture(lines: new List<InvoiceLineInput> { Line(quantity: "1.0e3") })).IsValid); // float
        Assert.False(v.Validate(NewCapture(lines: new List<InvoiceLineInput> { Line(quantity: "-5") })).IsValid);   // non-positive

        // Vacuity control (K3): a well-formed command passes.
        Assert.True(v.Validate(NewCapture()).IsValid);
    }

    // ══ 3-WAY MATCH ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Match_happy_path_is_matched_no_variance()
    {
        var f = NewFixture();
        await SeedSupplierAndPo(f, poQty: "100", poPrice: "10");
        await SeedGrn(f, receivedQty: "100");
        var invoiceId = await CaptureAsync(f); // qty 100, price 10, lineAmount 1000

        var outcome = await f.Match(new FakeMatchTolerancePolicy()).Handle(
            new RunThreeWayMatchCommand(invoiceId, null, null), default);

        Assert.True(outcome.IsSuccessful);
        Assert.Equal(InvoiceMatchResult.Matched, outcome.Data!.Result);
        Assert.Empty(outcome.Data.Variances);
        Assert.Contains("GRN-1", outcome.Data.GrnIds);
        Assert.Null(outcome.Data.ExceptionId);

        var read = await f.Get().Handle(new GetInvoiceByIdQuery(invoiceId), default);
        Assert.Equal(InvoiceStatus.Matched, read.Data!.Status);
    }

    // ── POLICY-DRIVEN tolerance (ASSUMPTION-P2P-01) — the SAME qty variance flips with the seam (K3) ──
    [Fact]
    public async Task Match_qty_variance_is_exception_under_zero_tolerance_but_within_under_wide_tolerance()
    {
        // Zero-tolerance (production default shape): qty 101 vs received 100 → beyond → Exception.
        var f1 = NewFixture();
        await SeedSupplierAndPo(f1, poQty: "100", poPrice: "10");
        await SeedGrn(f1, receivedQty: "100");
        var inv1 = await CaptureAsync(f1, NewCapture(invoiceNumber: "INV-Q",
            lines: new List<InvoiceLineInput> { Line(quantity: "101", unitPrice: "10", lineAmount: "1010") }));

        var zero = await f1.Match(new FakeMatchTolerancePolicy(qty: 0m)).Handle(new RunThreeWayMatchCommand(inv1, null, null), default);
        Assert.Equal(InvoiceMatchResult.Exception, zero.Data!.Result);
        Assert.NotNull(zero.Data.ExceptionId);
        Assert.Contains(zero.Data.Variances, x => x.Field == "quantity" && !x.WithinTolerance);
        Assert.Single(f1.ExceptionStore);
        Assert.Equal(MatchExceptionReason.QtyMismatch, f1.ExceptionStore.Single().ReasonCode);

        // Wide-tolerance seam: the SAME 1-unit variance is now within tolerance → MatchedWithinTolerance, NO queue entry.
        var f2 = NewFixture();
        await SeedSupplierAndPo(f2, poQty: "100", poPrice: "10");
        await SeedGrn(f2, receivedQty: "100");
        var inv2 = await CaptureAsync(f2, NewCapture(invoiceNumber: "INV-Q",
            lines: new List<InvoiceLineInput> { Line(quantity: "101", unitPrice: "10", lineAmount: "1010") }));

        var wide = await f2.Match(new FakeMatchTolerancePolicy(qty: 5m)).Handle(new RunThreeWayMatchCommand(inv2, "TOL-WIDE", null), default);
        Assert.Equal(InvoiceMatchResult.MatchedWithinTolerance, wide.Data!.Result);
        Assert.Null(wide.Data.ExceptionId);
        Assert.Contains(wide.Data.Variances, x => x.Field == "quantity" && x.WithinTolerance);
        Assert.Empty(f2.ExceptionStore); // policy absorbed it — no exception queued
        Assert.Equal("TOL-WIDE", wide.Data.ToleranceProfileId); // profile flowed from the seam, not baked in
    }

    [Fact]
    public async Task Match_price_beyond_tolerance_is_exception_with_queue_entry()
    {
        var f = NewFixture();
        await SeedSupplierAndPo(f, poQty: "100", poPrice: "10");
        await SeedGrn(f, receivedQty: "100");
        // invoice unitPrice 12 vs PO 10 → price variance 2 (beyond zero tolerance).
        var invoiceId = await CaptureAsync(f, NewCapture(invoiceNumber: "INV-P",
            lines: new List<InvoiceLineInput> { Line(quantity: "100", unitPrice: "12", lineAmount: "1200") }));

        var outcome = await f.Match(new FakeMatchTolerancePolicy()).Handle(new RunThreeWayMatchCommand(invoiceId, null, null), default);
        Assert.Equal(InvoiceMatchResult.Exception, outcome.Data!.Result);
        Assert.Contains(outcome.Data.Variances, x => x.Field == "price" && !x.WithinTolerance);

        // The exception is queued and listable by reasonCode.
        var queued = f.ExceptionStore.Single();
        Assert.Equal(MatchExceptionReason.PriceMismatch, queued.ReasonCode);
        Assert.Equal(MatchExceptionStatus.Open, queued.Status);
        Assert.Equal(invoiceId, queued.InvoiceId);

        var list = await f.ListExceptions().Handle(new ListMatchExceptionsQuery(MatchExceptionReason.PriceMismatch), default);
        Assert.Single(list.Data!.Items);
        var empty = await f.ListExceptions().Handle(new ListMatchExceptionsQuery(MatchExceptionReason.QtyMismatch), default);
        Assert.Empty(empty.Data!.Items);
    }

    [Fact]
    public async Task Match_without_receipt_is_no_receipt_exception()
    {
        var f = NewFixture();
        await SeedSupplierAndPo(f); // no GRN seeded
        var invoiceId = await CaptureAsync(f);

        var outcome = await f.Match(new FakeMatchTolerancePolicy()).Handle(new RunThreeWayMatchCommand(invoiceId, null, null), default);
        Assert.Equal(InvoiceMatchResult.Exception, outcome.Data!.Result);
        Assert.Equal(MatchExceptionReason.NoReceipt, f.ExceptionStore.Single().ReasonCode);
    }

    [Fact]
    public async Task Match_unknown_invoice_is_404()
    {
        var f = NewFixture();
        var outcome = await f.Match(new FakeMatchTolerancePolicy()).Handle(new RunThreeWayMatchCommand("INV-NONE", null, null), default);
        Assert.False(outcome.IsSuccessful);
        Assert.Equal(404, outcome.StatusCode);
        Assert.Contains("NOT_FOUND", outcome.Errors);
    }

    [Fact]
    public async Task Match_is_idempotent_no_duplicate_exception()
    {
        var f = NewFixture();
        await SeedSupplierAndPo(f, poQty: "100", poPrice: "10");
        await SeedGrn(f, receivedQty: "100");
        var invoiceId = await CaptureAsync(f, NewCapture(invoiceNumber: "INV-P",
            lines: new List<InvoiceLineInput> { Line(quantity: "100", unitPrice: "12", lineAmount: "1200") }));

        var first = await f.Match(new FakeMatchTolerancePolicy()).Handle(new RunThreeWayMatchCommand(invoiceId, null, "m-1"), default);
        var replay = await f.Match(new FakeMatchTolerancePolicy()).Handle(new RunThreeWayMatchCommand(invoiceId, null, "m-1"), default);

        Assert.Equal(InvoiceMatchResult.Exception, first.Data!.Result);
        Assert.Equal(first.Data.ExceptionId, replay.Data!.ExceptionId); // same outcome
        Assert.Single(f.ExceptionStore); // replay created NO second exception
    }

    [Fact]
    public async Task Match_already_matched_invoice_again_is_409()
    {
        var f = NewFixture();
        await SeedSupplierAndPo(f);
        await SeedGrn(f);
        var invoiceId = await CaptureAsync(f);

        var first = await f.Match(new FakeMatchTolerancePolicy()).Handle(new RunThreeWayMatchCommand(invoiceId, null, null), default);
        Assert.Equal(InvoiceMatchResult.Matched, first.Data!.Result);

        var again = await f.Match(new FakeMatchTolerancePolicy()).Handle(new RunThreeWayMatchCommand(invoiceId, null, null), default);
        Assert.False(again.IsSuccessful);
        Assert.Equal(409, again.StatusCode);
        Assert.Contains("INVALID_STATE", again.Errors);
    }

    // ══ RESOLVE EXCEPTION (approve / reject / tolerance-override) ══════════════════════════════════

    private static async Task<(Fixture Fixture, string InvoiceId, string ExceptionId)> WithOpenException(string invoiceNumber = "INV-EXC")
    {
        var f = NewFixture();
        await SeedSupplierAndPo(f, poQty: "100", poPrice: "10");
        await SeedGrn(f, receivedQty: "100");
        var invoiceId = await CaptureAsync(f, NewCapture(invoiceNumber: invoiceNumber,
            lines: new List<InvoiceLineInput> { Line(quantity: "100", unitPrice: "12", lineAmount: "1200") }));
        var outcome = await f.Match(new FakeMatchTolerancePolicy()).Handle(new RunThreeWayMatchCommand(invoiceId, null, null), default);
        return (f, invoiceId, outcome.Data!.ExceptionId!);
    }

    [Fact]
    public async Task Resolve_approve_clears_for_payment_and_records_trail_but_executes_no_payment()
    {
        var (f, invoiceId, exceptionId) = await WithOpenException();

        var resolved = await f.Resolve().Handle(
            new ResolveMatchExceptionCommand(exceptionId, "approve", "sözleşme ekiyle onaylandı", "res-1"), default);
        Assert.True(resolved.IsSuccessful);

        // Invoice → ClearedForPayment (a STATUS FLAG only; no payment is executed here — AP/payment = Finance/Treasury).
        var read = await f.Get().Handle(new GetInvoiceByIdQuery(invoiceId), default);
        Assert.Equal(InvoiceStatus.ClearedForPayment, read.Data!.Status);

        // Exception closed with an approval trail (decision + note + actor).
        var exc = f.ExceptionStore.Single();
        Assert.Equal(MatchExceptionStatus.Resolved, exc.Status);
        Assert.Equal("approve", exc.ResolutionDecision);
        Assert.Equal("sözleşme ekiyle onaylandı", exc.ResolutionNote);
        Assert.Equal("ap-approver", exc.ResolvedBy);
        Assert.NotNull(exc.ResolvedAt);
    }

    [Fact]
    public async Task Resolve_reject_sets_invoice_rejected()
    {
        var (f, invoiceId, exceptionId) = await WithOpenException();

        var resolved = await f.Resolve().Handle(new ResolveMatchExceptionCommand(exceptionId, "reject", null, null), default);
        Assert.True(resolved.IsSuccessful);
        Assert.Equal(InvoiceMatchResult.Exception, resolved.Data!.Result);

        var read = await f.Get().Handle(new GetInvoiceByIdQuery(invoiceId), default);
        Assert.Equal(InvoiceStatus.Rejected, read.Data!.Status);
        Assert.Equal(MatchExceptionStatus.Resolved, f.ExceptionStore.Single().Status);
    }

    [Fact]
    public async Task Resolve_tolerance_override_sets_matched_within_tolerance()
    {
        var (f, invoiceId, exceptionId) = await WithOpenException();

        var resolved = await f.Resolve().Handle(new ResolveMatchExceptionCommand(exceptionId, "tolerance-override", null, null), default);
        Assert.True(resolved.IsSuccessful);
        Assert.Equal(InvoiceMatchResult.MatchedWithinTolerance, resolved.Data!.Result);

        var read = await f.Get().Handle(new GetInvoiceByIdQuery(invoiceId), default);
        Assert.Equal(InvoiceStatus.MatchedWithinTolerance, read.Data!.Status);
    }

    [Fact]
    public async Task Resolve_closed_exception_again_is_409()
    {
        var (f, _, exceptionId) = await WithOpenException();

        var first = await f.Resolve().Handle(new ResolveMatchExceptionCommand(exceptionId, "approve", null, null), default);
        Assert.True(first.IsSuccessful);

        var again = await f.Resolve().Handle(new ResolveMatchExceptionCommand(exceptionId, "approve", null, null), default);
        Assert.False(again.IsSuccessful);
        Assert.Equal(409, again.StatusCode);
        Assert.Contains("INVALID_STATE", again.Errors);
    }

    [Fact]
    public async Task Resolve_is_idempotent_replay_no_second_transition()
    {
        var (f, _, exceptionId) = await WithOpenException();

        var first = await f.Resolve().Handle(new ResolveMatchExceptionCommand(exceptionId, "approve", null, "res-9"), default);
        var replay = await f.Resolve().Handle(new ResolveMatchExceptionCommand(exceptionId, "approve", null, "res-9"), default);

        Assert.True(first.IsSuccessful);
        Assert.True(replay.IsSuccessful); // replay returns the same outcome, not a 409
        Assert.Equal(MatchExceptionStatus.Resolved, f.ExceptionStore.Single().Status);
    }

    [Fact]
    public async Task Resolve_unknown_exception_is_404()
    {
        var f = NewFixture();
        var result = await f.Resolve().Handle(new ResolveMatchExceptionCommand("EXC-NONE", "approve", null, null), default);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("NOT_FOUND", result.Errors);
    }

    [Fact]
    public void Resolve_validator_rejects_bad_decision()
    {
        var v = new ResolveMatchExceptionValidator();
        Assert.False(v.Validate(new ResolveMatchExceptionCommand("EXC-1", "", null, null)).IsValid);
        Assert.False(v.Validate(new ResolveMatchExceptionCommand("EXC-1", "maybe", null, null)).IsValid);
        Assert.True(v.Validate(new ResolveMatchExceptionCommand("EXC-1", "approve", null, null)).IsValid);
        Assert.True(v.Validate(new ResolveMatchExceptionCommand("EXC-1", "tolerance-override", null, null)).IsValid);
    }

    // ══ TENANT + LE ISOLATION + SOFT DELETE ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Cross_legal_entity_read_returns_404()
    {
        var invoiceStore = new List<InvoiceEntity>();
        var exceptionStore = new List<MatchExceptionEntity>();
        var supplierStore = new List<SupplierEntity>();
        var poStore = new List<PurchaseOrderEntity>();
        var grnStore = new List<GoodsReceiptEntity>();

        var repoA = new FakeInvoiceMatchRepository(invoiceStore, exceptionStore, TenantA, LeA);
        var repoB = new FakeInvoiceMatchRepository(invoiceStore, exceptionStore, TenantA, LeB);
        var suppliersA = new FakeSupplierRepository(supplierStore, TenantA, LeA);
        var posA = new FakePurchaseOrderRepository(poStore, TenantA, LeA);
        var products = new FakeProductReferenceValidator(new[] { Item1, Item2 });

        await suppliersA.CreateAsync(new SupplierEntity { SupplierId = Sup1 }, default);
        await posA.CreateAsync(new PurchaseOrderEntity { PoId = Po1, SupplierId = Sup1, Currency = Ccy }, default);

        var created = await new CaptureInvoiceHandler(repoA, suppliersA, posA, products).Handle(NewCapture(), default);
        var invoiceId = created.Data!.InvoiceId;

        // Same store, same tenant, DIFFERENT legal entity → invisible.
        var readB = await new GetInvoiceByIdHandler(repoB).Handle(new GetInvoiceByIdQuery(invoiceId), default);
        Assert.False(readB.IsSuccessful);
        Assert.Equal(404, readB.StatusCode);

        // Control: the owning LE can read it.
        var readA = await new GetInvoiceByIdHandler(repoA).Handle(new GetInvoiceByIdQuery(invoiceId), default);
        Assert.True(readA.IsSuccessful);
    }

    [Fact]
    public async Task Delete_captured_soft_deletes_but_matched_is_409()
    {
        var f = NewFixture();
        await SeedSupplierAndPo(f);
        await SeedGrn(f);

        // A Captured invoice soft-deletes and becomes invisible.
        var captured = await CaptureAsync(f, NewCapture(invoiceNumber: "INV-DEL"));
        var del = await f.Delete().Handle(new DeleteInvoiceCommand(captured), default);
        Assert.True(del.IsSuccessful);
        var read = await f.Get().Handle(new GetInvoiceByIdQuery(captured), default);
        Assert.Equal(404, read.StatusCode);

        // A Matched invoice cannot be deleted (append-only outcome) → 409.
        var matched = await CaptureAsync(f, NewCapture(invoiceNumber: "INV-KEEP"));
        await f.Match(new FakeMatchTolerancePolicy()).Handle(new RunThreeWayMatchCommand(matched, null, null), default);
        var delMatched = await f.Delete().Handle(new DeleteInvoiceCommand(matched), default);
        Assert.False(delMatched.IsSuccessful);
        Assert.Equal(409, delMatched.StatusCode);
        Assert.Contains("INVALID_STATE", delMatched.Errors);
    }

    [Fact]
    public async Task BulkDelete_removes_only_captured_invoices()
    {
        var f = NewFixture();
        await SeedSupplierAndPo(f);
        await SeedGrn(f);

        var captured = await CaptureAsync(f, NewCapture(invoiceNumber: "INV-C"));
        var matched = await CaptureAsync(f, NewCapture(invoiceNumber: "INV-M"));
        await f.Match(new FakeMatchTolerancePolicy()).Handle(new RunThreeWayMatchCommand(matched, null, null), default);

        var result = await f.BulkDelete().Handle(new BulkDeleteInvoiceCommand(new List<string> { captured, matched }), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data); // only the Captured one deleted

        Assert.Equal(404, (await f.Get().Handle(new GetInvoiceByIdQuery(captured), default)).StatusCode);
        Assert.True((await f.Get().Handle(new GetInvoiceByIdQuery(matched), default)).IsSuccessful);
    }
}
