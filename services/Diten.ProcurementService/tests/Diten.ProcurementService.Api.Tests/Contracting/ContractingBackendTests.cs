using Diten.ProcurementService.Api.Tests.Sourcing;
using Diten.ProcurementService.Api.Tests.Suppliers;
using Diten.ProcurementService.Application.Features.Clause.Commands;
using Diten.ProcurementService.Application.Features.Clause.Handlers.CommandHandlers;
using Diten.ProcurementService.Application.Features.Clause.Handlers.QueryHandlers;
using Diten.ProcurementService.Application.Features.Clause.Queries;
using Diten.ProcurementService.Application.Features.Contract;
using Diten.ProcurementService.Application.Features.Contract.Commands;
using Diten.ProcurementService.Application.Features.Contract.Handlers.CommandHandlers;
using Diten.ProcurementService.Application.Features.Contract.Handlers.QueryHandlers;
using Diten.ProcurementService.Application.Features.Contract.Queries;
using Diten.ProcurementService.Application.Features.Contract.Validators;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Xunit;
using BidEntity = Diten.ProcurementService.Domain.Entities.Bid;
using ClauseEntity = Diten.ProcurementService.Domain.Entities.Clause;
using ContractEntity = Diten.ProcurementService.Domain.Entities.Contract;
using RfxEntity = Diten.ProcurementService.Domain.Entities.RfxEvent;
using SupplierEntity = Diten.ProcurementService.Domain.Entities.Supplier;

namespace Diten.ProcurementService.Api.Tests.Contracting;

