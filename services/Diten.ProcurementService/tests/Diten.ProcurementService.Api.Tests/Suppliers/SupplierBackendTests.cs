using Diten.ProcurementService.Application.Features.Supplier;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using Diten.ProcurementService.Application.Features.Supplier.Handlers.CommandHandlers;
using Diten.ProcurementService.Application.Features.Supplier.Handlers.QueryHandlers;
using Diten.ProcurementService.Application.Features.Supplier.Queries;
using Diten.ProcurementService.Application.Features.Supplier.Validators;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Xunit;

namespace Diten.ProcurementService.Api.Tests.Suppliers;

/// <summary>
/// MOD-0140 Supplier backend behaviour tests. Each pins a contract/pack rule against PRODUCTION handlers/validators
/// over the tenant+LE-scoped FakeSupplierRepository (the seam SupplierRepository.cs enforces via Mongo).
/// </summary>
public sealed class SupplierBackendTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid LeA = Guid.NewGuid();
    private static readonly Guid LeB = Guid.NewGuid();

    private static (List<Supplier> store, ISupplierRepository repoA) NewFixture()
    {
        var store = new List<Supplier>();
        return (store, new FakeSupplierRepository(store, TenantA, LeA));
    }

    private static CreateSupplierCommand NewCreate(string name = "Medip-Kim", string? taxId = null, string? idem = null)
        => new(name, "tr", taxId, new List<SupplierContactInput> { new("primary", "a@b.example", "+90") }, null, null, idem);

    // ── DUPLICATE ACTIVE TaxId → 409 (contract DuplicateSupplier) ──────────────────────────────
    [Fact]
    public async Task Duplicate_active_taxId_is_refused_409()
    {
        var (_, repo) = NewFixture();
        var handler = new CreateSupplierHandler(repo);

        var first = await handler.Handle(NewCreate(taxId: "1234567890"), default);
        Assert.True(first.IsSuccessful);
        Assert.Equal(201, first.StatusCode);

        var second = await handler.Handle(NewCreate(taxId: "1234567890"), default);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Contains("DUPLICATE_SUPPLIER", second.Errors);
    }

    // ── MISSING Name → validation fail (pack §12/§13) ──────────────────────────────────────────
    [Fact]
    public void Missing_name_fails_validation()
    {
        var validator = new CreateSupplierValidator();

        var missing = validator.Validate(NewCreate(name: ""));
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Errors, e => e.PropertyName == nameof(CreateSupplierCommand.Name));

        // Control: a well-formed command passes (the rule is not vacuously failing everything).
        var ok = validator.Validate(NewCreate(name: "Acme"));
        Assert.True(ok.IsValid);
    }

    // ── OPTIMISTIC CONCURRENCY → stale update 409 (contract ConcurrencyConflict) ───────────────
    [Fact]
    public async Task Stale_update_is_refused_409_concurrency()
    {
        var (_, repo) = NewFixture();
        var created = await new CreateSupplierHandler(repo).Handle(NewCreate(), default);
        var code = created.Data!.SupplierId;
        Assert.Equal(0, created.Data.Version);

        var update = new UpdateSupplierHandler(repo);

        // First update at version 0 succeeds → version becomes 1.
        var firstOk = await update.Handle(new UpdateSupplierCommand { SupplierId = code, Name = "Renamed", ExpectedVersion = 0 }, default);
        Assert.True(firstOk.IsSuccessful);
        Assert.Equal(1, firstOk.Data!.Version);

        // Second update still presenting the STALE version 0 → 409, no silent overwrite.
        var stale = await update.Handle(new UpdateSupplierCommand { SupplierId = code, Name = "Hijack", ExpectedVersion = 0 }, default);
        Assert.False(stale.IsSuccessful);
        Assert.Equal(409, stale.StatusCode);
        Assert.Contains("CONCURRENCY_CONFLICT", stale.Errors);
    }

    // ── ONBOARDING happy path (KYC/sanctions + doc evidenceRef + approval link) ────────────────
    [Fact]
    public async Task Onboarding_happy_path_records_outcomes_documents_and_approval()
    {
        var (_, repo) = NewFixture();
        var created = await new CreateSupplierHandler(repo).Handle(NewCreate(), default);
        var code = created.Data!.SupplierId;

        var onboard = new SubmitOnboardingCaseHandler(repo);
        var result = await onboard.Handle(new SubmitOnboardingCaseCommand
        {
            SupplierId = code,
            Kyc = new KycInput("Medip-Kim İlaç Ham. A.Ş.", "TR-00099", new List<string> { "A. Yılmaz" }),
            Documents = new List<OnboardingDocumentInput> { new("tax-certificate", "doc-EV-771") },
            SubmitForApproval = true
        }, default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(OnboardingStatus.InReview, result.Data!.OnboardingStatus);
        Assert.Equal(KycOutcome.Passed, result.Data.KycOutcome);
        Assert.Equal(SanctionsOutcome.Clear, result.Data.SanctionsOutcome);
        Assert.Single(result.Data.Documents);
        Assert.Equal("doc-EV-771", result.Data.Documents[0].EvidenceRef);
        Assert.NotNull(result.Data.Approval);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Approval!.WorkflowInstanceId));
    }

    [Fact]
    public void Onboarding_submit_requires_kyc_legalName_and_a_document()
    {
        var validator = new SubmitOnboardingCaseValidator();

        var bad = validator.Validate(new SubmitOnboardingCaseCommand { SupplierId = "SUP-1", SubmitForApproval = true });
        Assert.False(bad.IsValid);

        var good = validator.Validate(new SubmitOnboardingCaseCommand
        {
            SupplierId = "SUP-1",
            SubmitForApproval = true,
            Kyc = new KycInput("Legal Co", null, null),
            Documents = new List<OnboardingDocumentInput> { new("tax-certificate", "doc-1") }
        });
        Assert.True(good.IsValid);
    }

    // ── validateSuppliers marks unknown ids known:false (consumer fail-closed) ─────────────────
    [Fact]
    public async Task ValidateSuppliers_marks_unknown_ids_known_false()
    {
        var (_, repo) = NewFixture();
        var created = await new CreateSupplierHandler(repo).Handle(NewCreate(), default);
        var known = created.Data!.SupplierId;

        var handler = new ValidateSuppliersHandler(repo);
        var result = await handler.Handle(new ValidateSuppliersQuery(new List<string> { known, "SUP-9999" }), default);

        Assert.True(result.IsSuccessful);
        var byId = result.Data!.Results.ToDictionary(r => r.SupplierId);
        Assert.True(byId[known].Known);
        Assert.Equal(SupplierStatus.Active, byId[known].Status);
        Assert.False(byId["SUP-9999"].Known);
        Assert.Null(byId["SUP-9999"].Status);
    }

    // ── SOFT DELETE: deleted supplier is not returned ──────────────────────────────────────────
    [Fact]
    public async Task Soft_deleted_supplier_is_not_returned()
    {
        var (_, repo) = NewFixture();
        var created = await new CreateSupplierHandler(repo).Handle(NewCreate(), default);
        var code = created.Data!.SupplierId;

        var del = await new DeleteSupplierHandler(repo).Handle(new DeleteSupplierCommand(code), default);
        Assert.True(del.IsSuccessful);

        var byId = await new GetSupplierByIdHandler(repo).Handle(new GetSupplierByIdQuery(code), default);
        Assert.False(byId.IsSuccessful);
        Assert.Equal(404, byId.StatusCode);

        var list = await new GetSupplierListHandler(repo).Handle(new GetSupplierListQuery(), default);
        Assert.DoesNotContain(list.Data!.Items, i => i.SupplierId == code);
    }

    // ── TENANT + LE ISOLATION: cross-LE read → 404 ─────────────────────────────────────────────
    [Fact]
    public async Task Cross_legal_entity_read_returns_404()
    {
        var store = new List<Supplier>();
        var repoA = new FakeSupplierRepository(store, TenantA, LeA);
        var repoB = new FakeSupplierRepository(store, TenantA, LeB);

        var created = await new CreateSupplierHandler(repoA).Handle(NewCreate(), default);
        var code = created.Data!.SupplierId;

        // Same store, same tenant, DIFFERENT legal entity → invisible.
        var readB = await new GetSupplierByIdHandler(repoB).Handle(new GetSupplierByIdQuery(code), default);
        Assert.False(readB.IsSuccessful);
        Assert.Equal(404, readB.StatusCode);

        // Control: the owning LE can read it (isolation is not vacuously hiding everything).
        var readA = await new GetSupplierByIdHandler(repoA).Handle(new GetSupplierByIdQuery(code), default);
        Assert.True(readA.IsSuccessful);
    }

    // ── TENANT + LE ISOLATION: cross-LE write is blocked (→ 404, not silent overwrite) ─────────
    [Fact]
    public async Task Cross_legal_entity_write_is_blocked()
    {
        var store = new List<Supplier>();
        var repoA = new FakeSupplierRepository(store, TenantA, LeA);
        var repoB = new FakeSupplierRepository(store, TenantA, LeB);

        var created = await new CreateSupplierHandler(repoA).Handle(NewCreate(name: "Original"), default);
        var code = created.Data!.SupplierId;

        var writeB = await new UpdateSupplierHandler(repoB).Handle(
            new UpdateSupplierCommand { SupplierId = code, Name = "Tampered" }, default);
        Assert.False(writeB.IsSuccessful);
        Assert.Equal(404, writeB.StatusCode);

        // The record under LE-A is untouched.
        var readA = await new GetSupplierByIdHandler(repoA).Handle(new GetSupplierByIdQuery(code), default);
        Assert.Equal("Original", readA.Data!.Name);
    }

    // ── IDEMPOTENT create: same Idempotency-Key replays the same record, no duplicate ──────────
    [Fact]
    public async Task Create_is_idempotent_on_idempotency_key()
    {
        var (store, repo) = NewFixture();
        var handler = new CreateSupplierHandler(repo);

        var first = await handler.Handle(NewCreate(idem: "idem-1"), default);
        var second = await handler.Handle(NewCreate(idem: "idem-1"), default);

        Assert.Equal(first.Data!.SupplierId, second.Data!.SupplierId);
        Assert.Single(store);
    }
}
