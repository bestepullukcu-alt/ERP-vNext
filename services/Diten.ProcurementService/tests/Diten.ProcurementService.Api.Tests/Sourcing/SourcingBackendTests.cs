using Diten.ProcurementService.Api.Tests.Suppliers;
using Diten.ProcurementService.Application.Features.Sourcing;
using Diten.ProcurementService.Application.Features.Sourcing.Commands;
using Diten.ProcurementService.Application.Features.Sourcing.Handlers.CommandHandlers;
using Diten.ProcurementService.Application.Features.Sourcing.Handlers.QueryHandlers;
using Diten.ProcurementService.Application.Features.Sourcing.Queries;
using Diten.ProcurementService.Application.Features.Sourcing.Validators;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Xunit;

namespace Diten.ProcurementService.Api.Tests.Sourcing;

/// <summary>
/// MOD-0145 Sourcing (RFQ/RFP) backend behaviour tests. Each pins a contract/pack rule against PRODUCTION
/// handlers/validators over the tenant+LE-scoped FakeRfxRepository + the real Supplier seam (FakeSupplierRepository)
/// + the PRODUCT-MASTER consume seam (FakeProductReferenceValidator). The Mongo filter is pinned in RfxRepository.cs.
/// </summary>
public sealed class SourcingBackendTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid LeA = Guid.NewGuid();
    private static readonly Guid LeB = Guid.NewGuid();

    private const string Item1 = "b1f2c3d4-0000-0000-0000-000000000001";
    private const string Item2 = "b1f2c3d4-0000-0000-0000-000000000002";

    private sealed class Fixture
    {
        public List<RfxEvent> RfxStore = new();
        public List<Bid> BidStore = new();
        public List<Supplier> SupplierStore = new();
        public required IRfxRepository Rfx;
        public required ISupplierRepository Suppliers;
        public required FakeProductReferenceValidator Products;

        public CreateRfxEventHandler Create() => new(Rfx, Suppliers, Products);
        public PublishRfxEventHandler Publish() => new(Rfx);
        public SubmitBidHandler Bid() => new(Rfx, Suppliers, Products);
        public AwardRfxEventHandler Award() => new(Rfx);
        public DeleteRfxEventHandler Delete() => new(Rfx);
        public BulkDeleteRfxEventHandler BulkDelete() => new(Rfx);
    }

    private static Fixture NewFixture(IEnumerable<string>? knownItems = null, Guid? le = null)
    {
        var rfxStore = new List<RfxEvent>();
        var bidStore = new List<Bid>();
        var supplierStore = new List<Supplier>();
        var legalEntity = le ?? LeA;
        return new Fixture
        {
            RfxStore = rfxStore,
            BidStore = bidStore,
            SupplierStore = supplierStore,
            Rfx = new FakeRfxRepository(rfxStore, bidStore, TenantA, legalEntity),
            Suppliers = new FakeSupplierRepository(supplierStore, TenantA, legalEntity),
            // Default: only Item1/Item2 known (fail-closed enforced). Pass knownItems: null branch not used here.
            Products = new FakeProductReferenceValidator(knownItems ?? new[] { Item1, Item2 })
        };
    }

    private static async Task SeedSupplier(Fixture f, string supplierId)
        => await f.Suppliers.CreateAsync(new Supplier { SupplierId = supplierId, Name = supplierId, Status = SupplierStatus.Active }, default);

    private static CreateRfxEventCommand NewCreate(
        string title = "Hammadde Q4",
        List<string>? invited = null,
        List<RfxLineInput>? lines = null,
        string? idem = null)
        => new(RfxType.RFQ, title, null, invited, lines ?? new List<RfxLineInput> { new(Item1, "100.000", "BOX") }, idem);

    // ── CREATE: draft happy path ────────────────────────────────────────────────
    [Fact]
    public async Task Create_rfx_starts_in_draft()
    {
        var f = NewFixture();
        var result = await f.Create().Handle(NewCreate(), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(RfxStatus.Draft, result.Data!.Status);
        Assert.StartsWith("RFX-", result.Data.RfxId);
        Assert.Single(result.Data.Lines);
    }

    // ── VALIDATION: empty lines / missing title / float decimal → validator + handler 422 ──────
    [Fact]
    public void Validator_rejects_empty_lines_missing_title_and_float_quantity()
    {
        var validator = new CreateRfxEventValidator();

        Assert.False(validator.Validate(NewCreate(lines: new List<RfxLineInput>())).IsValid);   // empty lines
        Assert.False(validator.Validate(NewCreate(title: "")).IsValid);                          // missing title
        Assert.False(validator.Validate(NewCreate(lines: new List<RfxLineInput> { new(Item1, "1.0e3", "BOX") })).IsValid); // float/sci notation
        Assert.False(validator.Validate(NewCreate(lines: new List<RfxLineInput> { new(Item1, "0", "BOX") })).IsValid);     // quantity not > 0

        // Vacuity control (K3): a well-formed command passes.
        Assert.True(validator.Validate(NewCreate()).IsValid);
    }

    [Fact]
    public async Task Create_handler_returns_422_on_empty_lines()
    {
        var f = NewFixture();
        var result = await f.Create().Handle(NewCreate(lines: new List<RfxLineInput>()), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(422, result.StatusCode);
        Assert.Contains("VALIDATION_FAILED", result.Errors);

        // Vacuity control: a valid create succeeds in the same setup.
        var ok = await f.Create().Handle(NewCreate(), default);
        Assert.Equal(201, ok.StatusCode);
    }

    [Fact]
    public async Task Create_handler_returns_422_on_float_quantity()
    {
        var f = NewFixture();
        var result = await f.Create().Handle(
            NewCreate(lines: new List<RfxLineInput> { new(Item1, "12.5f", "BOX") }), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(422, result.StatusCode);
    }

    // ── CREATE: unknown invited supplier → 404 (fail-closed) + control ──────────────────────────
    [Fact]
    public async Task Create_with_unknown_invited_supplier_is_404_fail_closed()
    {
        var f = NewFixture();
        // No supplier seeded → SUP-2001 unknown.
        var result = await f.Create().Handle(NewCreate(invited: new List<string> { "SUP-2001" }), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("UNKNOWN_REFERENCE", result.Errors);

        // Vacuity control: once the supplier exists, the same invited create succeeds.
        await SeedSupplier(f, "SUP-2001");
        var ok = await f.Create().Handle(NewCreate(invited: new List<string> { "SUP-2001" }), default);
        Assert.Equal(201, ok.StatusCode);
    }

    // ── CREATE: unknown item (0290 seam) → 404 (fail-closed) + control ──────────────────────────
    [Fact]
    public async Task Create_with_unknown_item_is_404_fail_closed()
    {
        var f = NewFixture(knownItems: new[] { Item1 }); // Item2 is NOT known
        var unknown = await f.Create().Handle(
            NewCreate(lines: new List<RfxLineInput> { new(Item2, "5", "BOX") }), default);
        Assert.False(unknown.IsSuccessful);
        Assert.Equal(404, unknown.StatusCode);
        Assert.Contains("UNKNOWN_REFERENCE", unknown.Errors);

        // Vacuity control: a known item passes.
        var ok = await f.Create().Handle(
            NewCreate(lines: new List<RfxLineInput> { new(Item1, "5", "BOX") }), default);
        Assert.Equal(201, ok.StatusCode);
    }

    // ── PUBLISH: draft→published; already published → 409; idempotent replay ────────────────────
    [Fact]
    public async Task Publish_moves_draft_to_published_then_409_on_repeat()
    {
        var f = NewFixture();
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;

        var published = await f.Publish().Handle(new PublishRfxEventCommand(code, null), default);
        Assert.True(published.IsSuccessful);
        Assert.Equal(RfxStatus.Published, published.Data!.Status);

        // Publishing an already-Published RFx (no idempotency key) → 409 INVALID_STATE.
        var again = await f.Publish().Handle(new PublishRfxEventCommand(code, null), default);
        Assert.False(again.IsSuccessful);
        Assert.Equal(409, again.StatusCode);
        Assert.Contains("INVALID_STATE", again.Errors);
    }

    [Fact]
    public async Task Publish_is_idempotent_on_key_no_second_side_effect()
    {
        var f = NewFixture();
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;

        var first = await f.Publish().Handle(new PublishRfxEventCommand(code, "pub-1"), default);
        var replay = await f.Publish().Handle(new PublishRfxEventCommand(code, "pub-1"), default);

        Assert.True(first.IsSuccessful);
        Assert.True(replay.IsSuccessful);
        Assert.Equal(RfxStatus.Published, replay.Data!.Status);
        // Same version: the replay did not re-apply the transition.
        Assert.Equal(first.Data!.Version, replay.Data.Version);
    }

    [Fact]
    public async Task Publish_unknown_rfx_is_404()
    {
        var f = NewFixture();
        var result = await f.Publish().Handle(new PublishRfxEventCommand("RFX-NONE", null), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    // ── BID: only on Published + before closesAt; else 409 (no record) ──────────────────────────
    [Fact]
    public async Task Bid_on_unpublished_rfx_is_409_and_records_nothing()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId; // Draft

        var result = await f.Bid().Handle(
            new SubmitBidCommand(code, "SUP-1", new List<BidLineInput> { new(Item1, "12.50", 14) }, null), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("INVALID_STATE", result.Errors);

        // No bid recorded.
        var bids = await new GetBidListHandler(f.Rfx).Handle(new GetBidListQuery(code), default);
        Assert.Empty(bids.Data!.Items);
    }

    [Fact]
    public async Task Bid_on_closed_rfx_is_409()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        // Create with a closesAt in the past, then publish.
        var create = new CreateRfxEventCommand(RfxType.RFQ, "Closed", DateTimeOffset.UtcNow.AddHours(-1), null,
            new List<RfxLineInput> { new(Item1, "1", "BOX") }, null);
        var code = (await f.Create().Handle(create, default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(code, null), default);

        var result = await f.Bid().Handle(
            new SubmitBidCommand(code, "SUP-1", new List<BidLineInput> { new(Item1, "1", null) }, null), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task Bid_happy_path_on_published_is_201()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(code, null), default);

        var result = await f.Bid().Handle(
            new SubmitBidCommand(code, "SUP-1", new List<BidLineInput> { new(Item1, "12.5000", 14) }, null), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("SUP-1", result.Data!.SupplierId);
        Assert.StartsWith("BID-", result.Data.BidId);
    }

    // ── BID: unknown supplier → 404 (fail-closed) + control ─────────────────────────────────────
    [Fact]
    public async Task Bid_unknown_supplier_is_404_fail_closed()
    {
        var f = NewFixture();
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(code, null), default);

        // SUP-404 not seeded → unknown → fail-closed 404.
        var unknown = await f.Bid().Handle(
            new SubmitBidCommand(code, "SUP-404", new List<BidLineInput> { new(Item1, "1", null) }, null), default);
        Assert.False(unknown.IsSuccessful);
        Assert.Equal(404, unknown.StatusCode);
        Assert.Contains("UNKNOWN_REFERENCE", unknown.Errors);

        // Vacuity control: a known supplier can bid on the same RFx.
        await SeedSupplier(f, "SUP-OK");
        var ok = await f.Bid().Handle(
            new SubmitBidCommand(code, "SUP-OK", new List<BidLineInput> { new(Item1, "1", null) }, null), default);
        Assert.Equal(201, ok.StatusCode);
    }

    // ── BID: supplier not on invited list → 422 (pack §12) ──────────────────────────────────────
    [Fact]
    public async Task Bid_from_uninvited_supplier_is_422_when_invited_list_present()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-INV");
        await SeedSupplier(f, "SUP-OUT");
        var code = (await f.Create().Handle(NewCreate(invited: new List<string> { "SUP-INV" }), default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(code, null), default);

        var result = await f.Bid().Handle(
            new SubmitBidCommand(code, "SUP-OUT", new List<BidLineInput> { new(Item1, "1", null) }, null), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(422, result.StatusCode);

        // Control: the invited supplier can bid.
        var ok = await f.Bid().Handle(
            new SubmitBidCommand(code, "SUP-INV", new List<BidLineInput> { new(Item1, "1", null) }, null), default);
        Assert.Equal(201, ok.StatusCode);
    }

    // ── BID: idempotent replay → no duplicate ───────────────────────────────────────────────────
    [Fact]
    public async Task Bid_is_idempotent_on_key()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(code, null), default);

        var cmd = new SubmitBidCommand(code, "SUP-1", new List<BidLineInput> { new(Item1, "1", null) }, "bid-1");
        var first = await f.Bid().Handle(cmd, default);
        var replay = await f.Bid().Handle(cmd, default);

        Assert.Equal(first.Data!.BidId, replay.Data!.BidId);
        Assert.Single(f.BidStore);
    }

    // ── AWARD: happy path sets AwardDecision + Awarded + resolves supplier from bid ─────────────
    [Fact]
    public async Task Award_happy_path_sets_award_and_resolves_supplier()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(code, null), default);
        var bid = await f.Bid().Handle(
            new SubmitBidCommand(code, "SUP-1", new List<BidLineInput> { new(Item1, "9.99", 7) }, null), default);

        var result = await f.Award().Handle(new AwardRfxEventCommand(code, bid.Data!.BidId, "en düşük TCO", null), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(bid.Data.BidId, result.Data!.AwardedBidId);
        Assert.Equal("SUP-1", result.Data.AwardedSupplierId); // server-resolved from bid
        Assert.Equal("en düşük TCO", result.Data.Rationale);

        // RFx is now Awarded.
        var rfx = await new GetRfxEventByIdHandler(f.Rfx).Handle(new GetRfxEventByIdQuery(code), default);
        Assert.Equal(RfxStatus.Awarded, rfx.Data!.Status);
    }

    // ── AWARD: without bids → 409; foreign/nonexistent bid → 404 ────────────────────────────────
    [Fact]
    public async Task Award_without_bids_is_409()
    {
        var f = NewFixture();
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(code, null), default);

        var result = await f.Award().Handle(new AwardRfxEventCommand(code, "BID-NONE", null, null), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("INVALID_STATE", result.Errors);
    }

    [Fact]
    public async Task Award_with_foreign_bid_is_404()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");

        // RFx-A with its own bid.
        var codeA = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(codeA, null), default);
        var bidA = await f.Bid().Handle(
            new SubmitBidCommand(codeA, "SUP-1", new List<BidLineInput> { new(Item1, "1", null) }, null), default);

        // RFx-B with its own bid; then try to award B using A's bid.
        var codeB = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(codeB, null), default);
        await f.Bid().Handle(
            new SubmitBidCommand(codeB, "SUP-1", new List<BidLineInput> { new(Item1, "1", null) }, null), default);

        var result = await f.Award().Handle(new AwardRfxEventCommand(codeB, bidA.Data!.BidId, null, null), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);

        // RFx-B is untouched (still Published, no award).
        var rfxB = await new GetRfxEventByIdHandler(f.Rfx).Handle(new GetRfxEventByIdQuery(codeB), default);
        Assert.Equal(RfxStatus.Published, rfxB.Data!.Status);
    }

    // ── AWARD: idempotent replay → no re-award ──────────────────────────────────────────────────
    [Fact]
    public async Task Award_is_idempotent_on_key()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(code, null), default);
        var bid = await f.Bid().Handle(
            new SubmitBidCommand(code, "SUP-1", new List<BidLineInput> { new(Item1, "1", null) }, null), default);

        var first = await f.Award().Handle(new AwardRfxEventCommand(code, bid.Data!.BidId, "r", "aw-1"), default);
        var replay = await f.Award().Handle(new AwardRfxEventCommand(code, bid.Data.BidId, "r", "aw-1"), default);

        Assert.True(first.IsSuccessful);
        Assert.True(replay.IsSuccessful);
        Assert.Equal(first.Data!.DecidedAt, replay.Data!.DecidedAt); // same decision, not re-awarded
    }

    // ── AWARD: on already-awarded RFx (different key) → 409 ─────────────────────────────────────
    [Fact]
    public async Task Award_on_already_awarded_rfx_is_409()
    {
        var f = NewFixture();
        await SeedSupplier(f, "SUP-1");
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(code, null), default);
        var bid = await f.Bid().Handle(
            new SubmitBidCommand(code, "SUP-1", new List<BidLineInput> { new(Item1, "1", null) }, null), default);
        await f.Award().Handle(new AwardRfxEventCommand(code, bid.Data!.BidId, null, "aw-1"), default);

        // Second award with a DIFFERENT key on an Awarded RFx → 409.
        var again = await f.Award().Handle(new AwardRfxEventCommand(code, bid.Data.BidId, null, "aw-2"), default);
        Assert.False(again.IsSuccessful);
        Assert.Equal(409, again.StatusCode);
    }

    // ── DELETE: only Draft; non-Draft → 409; soft-deleted invisible ─────────────────────────────
    [Fact]
    public async Task Delete_draft_soft_deletes_and_hides_it()
    {
        var f = NewFixture();
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;

        var del = await f.Delete().Handle(new DeleteRfxEventCommand(code), default);
        Assert.True(del.IsSuccessful);

        var byId = await new GetRfxEventByIdHandler(f.Rfx).Handle(new GetRfxEventByIdQuery(code), default);
        Assert.Equal(404, byId.StatusCode);

        var list = await new GetRfxEventListHandler(f.Rfx).Handle(new GetRfxEventListQuery(), default);
        Assert.DoesNotContain(list.Data!.Items, i => i.RfxId == code);
    }

    [Fact]
    public async Task Delete_non_draft_rfx_is_409()
    {
        var f = NewFixture();
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(code, null), default);

        var del = await f.Delete().Handle(new DeleteRfxEventCommand(code), default);
        Assert.False(del.IsSuccessful);
        Assert.Equal(409, del.StatusCode);
        Assert.Contains("INVALID_STATE", del.Errors);

        // Still visible/present (not deleted).
        var byId = await new GetRfxEventByIdHandler(f.Rfx).Handle(new GetRfxEventByIdQuery(code), default);
        Assert.True(byId.IsSuccessful);
    }

    [Fact]
    public async Task BulkDelete_removes_only_draft_rfx()
    {
        var f = NewFixture();
        var draft = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;
        var published = (await f.Create().Handle(NewCreate(), default)).Data!.RfxId;
        await f.Publish().Handle(new PublishRfxEventCommand(published, null), default);

        var result = await f.BulkDelete().Handle(new BulkDeleteRfxEventCommand(new List<string> { draft, published }), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data); // only the Draft one deleted

        var list = await new GetRfxEventListHandler(f.Rfx).Handle(new GetRfxEventListQuery(), default);
        Assert.DoesNotContain(list.Data!.Items, i => i.RfxId == draft);
        Assert.Contains(list.Data.Items, i => i.RfxId == published);
    }

    // ── CREATE: idempotent replay → no duplicate ────────────────────────────────────────────────
    [Fact]
    public async Task Create_is_idempotent_on_key()
    {
        var f = NewFixture();
        var first = await f.Create().Handle(NewCreate(idem: "idem-1"), default);
        var second = await f.Create().Handle(NewCreate(idem: "idem-1"), default);

        Assert.Equal(first.Data!.RfxId, second.Data!.RfxId);
        Assert.Single(f.RfxStore);
    }

    // ── TENANT + LE ISOLATION: cross-LE read → 404 (+ owning-LE control) ────────────────────────
    [Fact]
    public async Task Cross_legal_entity_read_returns_404()
    {
        var rfxStore = new List<RfxEvent>();
        var bidStore = new List<Bid>();
        var supplierStore = new List<Supplier>();
        var repoA = new FakeRfxRepository(rfxStore, bidStore, TenantA, LeA);
        var repoB = new FakeRfxRepository(rfxStore, bidStore, TenantA, LeB);
        var supA = new FakeSupplierRepository(supplierStore, TenantA, LeA);
        var products = new FakeProductReferenceValidator(new[] { Item1, Item2 });

        var created = await new CreateRfxEventHandler(repoA, supA, products).Handle(NewCreate(), default);
        var code = created.Data!.RfxId;

        // Same store, same tenant, DIFFERENT legal entity → invisible.
        var readB = await new GetRfxEventByIdHandler(repoB).Handle(new GetRfxEventByIdQuery(code), default);
        Assert.False(readB.IsSuccessful);
        Assert.Equal(404, readB.StatusCode);

        // Control: the owning LE can read it (isolation is not vacuously hiding everything).
        var readA = await new GetRfxEventByIdHandler(repoA).Handle(new GetRfxEventByIdQuery(code), default);
        Assert.True(readA.IsSuccessful);
    }

    // ── TENANT + LE ISOLATION: cross-LE publish is blocked (→ 404, not silent transition) ───────
    [Fact]
    public async Task Cross_legal_entity_publish_is_blocked()
    {
        var rfxStore = new List<RfxEvent>();
        var bidStore = new List<Bid>();
        var supplierStore = new List<Supplier>();
        var repoA = new FakeRfxRepository(rfxStore, bidStore, TenantA, LeA);
        var repoB = new FakeRfxRepository(rfxStore, bidStore, TenantA, LeB);
        var supA = new FakeSupplierRepository(supplierStore, TenantA, LeA);
        var products = new FakeProductReferenceValidator(new[] { Item1, Item2 });

        var created = await new CreateRfxEventHandler(repoA, supA, products).Handle(NewCreate(), default);
        var code = created.Data!.RfxId;

        var publishB = await new PublishRfxEventHandler(repoB).Handle(new PublishRfxEventCommand(code, null), default);
        Assert.False(publishB.IsSuccessful);
        Assert.Equal(404, publishB.StatusCode);

        // The record under LE-A is untouched (still Draft).
        var readA = await new GetRfxEventByIdHandler(repoA).Handle(new GetRfxEventByIdQuery(code), default);
        Assert.Equal(RfxStatus.Draft, readA.Data!.Status);
    }
}