/// <summary>
/// MOD-0144 Contracting &amp; Clause Library backend behaviour tests — the FINAL P2P slice. Each pins a contract/pack
/// rule against PRODUCTION handlers/validators over the tenant+LE-scoped FakeContractingRepository and the CONSUMED
/// SUPPLIER (MOD-0140) / SOURCING award (MOD-0145) seams (fail-closed). Central rules: supplier/rfx are CONSUMED not
/// owned (unknown → 404 UNKNOWN_REFERENCE); the lifecycle state machine gates activate (Draft/InReview→Active only,
/// else 409 INVALID_STATE); clause library is duplicate-guarded (Category+Title → 409 DUPLICATE_CLAUSE) and IMMUTABLE
/// (no update/delete); document BINARY is never stored (only evidenceRef references). The Mongo filter is pinned in
/// ContractingRepository.cs.
/// </summary>
public sealed class ContractingBackendTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid LeA = Guid.NewGuid();
    private static readonly Guid LeB = Guid.NewGuid();

    private const string Sup1 = "SUP-1";
    private const string Rfx1 = "RFX-1";

    private sealed class Fixture
    {
        public List<ContractEntity> ContractStore = new();
        public List<ClauseEntity> ClauseStore = new();
        public List<SupplierEntity> SupplierStore = new();
        public List<RfxEntity> RfxStore = new();
        public List<BidEntity> BidStore = new();
        public required IContractingRepository Contracting;
        public required ISupplierRepository Suppliers;
        public required IRfxRepository Rfx;

        public CreateContractHandler Create() => new(Contracting, Suppliers, Rfx);
        public UpdateContractHandler Update() => new(Contracting, Rfx);
        public TerminateContractHandler Terminate() => new(Contracting);
        public ActivateContractHandler Activate() => new(Contracting);
        public DeleteContractHandler Delete() => new(Contracting);
        public BulkDeleteContractHandler BulkDelete() => new(Contracting);
        public GetContractByIdHandler Get() => new(Contracting);
        public ListContractsHandler ListContracts() => new(Contracting);
        public CreateClauseHandler CreateClause() => new(Contracting);
        public ListClauseLibraryHandler ListClauses() => new(Contracting);
    }

    private static Fixture NewFixture(Guid? le = null)
    {
        var legalEntity = le ?? LeA;
        var contractStore = new List<ContractEntity>();
        var clauseStore = new List<ClauseEntity>();
        var supplierStore = new List<SupplierEntity>();
        var rfxStore = new List<RfxEntity>();
        var bidStore = new List<BidEntity>();
        return new Fixture
        {
            ContractStore = contractStore,
            ClauseStore = clauseStore,
            SupplierStore = supplierStore,
            RfxStore = rfxStore,
            BidStore = bidStore,
            Contracting = new FakeContractingRepository(contractStore, clauseStore, TenantA, legalEntity),
            Suppliers = new FakeSupplierRepository(supplierStore, TenantA, legalEntity),
            Rfx = new FakeRfxRepository(rfxStore, bidStore, TenantA, legalEntity)
        };
    }

    private static async Task SeedSupplier(Fixture f)
        => await f.Suppliers.CreateAsync(new SupplierEntity { SupplierId = Sup1 }, default);

    private static async Task SeedRfx(Fixture f)
        => await f.Rfx.CreateAsync(new RfxEntity { RfxId = Rfx1 }, default);

    private static async Task<string> SeedClause(Fixture f, string clauseId = "CL-PAY", string category = "Payment", string title = "Net 30")
    {
        await f.Contracting.CreateClauseAsync(new ClauseEntity { ClauseId = clauseId, Category = category, Title = title, Body = "payment terms" }, default);
        return clauseId;
    }

    private static CreateContractCommand NewCreate(
        string supplierId = Sup1,
        string? rfxId = null,
        string title = "2026 Framework",
        string effectiveFrom = "2026-10-01",
        string? effectiveTo = "2027-09-30",
        string? currency = "TRY",
        List<ClauseRefInput>? clauses = null,
        List<string>? evidenceRefs = null,
        string? idem = null)
        => new(supplierId, rfxId, title, effectiveFrom, effectiveTo, currency, clauses, evidenceRefs, idem);

    private static async Task<string> CreateAsync(Fixture f, CreateContractCommand? cmd = null)
    {
        var result = await f.Create().Handle(cmd ?? NewCreate(), default);
        Assert.Equal(201, result.StatusCode);
        return result.Data!.ContractId;
    }

    // ══ CREATE ═══════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Create_happy_path_is_201_draft()
    {
        var f = NewFixture();
        await SeedSupplier(f);

        var result = await f.Create().Handle(NewCreate(idem: "c-1"), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(ContractStatus.Draft, result.Data!.Status);
        Assert.StartsWith("CTR-", result.Data.ContractId);
        Assert.Null(result.Data.WorkflowInstanceId); // approval trail not started until activate
        Assert.Single(f.ContractStore);
    }

    [Fact]
    public async Task Create_unknown_supplier_is_404_fail_closed()
    {
        var f = NewFixture(); // supplier NOT seeded

        var result = await f.Create().Handle(NewCreate(), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("UNKNOWN_REFERENCE", result.Errors);
        Assert.Empty(f.ContractStore); // fail-closed: nothing written

        // Vacuity control: once the supplier exists, the same create succeeds.
        await SeedSupplier(f);
        var ok = await f.Create().Handle(NewCreate(), default);
        Assert.Equal(201, ok.StatusCode);
    }

    [Fact]
    public async Task Create_unknown_rfx_is_404_fail_closed()
    {
        var f = NewFixture();
        await SeedSupplier(f); // supplier exists but RFX NOT seeded

        var result = await f.Create().Handle(NewCreate(rfxId: Rfx1), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("UNKNOWN_REFERENCE", result.Errors);
        Assert.Empty(f.ContractStore);

        // Vacuity control: once the award/rfx exists, the same create succeeds and the link is kept.
        await SeedRfx(f);
        var ok = await f.Create().Handle(NewCreate(rfxId: Rfx1), default);
        Assert.Equal(201, ok.StatusCode);
        Assert.Equal(Rfx1, ok.Data!.RfxId);
    }

    [Fact]
    public async Task Create_with_unknown_clause_reference_is_422()
    {
        var f = NewFixture();
        await SeedSupplier(f);

        var result = await f.Create().Handle(
            NewCreate(clauses: new List<ClauseRefInput> { new("CL-NONE", null, null) }), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(422, result.StatusCode);
        Assert.Contains("VALIDATION_FAILED", result.Errors);
        Assert.Empty(f.ContractStore);
    }

    [Fact]
    public async Task Create_captures_clause_deviation()
    {
        var f = NewFixture();
        await SeedSupplier(f);
        var clauseId = await SeedClause(f);

        var result = await f.Create().Handle(NewCreate(clauses: new List<ClauseRefInput>
        {
            new(clauseId, true, "supplier-specific carve-out")
        }), default);

        Assert.Equal(201, result.StatusCode);
        var clause = Assert.Single(result.Data!.Clauses);
        Assert.Equal(clauseId, clause.ClauseId);
        Assert.True(clause.Deviation);
        Assert.Equal("supplier-specific carve-out", clause.DeviationText);
    }

    [Fact]
    public async Task Create_stores_evidence_refs_only_no_binary()
    {
        var f = NewFixture();
        await SeedSupplier(f);

        var result = await f.Create().Handle(
            NewCreate(evidenceRefs: new List<string> { "EVR-0029-001", "EVR-0031-002" }), default);

        Assert.Equal(201, result.StatusCode);
        Assert.Equal(new[] { "EVR-0029-001", "EVR-0031-002" }, result.Data!.EvidenceRefs);

        // The stored entity holds only string references (MOD-0029/0031) — no binary/blob field exists on Contract.
        var stored = Assert.Single(f.ContractStore);
        Assert.Equal(2, stored.EvidenceRefs.Count);
        Assert.All(stored.EvidenceRefs, r => Assert.IsType<string>(r));
    }

    [Fact]
    public async Task Create_empty_currency_defaults_to_server_base()
    {
        var f = NewFixture();
        await SeedSupplier(f);

        var result = await f.Create().Handle(NewCreate(currency: null), default);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(ContractingContract.DefaultCurrency, result.Data!.Currency); // ASSUMPTION-0144-04
    }

    [Fact]
    public async Task Create_is_idempotent_no_duplicate()
    {
        var f = NewFixture();
        await SeedSupplier(f);

        var first = await f.Create().Handle(NewCreate(idem: "c-x"), default);
        var replay = await f.Create().Handle(NewCreate(idem: "c-x"), default);

        Assert.Equal(first.Data!.ContractId, replay.Data!.ContractId);
        Assert.Single(f.ContractStore); // replay wrote nothing new
    }

    [Fact]
    public void Validator_rejects_missing_fields_and_bad_dates()
    {
        var v = new CreateContractValidator();

        Assert.False(v.Validate(NewCreate(supplierId: "")).IsValid);
        Assert.False(v.Validate(NewCreate(title: "")).IsValid);
        Assert.False(v.Validate(NewCreate(title: new string('x', 201))).IsValid); // max 200
        Assert.False(v.Validate(NewCreate(effectiveFrom: "")).IsValid);
        Assert.False(v.Validate(NewCreate(effectiveFrom: "01/10/2026")).IsValid);  // wrong format
        Assert.False(v.Validate(NewCreate(effectiveFrom: "2026-10-01", effectiveTo: "2026-09-30")).IsValid); // to < from

        // Vacuity control (K3): a well-formed command passes.
        Assert.True(v.Validate(NewCreate()).IsValid);
    }

    [Fact]
    public async Task Create_effective_to_before_from_is_422()
    {
        var f = NewFixture();
        await SeedSupplier(f);

        var result = await f.Create().Handle(
            NewCreate(effectiveFrom: "2026-10-01", effectiveTo: "2026-09-30"), default);
        Assert.Equal(422, result.StatusCode);
        Assert.Contains("VALIDATION_FAILED", result.Errors);
        Assert.Empty(f.ContractStore);
    }

    // ══ ACTIVATE (lifecycle state machine) ═════════════════════════════════════════════════════════

    [Fact]
    public async Task Activate_draft_transitions_to_active_with_approval_trail()
    {
        var f = NewFixture();
        await SeedSupplier(f);
        var contractId = await CreateAsync(f);

        var result = await f.Activate().Handle(new ActivateContractCommand(contractId, "a-1"), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ContractStatus.Active, result.Data!.Status);
        Assert.NotNull(result.Data.WorkflowInstanceId); // MOD-0023 approval instance (ASSUMPTION-0144-05)
        Assert.StartsWith("WF-", result.Data.WorkflowInstanceId);

        var read = await f.Get().Handle(new GetContractByIdQuery(contractId), default);
        Assert.Equal(ContractStatus.Active, read.Data!.Status);
    }

    [Fact]
    public async Task Activate_from_in_review_transitions_to_active()
    {
        var f = NewFixture();
        await SeedSupplier(f);
        var contractId = await CreateAsync(f);
        // Move to InReview (submit is additive/ASSUMPTION-0144-02; seed the state directly here).
        f.ContractStore.Single().Status = ContractStatus.InReview;

        var result = await f.Activate().Handle(new ActivateContractCommand(contractId, null), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(ContractStatus.Active, result.Data!.Status);
    }

    [Fact]
    public async Task Activate_already_active_is_409_state_conflict()
    {
        var f = NewFixture();
        await SeedSupplier(f);
        var contractId = await CreateAsync(f);

        var first = await f.Activate().Handle(new ActivateContractCommand(contractId, null), default);
        Assert.Equal(ContractStatus.Active, first.Data!.Status);

        var again = await f.Activate().Handle(new ActivateContractCommand(contractId, null), default);
        Assert.False(again.IsSuccessful);
        Assert.Equal(409, again.StatusCode);
        Assert.Contains("INVALID_STATE", again.Errors);
    }

    [Fact]
    public async Task Activate_terminated_contract_is_409()
    {
        var f = NewFixture();
        await SeedSupplier(f);
        var contractId = await CreateAsync(f);
        f.ContractStore.Single().Status = ContractStatus.Terminated;

        var result = await f.Activate().Handle(new ActivateContractCommand(contractId, null), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("INVALID_STATE", result.Errors);
    }

    [Fact]
    public async Task Activate_unknown_contract_is_404()
    {
        var f = NewFixture();
        var result = await f.Activate().Handle(new ActivateContractCommand("CTR-NONE", null), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("NOT_FOUND", result.Errors);
    }

    [Fact]
    public async Task Activate_is_idempotent_replay_no_second_transition()
    {
        var f = NewFixture();
        await SeedSupplier(f);
        var contractId = await CreateAsync(f);

        var first = await f.Activate().Handle(new ActivateContractCommand(contractId, "a-9"), default);
        var replay = await f.Activate().Handle(new ActivateContractCommand(contractId, "a-9"), default);

        Assert.True(first.IsSuccessful);
        Assert.True(replay.IsSuccessful); // replay returns the same active contract, not a 409
        Assert.Equal(first.Data!.WorkflowInstanceId, replay.Data!.WorkflowInstanceId);
        Assert.Equal(ContractStatus.Active, replay.Data.Status);
    }

    // ══ CLAUSE LIBRARY ═════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateClause_happy_path_is_201()
    {
        var f = NewFixture();

        var result = await f.CreateClause().Handle(new CreateClauseCommand("Payment", "Net 30", "Ödeme 30 gün", "cl-1"), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        Assert.StartsWith("CL-", result.Data!.ClauseId);
        Assert.Single(f.ClauseStore);
    }

    [Fact]
    public async Task CreateClause_duplicate_category_plus_title_is_409()
    {
        var f = NewFixture();

        var first = await f.CreateClause().Handle(new CreateClauseCommand("Payment", "Net 30", "body-1", null), default);
        Assert.Equal(201, first.StatusCode);

        var dup = await f.CreateClause().Handle(new CreateClauseCommand("Payment", "Net 30", "body-2", null), default);
        Assert.False(dup.IsSuccessful);
        Assert.Equal(409, dup.StatusCode);
        Assert.Contains("DUPLICATE_CLAUSE", dup.Errors);
        Assert.Single(f.ClauseStore); // no second record

        // Vacuity control: a different title under the same category is allowed.
        var ok = await f.CreateClause().Handle(new CreateClauseCommand("Payment", "Net 60", "body-3", null), default);
        Assert.Equal(201, ok.StatusCode);
    }

    [Fact]
    public async Task CreateClause_is_idempotent_no_duplicate()
    {
        var f = NewFixture();

        var first = await f.CreateClause().Handle(new CreateClauseCommand("Delivery", "Incoterms", "DAP", "cl-x"), default);
        var replay = await f.CreateClause().Handle(new CreateClauseCommand("Delivery", "Incoterms", "DAP", "cl-x"), default);

        Assert.Equal(first.Data!.ClauseId, replay.Data!.ClauseId);
        Assert.Single(f.ClauseStore);
    }

    [Fact]
    public async Task ListClauseLibrary_filters_by_category()
    {
        var f = NewFixture();
        await f.CreateClause().Handle(new CreateClauseCommand("Payment", "Net 30", "b", null), default);
        await f.CreateClause().Handle(new CreateClauseCommand("Delivery", "Incoterms", "b", null), default);

        var all = await f.ListClauses().Handle(new ListClauseLibraryQuery(), default);
        Assert.Equal(2, all.Data!.Items.Count);

        var payment = await f.ListClauses().Handle(new ListClauseLibraryQuery("Payment"), default);
        Assert.Single(payment.Data!.Items);
        Assert.Equal("Payment", payment.Data.Items[0].Category);
    }

    // ══ LIST CONTRACTS ═════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ListContracts_filters_by_supplier_and_status()
    {
        var f = NewFixture();
        await SeedSupplier(f);
        await f.Suppliers.CreateAsync(new SupplierEntity { SupplierId = "SUP-2" }, default);

        var c1 = await CreateAsync(f, NewCreate(supplierId: Sup1));
        await CreateAsync(f, NewCreate(supplierId: "SUP-2"));
        await f.Activate().Handle(new ActivateContractCommand(c1, null), default);

        var bySupplier = await f.ListContracts().Handle(new ListContractsQuery(SupplierId: Sup1), default);
        Assert.Single(bySupplier.Data!.Items);

        var active = await f.ListContracts().Handle(new ListContractsQuery(Status: ContractStatus.Active), default);
        Assert.Single(active.Data!.Items);
        Assert.Equal(c1, active.Data.Items[0].ContractId);

        var draft = await f.ListContracts().Handle(new ListContractsQuery(Status: ContractStatus.Draft), default);
        Assert.Single(draft.Data!.Items);
    }

    // ══ TENANT + LE ISOLATION + SOFT DELETE ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Cross_legal_entity_read_returns_404()
    {
        var contractStore = new List<ContractEntity>();
        var clauseStore = new List<ClauseEntity>();
        var supplierStore = new List<SupplierEntity>();
        var rfxStore = new List<RfxEntity>();
        var bidStore = new List<BidEntity>();

        var repoA = new FakeContractingRepository(contractStore, clauseStore, TenantA, LeA);
        var repoB = new FakeContractingRepository(contractStore, clauseStore, TenantA, LeB);
        var suppliersA = new FakeSupplierRepository(supplierStore, TenantA, LeA);
        var rfxA = new FakeRfxRepository(rfxStore, bidStore, TenantA, LeA);

        await suppliersA.CreateAsync(new SupplierEntity { SupplierId = Sup1 }, default);

        var created = await new CreateContractHandler(repoA, suppliersA, rfxA).Handle(NewCreate(), default);
        var contractId = created.Data!.ContractId;

        // Same store, same tenant, DIFFERENT legal entity → invisible.
        var readB = await new GetContractByIdHandler(repoB).Handle(new GetContractByIdQuery(contractId), default);
        Assert.False(readB.IsSuccessful);
        Assert.Equal(404, readB.StatusCode);

        // Control: the owning LE can read it.
        var readA = await new GetContractByIdHandler(repoA).Handle(new GetContractByIdQuery(contractId), default);
        Assert.True(readA.IsSuccessful);
    }

    [Fact]
    public async Task Cross_legal_entity_activate_returns_404()
    {
        var contractStore = new List<ContractEntity>();
        var clauseStore = new List<ClauseEntity>();
        var supplierStore = new List<SupplierEntity>();
        var rfxStore = new List<RfxEntity>();
        var bidStore = new List<BidEntity>();

        var repoA = new FakeContractingRepository(contractStore, clauseStore, TenantA, LeA);
        var repoB = new FakeContractingRepository(contractStore, clauseStore, TenantA, LeB);
        var suppliersA = new FakeSupplierRepository(supplierStore, TenantA, LeA);
        var rfxA = new FakeRfxRepository(rfxStore, bidStore, TenantA, LeA);

        await suppliersA.CreateAsync(new SupplierEntity { SupplierId = Sup1 }, default);
        var created = await new CreateContractHandler(repoA, suppliersA, rfxA).Handle(NewCreate(), default);

        var activateB = await new ActivateContractHandler(repoB).Handle(new ActivateContractCommand(created.Data!.ContractId, null), default);
        Assert.False(activateB.IsSuccessful);
        Assert.Equal(404, activateB.StatusCode);
    }

    [Fact]
    public async Task Delete_draft_soft_deletes_but_active_is_409()
    {
        var f = NewFixture();
        await SeedSupplier(f);

        // A Draft contract soft-deletes and becomes invisible.
        var draft = await CreateAsync(f, NewCreate());
        var del = await f.Delete().Handle(new DeleteContractCommand(draft), default);
        Assert.True(del.IsSuccessful);
        var read = await f.Get().Handle(new GetContractByIdQuery(draft), default);
        Assert.Equal(404, read.StatusCode);

        // An Active contract cannot be deleted (approval trail is durable) → 409.
        var active = await CreateAsync(f, NewCreate());
        await f.Activate().Handle(new ActivateContractCommand(active, null), default);
        var delActive = await f.Delete().Handle(new DeleteContractCommand(active), default);
        Assert.False(delActive.IsSuccessful);
        Assert.Equal(409, delActive.StatusCode);
        Assert.Contains("INVALID_STATE", delActive.Errors);
    }

    [Fact]
    public async Task BulkDelete_removes_only_draft_contracts()
    {
        var f = NewFixture();
        await SeedSupplier(f);

        var draft = await CreateAsync(f, NewCreate());
        var active = await CreateAsync(f, NewCreate());
        await f.Activate().Handle(new ActivateContractCommand(active, null), default);

        var result = await f.BulkDelete().Handle(new BulkDeleteContractCommand(new List<string> { draft, active }), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Data); // only the Draft one deleted

        Assert.Equal(404, (await f.Get().Handle(new GetContractByIdQuery(draft), default)).StatusCode);
        Assert.True((await f.Get().Handle(new GetContractByIdQuery(active), default)).IsSuccessful);
    }

    [Fact]
    public async Task Delete_unknown_contract_is_404()
    {
        var f = NewFixture();
        var result = await f.Delete().Handle(new DeleteContractCommand("CTR-NONE"), default);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("NOT_FOUND", result.Errors);
    }

    // ══ UPDATE + TERMINATE (additive; ASSUMPTION-0144-01/02) ═══════════════════════════════════════

    private static UpdateContractCommand NewUpdate(
        string contractId,
        string? rfxId = null,
        string title = "2026 Framework (rev)",
        string effectiveFrom = "2026-10-01",
        string? effectiveTo = "2027-09-30",
        string? currency = "TRY",
        List<ClauseRefInput>? clauses = null,
        List<string>? evidenceRefs = null)
        => new(contractId, rfxId, title, effectiveFrom, effectiveTo, currency, clauses, evidenceRefs);

    [Fact]
    public async Task Update_draft_applies_changes()
    {
        var f = NewFixture();
        await SeedSupplier(f);
        var contractId = await CreateAsync(f);

        var result = await f.Update().Handle(NewUpdate(contractId, title: "Yeni Başlık"), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal("Yeni Başlık", result.Data!.Title);
        Assert.Equal(ContractStatus.Draft, result.Data.Status);

        var read = await f.Get().Handle(new GetContractByIdQuery(contractId), default);
        Assert.Equal("Yeni Başlık", read.Data!.Title);
    }

    [Fact]
    public async Task Update_active_contract_is_409()
    {
        var f = NewFixture();
        await SeedSupplier(f);
        var contractId = await CreateAsync(f);
        await f.Activate().Handle(new ActivateContractCommand(contractId, null), default);

        var result = await f.Update().Handle(NewUpdate(contractId), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("INVALID_STATE", result.Errors);
    }

    [Fact]
    public async Task Update_unknown_rfx_is_404_fail_closed()
    {
        var f = NewFixture();
        await SeedSupplier(f);
        var contractId = await CreateAsync(f);

        var result = await f.Update().Handle(NewUpdate(contractId, rfxId: "RFX-NONE"), default);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("UNKNOWN_REFERENCE", result.Errors);
    }

    [Fact]
    public async Task Terminate_active_transitions_to_terminated()
    {
        var f = NewFixture();
        await SeedSupplier(f);
        var contractId = await CreateAsync(f);
        await f.Activate().Handle(new ActivateContractCommand(contractId, null), default);

        var result = await f.Terminate().Handle(new TerminateContractCommand(contractId), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(ContractStatus.Terminated, result.Data!.Status);
    }

    [Fact]
    public async Task Terminate_draft_contract_is_409()
    {
        var f = NewFixture();
        await SeedSupplier(f);
        var contractId = await CreateAsync(f);

        var result = await f.Terminate().Handle(new TerminateContractCommand(contractId), default);
        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("INVALID_STATE", result.Errors);
    }
}
