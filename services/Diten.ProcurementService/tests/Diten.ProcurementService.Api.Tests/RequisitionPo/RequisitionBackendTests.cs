using Diten.ProcurementService.Api.Tests.Sourcing;
using Diten.ProcurementService.Application.Features.Requisition;
using Diten.ProcurementService.Application.Features.Requisition.Commands;
using Diten.ProcurementService.Application.Features.Requisition.Handlers.CommandHandlers;
using Diten.ProcurementService.Application.Features.Requisition.Handlers.QueryHandlers;
using Diten.ProcurementService.Application.Features.Requisition.Queries;
using Diten.ProcurementService.Application.Features.Requisition.Validators;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Xunit;
using RequisitionEntity = Diten.ProcurementService.Domain.Entities.Requisition;

namespace Diten.ProcurementService.Api.Tests.RequisitionPo;

/// <summary>
/// MOD-0141 Requisition backend behaviour tests. Each pins a contract/pack rule against PRODUCTION handlers/validators
/// over the tenant+LE-scoped FakeRequisitionRepository + the PRODUCT-MASTER consume seam (FakeProductReferenceValidator).
/// The Mongo filter itself is pinned in RequisitionRepository.cs.
/// </summary>
public sealed class RequisitionBackendTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid LeA = Guid.NewGuid();
    private static readonly Guid LeB = Guid.NewGuid();

    private const string Item1 = "b1f2c3d4-0000-0000-0000-000000000001";
    private const string Item2 = "b1f2c3d4-0000-0000-0000-000000000002";

    private sealed class Fixture
    {
        public List<RequisitionEntity> Store = new();
        public required IRequisitionRepository Requisitions;
        public required FakeProductReferenceValidator Products;

        public CreateRequisitionHandler Create() => new(Requisitions, Products);
        public SubmitRequisitionHandler Submit() => new(Requisitions);
        public DeleteRequisitionHandler Delete() => new(Requisitions);
        public BulkDeleteRequisitionHandler BulkDelete() => new(Requisitions);
    }

    private static Fixture NewFixture(IEnumerable<string>? knownItems = null, Guid? le = null)
    {
        var store = new List<RequisitionEntity>();
        var legalEntity = le ?? LeA;
        return new Fixture
        {
            Store = store,
            Requisitions = new FakeRequisitionRepository(store, TenantA, legalEntity),
            Products = new FakeProductReferenceValidator(knownItems ?? new[] { Item1, Item2 })
        };
    }

    private static CreateRequisitionCommand NewCreate(
        List<RequisitionLineInput>? lines = null,
        string? justification = "üretim hammadde ikmali",
        string? idem = null)
        => new(lines ?? new List<RequisitionLineInput> { new(Item1, null, "100.000", "BOX", "2026-10-01") }, justification, idem);

    // ── CREATE: draft happy path ────────────────────────────────────────────────
    [Fact]
    public async Task Create_requisition_starts_in_draft()
    {
        var f = NewFixture();
        var result = await f.Create().Handle(NewCreate(), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(RequisitionStatus.Draft, result.Data!.Status);
        Assert.StartsWith("REQ-", result.Data.RequisitionId);
        Assert.Single(result.Data.Lines);
        Assert.Equal("100.000", result.Data.Lines[0].Quantity);
    }

    // ── VALIDATION: empty lines / float quantity / zero quantity → validator 422 ──────
    [Fact]
    public void Validator_rejects_empty_lines_and_float_and_zero_quantity()
    {
        var validator = new CreateRequisitionValidator();

        Assert.False(validator.Validate(NewCreate(lines: new List<RequisitionLineInput>())).IsValid);   // empty lines
        Assert.False(validator.Validate(NewCreate(lines: new List<RequisitionLineInput> { new(Item1, null, "1.0e3", "BOX", null) })).IsValid); // float/sci notation
        Assert.False(validator.Validate(NewCreate(lines: new List<RequisitionLineInput> { new(Item1, null, "0", "BOX", null) })).IsValid);     // quantity not > 0
        Assert.False(validator.Validate(NewCreate(lines: new List<RequisitionLineInput> { new(Item1, null, "5", "", null) })).IsValid);        // missing uomId

        // Vacuity control (K3): a well-formed command passes.
        Assert.True(validator.Validate(NewCreate()).IsValid);
    }

    [Fact]
    public async Task Create_handler_returns_422_on_empty_lines()
    {
        var f = NewFixture();
        var result = await f.Create().Handle(NewCreate(lines: new List<RequisitionLineInput>()), default);

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
            NewCreate(lines: new List<RequisitionLineInput> { new(Item1, null, "12.5f", "BOX", null) }), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(422, result.StatusCode);
    }

    // ── CREATE: unknown item (0290 seam) → 404 (fail-closed) + control ──────────────────────────
    [Fact]
    public async Task Create_with_unknown_item_is_404_fail_closed()
    {
        var f = NewFixture(knownItems: new[] { Item1 }); // Item2 is NOT known
        var unknown = await f.Create().Handle(
            NewCreate(lines: new List<RequisitionLineInput> { new(Item2, null, "5", "BOX", null) }), default);
        Assert.False(unknown.IsSuccessful);
        Assert.Equal(404, unknown.StatusCode);
        Assert.Contains("UNKNOWN_REFERENCE", unknown.Errors);

        // Vacuity control: a known item passes.
        var ok = await f.Create().Handle(
            NewCreate(lines: new List<RequisitionLineInput> { new(Item1, null, "5", "BOX", null) }), default);
        Assert.Equal(201, ok.StatusCode);
    }

    // ── SUBMIT: draft→submitted (+ workflowInstanceId); repeat → 409 ─────────────────────────────
    [Fact]
    public async Task Submit_moves_draft_to_submitted_then_409_on_repeat()
    {
        var f = NewFixture();
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RequisitionId;

        var submitted = await f.Submit().Handle(new SubmitRequisitionCommand(code, null), default);
        Assert.True(submitted.IsSuccessful);
        Assert.Equal(RequisitionStatus.Submitted, submitted.Data!.Status);
        Assert.False(string.IsNullOrWhiteSpace(submitted.Data.WorkflowInstanceId)); // MOD-0023 seam assigned

        // Submitting an already-Submitted requisition (no idempotency key) → 409 INVALID_STATE.
        var again = await f.Submit().Handle(new SubmitRequisitionCommand(code, null), default);
        Assert.False(again.IsSuccessful);
        Assert.Equal(409, again.StatusCode);
        Assert.Contains("INVALID_STATE", again.Errors);
    }

    [Fact]
    public async Task Submit_is_idempotent_on_key_no_second_side_effect()
    {
        var f = NewFixture();
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RequisitionId;

        var first = await f.Submit().Handle(new SubmitRequisitionCommand(code, "sub-1"), default);
        var replay = await f.Submit().Handle(new SubmitRequisitionCommand(code, "sub-1"), default);

        Assert.True(first.IsSuccessful);
        Assert.True(replay.IsSuccessful);
        Assert.Equal(RequisitionStatus.Submitted, replay.Data!.Status);
        // Same version: the replay did not re-apply the transition.
        Assert.Equal(first.Data!.Version, replay.Data.Version);
    }

    [Fact]
    public async Task Submit_unknown_requisition_is_404()
    {
        var f = NewFixture();
        var result = await f.Submit().Handle(new SubmitRequisitionCommand("REQ-NONE", null), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    // ── DELETE: only Draft; non-Draft → 409; soft-deleted invisible ─────────────────────────────
    [Fact]
    public async Task Delete_draft_soft_deletes_and_hides_it()
    {
        var f = NewFixture();
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RequisitionId;

        var del = await f.Delete().Handle(new DeleteRequisitionCommand(code), default);
        Assert.True(del.IsSuccessful);

        var byId = await new GetRequisitionByIdHandler(f.Requisitions).Handle(new GetRequisitionByIdQuery(code), default);
        Assert.Equal(404, byId.StatusCode);

        var list = await new GetRequisitionListHandler(f.Requisitions).Handle(new GetRequisitionListQuery(), default);
        Assert.DoesNotContain(list.Data!.Items, i => i.RequisitionId == code);
    }

    [Fact]
    public async Task Delete_non_draft_requisition_is_409()
    {
        var f = NewFixture();
        var code = (await f.Create().Handle(NewCreate(), default)).Data!.RequisitionId;
        await f.Submit().Handle(new SubmitRequisitionCommand(code, null), default);

        var del = await f.Delete().Handle(new DeleteRequisitionCommand(code), default);
        Assert.False(del.IsSuccessful);
        Assert.Equal(409, del.StatusCode);
        Assert.Contains("INVALID_STATE", del.Errors);

        // Still visible/present (not deleted).
        var byId = await new GetRequisitionByIdHandler(f.Requisitions).Handle(new GetRequisitionByIdQuery(code), default);
        Assert.True(byId.IsSuccessful);
    }

    [Fact]
    public async Task BulkDelete_removes_only_draft_requisitions()
    {
        var f = NewFixture();
        var draft = (await f.Create().Handle(NewCreate(), default)).Data!.RequisitionId;
        var submitted = (await f.Create().Handle(NewCreate(), default)).Data!.RequisitionId;
        await f.Submit().Handle(new SubmitRequisitionCommand(submitted, null), default);

        var result = await f.BulkDelete().Handle(new BulkDeleteRequisitionCommand(new List<string> { draft, submitted }), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data); // only the Draft one deleted

        var list = await new GetRequisitionListHandler(f.Requisitions).Handle(new GetRequisitionListQuery(), default);
        Assert.DoesNotContain(list.Data!.Items, i => i.RequisitionId == draft);
        Assert.Contains(list.Data.Items, i => i.RequisitionId == submitted);
    }

    // ── CREATE: idempotent replay → no duplicate ────────────────────────────────────────────────
    [Fact]
    public async Task Create_is_idempotent_on_key()
    {
        var f = NewFixture();
        var first = await f.Create().Handle(NewCreate(idem: "idem-1"), default);
        var second = await f.Create().Handle(NewCreate(idem: "idem-1"), default);

        Assert.Equal(first.Data!.RequisitionId, second.Data!.RequisitionId);
        Assert.Single(f.Store);
    }

    // ── TENANT + LE ISOLATION: cross-LE read → 404 (+ owning-LE control) ────────────────────────
    [Fact]
    public async Task Cross_legal_entity_read_returns_404()
    {
        var store = new List<RequisitionEntity>();
        var repoA = new FakeRequisitionRepository(store, TenantA, LeA);
        var repoB = new FakeRequisitionRepository(store, TenantA, LeB);
        var products = new FakeProductReferenceValidator(new[] { Item1, Item2 });

        var created = await new CreateRequisitionHandler(repoA, products).Handle(NewCreate(), default);
        var code = created.Data!.RequisitionId;

        // Same store, same tenant, DIFFERENT legal entity → invisible.
        var readB = await new GetRequisitionByIdHandler(repoB).Handle(new GetRequisitionByIdQuery(code), default);
        Assert.False(readB.IsSuccessful);
        Assert.Equal(404, readB.StatusCode);

        // Control: the owning LE can read it (isolation is not vacuously hiding everything).
        var readA = await new GetRequisitionByIdHandler(repoA).Handle(new GetRequisitionByIdQuery(code), default);
        Assert.True(readA.IsSuccessful);
    }

    // ── TENANT + LE ISOLATION: cross-LE submit is blocked (→ 404, not silent transition) ────────
    [Fact]
    public async Task Cross_legal_entity_submit_is_blocked()
    {
        var store = new List<RequisitionEntity>();
        var repoA = new FakeRequisitionRepository(store, TenantA, LeA);
        var repoB = new FakeRequisitionRepository(store, TenantA, LeB);
        var products = new FakeProductReferenceValidator(new[] { Item1, Item2 });

        var created = await new CreateRequisitionHandler(repoA, products).Handle(NewCreate(), default);
        var code = created.Data!.RequisitionId;

        var submitB = await new SubmitRequisitionHandler(repoB).Handle(new SubmitRequisitionCommand(code, null), default);
        Assert.False(submitB.IsSuccessful);
        Assert.Equal(404, submitB.StatusCode);

        // The record under LE-A is untouched (still Draft).
        var readA = await new GetRequisitionByIdHandler(repoA).Handle(new GetRequisitionByIdQuery(code), default);
        Assert.Equal(RequisitionStatus.Draft, readA.Data!.Status);
    }
}
