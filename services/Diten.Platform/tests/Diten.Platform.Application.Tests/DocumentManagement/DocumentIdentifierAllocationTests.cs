using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU07 — UID / Document Code allocation engine tests. Tenant-aware in-memory fakes exercise never-reuse
/// (including cancelled/soft-deleted), idempotency, manual conflict, type-code mapping, eligibility and gaps.
/// </summary>
public sealed class DocumentIdentifierAllocationTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private const string Corr = "fu07-corr-1";

    [Fact]
    public async Task AllocateUid_sets_permanent_uid_and_ledger_record()
    {
        var f = Fixture();
        var entry = SeedEntry(f);

        var response = await f.Service.AllocateUidAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("UID-0000001", response.Data!.PermanentUid);
        Assert.True(response.Data.IsSystemAllocated);
        var ledger = Assert.Single(f.Ledger.Items);
        Assert.Equal(DocumentIdentifierType.PermanentUid, ledger.IdentifierType);
        Assert.Equal("UID-0000001", ledger.IdentifierValue);
        Assert.Equal(DocumentIdentifierAllocationStatus.Assigned, ledger.AllocationStatus);
        Assert.True(ledger.IsSystemAllocated);
        Assert.Equal("UID-0000001", f.Register.Items.Single().PermanentUid);
    }

    [Fact]
    public async Task AllocateUid_is_idempotent_when_system_uid_exists()
    {
        var f = Fixture();
        var entry = SeedEntry(f);
        await f.Service.AllocateUidAsync(entry.Id, null, Corr, CancellationToken.None);

        var again = await f.Service.AllocateUidAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.True(again.IsSuccessful);
        Assert.Equal("UID-0000001", again.Data!.PermanentUid);
        Assert.Single(f.Ledger.Items); // no second ledger row
    }

    [Fact]
    public async Task AllocateUid_rejects_when_manual_uid_exists()
    {
        var f = Fixture();
        var entry = SeedEntry(f);
        entry.PermanentUid = "MANUAL-UID-1"; // set manually, no system ledger row
        entry.IsSystemAllocated = false;

        var response = await f.Service.AllocateUidAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(IdentifierAllocationReasonCodes.ManualIdentifierExists, response.ReasonCode);
    }

    [Fact]
    public async Task AllocateCode_sets_document_code_and_ledger_record()
    {
        var f = Fixture();
        var entry = SeedEntry(f);

        var response = await f.Service.AllocateCodeAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("GMG-QMS-SOP-0001", response.Data!.DocumentCode);
        var ledger = Assert.Single(f.Ledger.Items);
        Assert.Equal("SOP", ledger.TypeCode);
        Assert.Equal("GMG-QMS-SOP-0001", ledger.IdentifierValue);
    }

    [Fact]
    public async Task AllocateCode_is_idempotent_when_system_code_exists()
    {
        var f = Fixture();
        var entry = SeedEntry(f);
        await f.Service.AllocateCodeAsync(entry.Id, null, Corr, CancellationToken.None);

        var again = await f.Service.AllocateCodeAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.True(again.IsSuccessful);
        Assert.Equal("GMG-QMS-SOP-0001", again.Data!.DocumentCode);
        Assert.Single(f.Ledger.Items);
    }

    [Fact]
    public async Task AllocateCode_rejects_when_manual_code_exists()
    {
        var f = Fixture();
        var entry = SeedEntry(f);
        entry.DocumentCode = "MANUAL-CODE-1";
        entry.IsSystemAllocated = false;

        var response = await f.Service.AllocateCodeAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(IdentifierAllocationReasonCodes.ManualIdentifierExists, response.ReasonCode);
    }

    [Fact]
    public async Task AllocateIdentifiers_sets_uid_and_code()
    {
        var f = Fixture();
        var entry = SeedEntry(f);

        var response = await f.Service.AllocateIdentifiersAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("UID-0000001", response.Data!.PermanentUid);
        Assert.Equal("GMG-QMS-SOP-0001", response.Data.DocumentCode);
        Assert.NotNull(response.Data.UidAllocation);
        Assert.NotNull(response.Data.CodeAllocation);
        Assert.Equal(2, f.Ledger.Items.Count);
    }

    [Fact]
    public async Task AllocateCode_uses_class_or_type_mapping()
    {
        var f = Fixture();
        // Bundled class resolves via DocumentType (Form → FRM).
        var entry = SeedEntry(f, documentClass: ControlledDocumentClass.FormTemplateRegisterMatrixPlanChecklist, documentType: DocumentType.Form);

        var response = await f.Service.AllocateCodeAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("GMG-QMS-FRM-0001", response.Data!.DocumentCode);
    }

    [Fact]
    public async Task AllocateCode_blocks_when_type_mapping_missing()
    {
        var f = Fixture();
        var entry = SeedEntry(f, documentClass: ControlledDocumentClass.Other, documentType: DocumentType.Other);

        var response = await f.Service.AllocateCodeAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(IdentifierAllocationReasonCodes.TypeMappingMissing, response.ReasonCode);
    }

    [Fact]
    public async Task Duplicate_reserved_value_is_rejected()
    {
        var f = Fixture();
        await f.Service.ReserveAsync(new ReserveIdentifierInput("PermanentUid", "UID-9000001", null, "Migration", null, "LegacyX", null), Corr, CancellationToken.None);

        var response = await f.Service.ReserveAsync(new ReserveIdentifierInput("PermanentUid", "UID-9000001", null, "Migration", null, null, null), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(IdentifierAllocationReasonCodes.DuplicateIdentifier, response.ReasonCode);
    }

    [Fact]
    public async Task Cancelled_or_soft_deleted_identifiers_are_still_not_reused()
    {
        var f = Fixture();
        var reserved = await f.Service.ReserveAsync(new ReserveIdentifierInput("DocumentCode", "GMG-QMS-SOP-9001", null, "Migration", null, null, null), Corr, CancellationToken.None);
        await f.Service.CancelAsync(reserved.Data!.Id, new CancelIdentifierInput("abandoned"), Corr, CancellationToken.None);
        Assert.Equal(DocumentIdentifierAllocationStatus.Cancelled, f.Ledger.Items.Single().AllocationStatus);

        // Same value cannot be reserved again even though it is cancelled.
        var reReserve = await f.Service.ReserveAsync(new ReserveIdentifierInput("DocumentCode", "GMG-QMS-SOP-9001", null, "Migration", null, null, null), Corr, CancellationToken.None);
        Assert.False(reReserve.IsSuccessful);
        Assert.Equal(IdentifierAllocationReasonCodes.DuplicateIdentifier, reReserve.ReasonCode);

        // And a soft-deleted ledger row still blocks reuse.
        f.Ledger.Items.Single().IsDeleted = true;
        var afterDelete = await f.Service.ReserveAsync(new ReserveIdentifierInput("DocumentCode", "GMG-QMS-SOP-9001", null, "Migration", null, null, null), Corr, CancellationToken.None);
        Assert.False(afterDelete.IsSuccessful);
        Assert.Equal(IdentifierAllocationReasonCodes.DuplicateIdentifier, afterDelete.ReasonCode);
    }

    [Fact]
    public async Task Cancelling_current_allocations_clears_register_identity_and_preserves_remaining_source()
    {
        var f = Fixture();
        var entry = SeedEntry(f);
        await f.Service.AllocateIdentifiersAsync(entry.Id, null, Corr, CancellationToken.None);
        var uid = f.Ledger.Items.Single(x => x.IdentifierType == DocumentIdentifierType.PermanentUid);
        var code = f.Ledger.Items.Single(x => x.IdentifierType == DocumentIdentifierType.DocumentCode);

        await f.Service.CancelAsync(uid.Id, new CancelIdentifierInput("UID assigned in error"), Corr, CancellationToken.None);

        Assert.Null(entry.PermanentUid);
        Assert.Equal(code.IdentifierValue, entry.DocumentCode);
        Assert.True(entry.IsSystemAllocated);

        await f.Service.CancelAsync(code.Id, new CancelIdentifierInput("Code assigned in error"), Corr, CancellationToken.None);

        Assert.Null(entry.DocumentCode);
        Assert.False(entry.IsSystemAllocated);
    }

    [Fact]
    public async Task Sequence_increments_without_reuse()
    {
        var f = Fixture();
        var a = await f.Service.AllocateUidAsync(SeedEntry(f).Id, null, Corr, CancellationToken.None);
        var b = await f.Service.AllocateUidAsync(SeedEntry(f).Id, null, Corr, CancellationToken.None);
        var c = await f.Service.AllocateUidAsync(SeedEntry(f).Id, null, Corr, CancellationToken.None);

        Assert.Equal("UID-0000001", a.Data!.PermanentUid);
        Assert.Equal("UID-0000002", b.Data!.PermanentUid);
        Assert.Equal("UID-0000003", c.Data!.PermanentUid);
    }

    [Fact]
    public async Task Uid_sequence_can_have_gaps_and_remains_valid()
    {
        var f = Fixture();
        // Manually reserve the value that the counter would produce second.
        await f.Service.ReserveAsync(new ReserveIdentifierInput("PermanentUid", "UID-0000002", null, "Migration", null, null, null), Corr, CancellationToken.None);

        var first = await f.Service.AllocateUidAsync(SeedEntry(f).Id, null, Corr, CancellationToken.None);
        var second = await f.Service.AllocateUidAsync(SeedEntry(f).Id, null, Corr, CancellationToken.None);

        Assert.Equal("UID-0000001", first.Data!.PermanentUid);
        // Counter's #2 collides with the reserved value → engine skips it (gap permitted) and hands out #3.
        Assert.Equal("UID-0000003", second.Data!.PermanentUid);
    }

    [Fact]
    public async Task Allocation_engine_is_the_only_path_that_sets_uid_idempotently()
    {
        var f = Fixture();
        var entry = SeedEntry(f);
        var first = await f.Service.AllocateUidAsync(entry.Id, null, Corr, CancellationToken.None);

        var again = await f.Service.AllocateUidAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.Equal(first.Data!.PermanentUid, again.Data!.PermanentUid);
        Assert.True(f.Register.Items.Single().IsSystemAllocated);
        Assert.Single(f.Ledger.Items);
    }

    [Fact]
    public async Task Cross_tenant_allocation_is_blocked()
    {
        var f = Fixture();
        var foreign = SeedEntry(f, tenantId: OtherTenantId);

        var response = await f.Service.AllocateUidAsync(foreign.Id, null, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Ledger_list_is_tenant_scoped()
    {
        var f = Fixture();
        await f.Service.AllocateUidAsync(SeedEntry(f).Id, null, Corr, CancellationToken.None);
        f.Ledger.Items.Add(new DocumentIdentifierAllocation { Id = Guid.NewGuid(), TenantId = OtherTenantId, IdentifierType = DocumentIdentifierType.PermanentUid, IdentifierValue = "UID-0000001" });

        var response = await f.Service.ListAsync(new IdentifierAllocationListFilter(), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task LegacyCode_duplicate_does_not_block_new_code_allocation()
    {
        var f = Fixture();
        var e1 = SeedEntry(f);
        e1.LegacyCode = "OLD-1";
        var e2 = SeedEntry(f);
        e2.LegacyCode = "OLD-1"; // same legacy code — allowed (SOP §12.3 mapping retained)

        var r1 = await f.Service.AllocateCodeAsync(e1.Id, null, Corr, CancellationToken.None);
        var r2 = await f.Service.AllocateCodeAsync(e2.Id, null, Corr, CancellationToken.None);

        Assert.True(r1.IsSuccessful);
        Assert.True(r2.IsSuccessful);
        Assert.Equal("GMG-QMS-SOP-0001", r1.Data!.DocumentCode);
        Assert.Equal("GMG-QMS-SOP-0002", r2.Data!.DocumentCode);
    }

    [Fact]
    public async Task Record_entry_cannot_receive_controlled_document_code()
    {
        var f = Fixture();
        var entry = SeedEntry(f);
        entry.IsControlledDocument = false;
        entry.IsRecord = true;

        var response = await f.Service.AllocateCodeAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(IdentifierAllocationReasonCodes.RecordNotEligible, response.ReasonCode);
    }

    [Fact]
    public async Task Variant_entry_blocks_code_allocation()
    {
        var f = Fixture();
        var entry = SeedEntry(f);
        entry.IsVariant = true;
        entry.ParentDocumentUid = "UID-0000001";

        var response = await f.Service.AllocateCodeAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(IdentifierAllocationReasonCodes.VariantInheritsParent, response.ReasonCode);
    }

    [Fact]
    public async Task Archived_entry_is_not_allocatable()
    {
        var f = Fixture();
        var entry = SeedEntry(f);
        entry.RegisterStatus = DocumentRegisterStatus.Archived;

        var response = await f.Service.AllocateUidAsync(entry.Id, null, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(IdentifierAllocationReasonCodes.EntryNotAllocatable, response.ReasonCode);
    }

    [Fact]
    public async Task Reserve_links_register_entry_and_keeps_manual_provenance()
    {
        var f = Fixture();
        var entry = SeedEntry(f);

        var response = await f.Service.ReserveAsync(
            new ReserveIdentifierInput("PermanentUid", "UID-5000001", entry.Id, "ManualImport", null, "LegacySys", "LEG-1"), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.False(response.Data!.IsSystemAllocated);
        Assert.Equal("UID-5000001", f.Register.Items.Single().PermanentUid);
        Assert.False(f.Register.Items.Single().IsSystemAllocated);
        Assert.Equal("LegacySys", response.Data.SourceSystem);
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var register = new FakeRegisterRepo(tenant);
        var ledger = new FakeLedgerRepo(tenant);
        var counter = new FakeCounterRepo(tenant);
        var options = Options.Create(new DocumentCodingOptions());
        var service = new DocumentIdentifierAllocationService(register, ledger, counter, tenant, new FakeUser(), options);
        return new Harness(service, register, ledger, counter);
    }

    private static DocumentMasterRegisterEntry SeedEntry(
        Harness f, Guid? tenantId = null,
        ControlledDocumentClass documentClass = ControlledDocumentClass.Sop,
        DocumentType documentType = DocumentType.Sop)
    {
        var entry = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? TenantId,
            DocumentTitle = "Document Control",
            DocumentClass = documentClass,
            DocumentType = documentType,
            Criticality = DocumentCriticality.Critical,
            OwnerCompanyId = CompanyId,
            IsControlledDocument = true,
            RegisterStatus = DocumentRegisterStatus.Draft
        };
        f.Register.Items.Add(entry);
        return entry;
    }

    private sealed record Harness(
        DocumentIdentifierAllocationService Service,
        FakeRegisterRepo Register,
        FakeLedgerRepo Ledger,
        FakeCounterRepo Counter);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444444");
        public string? Email => "fu07@example.test";
        public string? DisplayName => "FU07 Tester";
        public string ActorName => "fu07@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRegisterRepo(ITenantContext tenant) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];
        private IEnumerable<DocumentMasterRegisterEntry> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) { Items.Add(entry); return Task.FromResult(entry); }
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string permanentUid, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.PermanentUid == permanentUid));
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string documentCode, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.DocumentCode == documentCode));
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.ControlledDocumentId == controlledDocumentId));
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == entry.Id); if (i >= 0) Items[i] = entry; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeLedgerRepo(ITenantContext tenant) : IDocumentIdentifierAllocationRepository
    {
        public List<DocumentIdentifierAllocation> Items { get; } = [];
        private IEnumerable<DocumentIdentifierAllocation> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentIdentifierAllocation> CreateAsync(DocumentIdentifierAllocation allocation, CancellationToken ct = default) { Items.Add(allocation); return Task.FromResult(allocation); }
        public Task<DocumentIdentifierAllocation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));

        // Includes soft-deleted AND every status — mirrors the never-reuse Mongo index.
        public Task<bool> ExistsValueIncludingDeletedAsync(DocumentIdentifierType type, string identifierValue, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(x => x.TenantId == tenant.TenantId && x.IdentifierType == type && x.IdentifierValue == identifierValue));

        public Task<IReadOnlyList<DocumentIdentifierAllocation>> ListAsync(IdentifierAllocationListFilter filter, CancellationToken ct = default)
        {
            var q = Scoped;
            if (filter.IdentifierType is { } t) q = q.Where(x => x.IdentifierType == t);
            if (filter.AllocationStatus is { } s) q = q.Where(x => x.AllocationStatus == s);
            if (filter.RegisterEntryId is { } r) q = q.Where(x => x.RegisterEntryId == r);
            return Task.FromResult<IReadOnlyList<DocumentIdentifierAllocation>>(q.ToList());
        }

        public Task<bool> UpdateAsync(DocumentIdentifierAllocation allocation, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == allocation.Id); if (i >= 0) Items[i] = allocation; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeCounterRepo(ITenantContext tenant) : IDocumentIdentifierSequenceCounterRepository
    {
        private readonly Dictionary<string, long> _counters = [];

        public Task<long> NextAsync(DocumentIdentifierType type, string? prefix, string? domainCode, string? typeCode, string createdBy, CancellationToken ct = default)
        {
            var key = $"{tenant.TenantId}|{type}|{prefix}|{domainCode}|{typeCode}";
            var next = _counters.TryGetValue(key, out var v) ? v + 1 : 1;
            _counters[key] = next;
            return Task.FromResult(next);
        }
    }
}
