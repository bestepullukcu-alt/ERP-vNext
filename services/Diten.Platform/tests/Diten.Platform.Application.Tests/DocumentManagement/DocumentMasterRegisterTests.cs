using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU06 — Document Master Register Foundation service tests. Uses tenant-aware in-memory fakes so tenant
/// isolation, duplicate-guard, protected-field and SOP §2 boundary behaviours are exercised without Mongo.
/// </summary>
public sealed class DocumentMasterRegisterTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private const string Corr = "fu06-corr-1";

    [Fact]
    public async Task Create_entry_persists_required_metadata_and_defaults()
    {
        var f = Fixture();

        var response = await f.Service.CreateAsync(CreateInput() with { DocumentTitle = "  Document Control  " }, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        var entry = Assert.Single(f.Register.Items);
        Assert.Equal("Document Control", entry.DocumentTitle);
        Assert.Equal(ControlledDocumentClass.Sop, entry.DocumentClass);
        Assert.Equal(DocumentCriticality.Critical, entry.Criticality);
        Assert.Equal(ControlledDocumentLifecycleStatus.Draft, entry.LifecycleStatus);
        Assert.Equal(DocumentRegisterStatus.Draft, entry.RegisterStatus);
        Assert.Equal(TenantId, entry.TenantId);
        Assert.Equal(CreateInput().AuthorUserId, entry.AuthorUserId);
        Assert.NotEqual(entry.ProcessOwnerUserId, entry.AuthorUserId);
    }

    [Fact]
    public async Task Create_entry_without_controlled_document_is_allowed()
    {
        var f = Fixture();

        var response = await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Null(response.Data!.ControlledDocumentId);
    }

    [Fact]
    public async Task Create_entry_allows_null_uid_and_code_before_allocation()
    {
        var f = Fixture();

        var response = await f.Service.CreateAsync(CreateInput() with { PermanentUid = null, DocumentCode = null }, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Null(response.Data!.PermanentUid);
        Assert.Null(response.Data.DocumentCode);
        Assert.False(response.Data.IsSystemAllocated); // manual provenance
    }

    [Fact]
    public async Task Duplicate_permanent_uid_is_rejected()
    {
        var f = Fixture();
        await f.Service.CreateAsync(CreateInput() with { PermanentUid = "UID-0000001" }, Corr, CancellationToken.None);

        var response = await f.Service.CreateAsync(CreateInput() with { PermanentUid = "UID-0000001", DocumentCode = "GMG-QMS-SOP-0002" }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MasterRegisterReasonCodes.DuplicatePermanentUid, response.ReasonCode);
        Assert.Single(f.Register.Items);
    }

    [Fact]
    public async Task Duplicate_document_code_is_rejected()
    {
        var f = Fixture();
        await f.Service.CreateAsync(CreateInput() with { DocumentCode = "GMG-QMS-SOP-0001" }, Corr, CancellationToken.None);

        var response = await f.Service.CreateAsync(CreateInput() with { PermanentUid = "UID-0000002", DocumentCode = "GMG-QMS-SOP-0001" }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MasterRegisterReasonCodes.DuplicateDocumentCode, response.ReasonCode);
    }

    [Fact]
    public async Task Record_and_controlled_document_conflict_is_rejected()
    {
        var f = Fixture();

        var response = await f.Service.CreateAsync(CreateInput() with { IsControlledDocument = true, IsRecord = true }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(MasterRegisterReasonCodes.RecordControlledConflict, response.ReasonCode);
    }

    [Fact]
    public async Task Variant_without_parent_reference_is_rejected()
    {
        var f = Fixture();

        var response = await f.Service.CreateAsync(
            CreateInput() with { IsControlledDocument = false, IsVariant = true, ParentDocumentUid = null, ParentDocumentCode = null },
            Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(MasterRegisterReasonCodes.VariantParentMissing, response.ReasonCode);
    }

    [Fact]
    public async Task Variant_with_parent_reference_is_accepted()
    {
        var f = Fixture();

        var response = await f.Service.CreateAsync(
            CreateInput() with { IsControlledDocument = false, IsVariant = true, ParentDocumentUid = "UID-0000001" },
            Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.True(response.Data!.IsVariant);
        Assert.Equal("UID-0000001", response.Data.ParentDocumentUid);
    }

    [Fact]
    public async Task Invalid_class_is_rejected()
    {
        var f = Fixture();

        var response = await f.Service.CreateAsync(CreateInput() with { DocumentClass = "NotARealClass" }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(MasterRegisterReasonCodes.ValidationFailed, response.ReasonCode);
    }

    [Fact]
    public async Task Update_metadata_does_not_change_protected_allocation_or_lifecycle_fields()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(CreateInput() with { PermanentUid = "UID-0000001", DocumentCode = "GMG-QMS-SOP-0001" }, Corr, CancellationToken.None);
        // Simulate a later engine having set protected fields.
        var stored = f.Register.Items.Single();
        stored.LifecycleStatus = ControlledDocumentLifecycleStatus.Effective;
        stored.EffectiveDate = DateTimeOffset.UtcNow;
        stored.CurrentVersionLabel = "1.0";

        var response = await f.Service.UpdateMetadataAsync(created.Data!.Id, UpdateInput() with { DocumentTitle = "Renamed" }, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var after = f.Register.Items.Single();
        Assert.Equal("Renamed", after.DocumentTitle);
        // Protected fields are untouched by the metadata path.
        Assert.Equal("UID-0000001", after.PermanentUid);
        Assert.Equal("GMG-QMS-SOP-0001", after.DocumentCode);
        Assert.Equal(ControlledDocumentLifecycleStatus.Effective, after.LifecycleStatus);
        Assert.Equal("1.0", after.CurrentVersionLabel);
        Assert.NotNull(after.EffectiveDate);
    }

    [Fact]
    public async Task Update_missing_entry_returns_not_found_non_leakage()
    {
        var f = Fixture();

        var response = await f.Service.UpdateMetadataAsync(Guid.NewGuid(), UpdateInput(), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(MasterRegisterReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Update_can_assign_missing_legacy_author_once_but_cannot_replace_it()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        var stored = f.Register.Items.Single();
        stored.AuthorUserId = null;
        var legacyAuthor = Guid.Parse("dddddddd-1111-2222-3333-444444444444");

        var assigned = await f.Service.UpdateMetadataAsync(
            created.Data!.Id, UpdateInput() with { AuthorUserId = legacyAuthor }, Corr, CancellationToken.None);
        var replacement = await f.Service.UpdateMetadataAsync(
            created.Data.Id, UpdateInput() with { AuthorUserId = Guid.NewGuid() }, Corr, CancellationToken.None);

        Assert.True(assigned.IsSuccessful);
        Assert.Equal(legacyAuthor, stored.AuthorUserId);
        Assert.False(replacement.IsSuccessful);
        Assert.Equal(409, replacement.StatusCode);
        Assert.Equal(MasterRegisterReasonCodes.ProtectedFieldChange, replacement.ReasonCode);
        Assert.Equal(legacyAuthor, stored.AuthorUserId);
    }

    [Fact]
    public async Task Link_controlled_document_sets_relation()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        var doc = SeedControlledDocument(f, TenantId);

        var response = await f.Service.LinkControlledDocumentAsync(created.Data!.Id, doc.Id, "Legacy reconciliation", Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(doc.Id, response.Data!.ControlledDocumentId);
    }

    [Fact]
    public async Task Link_missing_controlled_document_returns_not_found()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);

        var response = await f.Service.LinkControlledDocumentAsync(created.Data!.Id, Guid.NewGuid(), "Legacy reconciliation", Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(MasterRegisterReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Link_cross_tenant_controlled_document_is_blocked()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        var otherTenantDoc = SeedControlledDocument(f, OtherTenantId); // belongs to a different tenant

        var response = await f.Service.LinkControlledDocumentAsync(created.Data!.Id, otherTenantDoc.Id, "Legacy reconciliation", Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode); // non-leaking: cross-tenant looks like "not found"
    }

    [Fact]
    public async Task Relinking_same_document_is_idempotent_but_different_document_conflicts()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        var doc1 = SeedControlledDocument(f, TenantId);
        var doc2 = SeedControlledDocument(f, TenantId);
        await f.Service.LinkControlledDocumentAsync(created.Data!.Id, doc1.Id, "Legacy reconciliation", Corr, CancellationToken.None);

        var same = await f.Service.LinkControlledDocumentAsync(created.Data!.Id, doc1.Id, "Legacy reconciliation", Corr, CancellationToken.None);
        Assert.True(same.IsSuccessful);

        var conflict = await f.Service.LinkControlledDocumentAsync(created.Data!.Id, doc2.Id, "Legacy reconciliation", Corr, CancellationToken.None);
        Assert.False(conflict.IsSuccessful);
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal(MasterRegisterReasonCodes.AlreadyLinked, conflict.ReasonCode);
    }

    [Fact]
    public async Task List_is_tenant_scoped()
    {
        var f = Fixture();
        await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        // A row that belongs to another tenant must be invisible.
        f.Register.Items.Add(new DocumentMasterRegisterEntry { Id = Guid.NewGuid(), TenantId = OtherTenantId, DocumentTitle = "Foreign" });

        var response = await f.Service.ListAsync(new MasterRegisterListFilter(), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task Detail_cross_tenant_returns_not_found()
    {
        var f = Fixture();
        var foreign = new DocumentMasterRegisterEntry { Id = Guid.NewGuid(), TenantId = OtherTenantId, DocumentTitle = "Foreign" };
        f.Register.Items.Add(foreign);

        var response = await f.Service.GetDetailAsync(foreign.Id, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Summary_counts_by_status_class_and_criticality()
    {
        var f = Fixture();
        await f.Service.CreateAsync(CreateInput() with { DocumentCode = "C1", PermanentUid = "U1", Criticality = "Critical", DocumentClass = "Sop" }, Corr, CancellationToken.None);
        await f.Service.CreateAsync(CreateInput() with { DocumentCode = "C2", PermanentUid = null, Criticality = "Minor", DocumentClass = "WorkInstruction" }, Corr, CancellationToken.None);

        var response = await f.Service.GetSummaryAsync(Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var s = response.Data!;
        Assert.Equal(2, s.Total);
        Assert.Equal(1, s.WithPermanentUid);
        Assert.Equal(1, s.WithoutPermanentUid);
        Assert.Equal(1, s.ByCriticality["Critical"]);
        Assert.Equal(1, s.ByCriticality["Minor"]);
        Assert.Equal(2, s.ByLifecycleStatus["Draft"]);
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var register = new FakeMasterRegisterRepository(tenant);
        var documents = new FakeTenantScopedControlledDocumentRepository(tenant);
        var service = new DocumentMasterRegisterService(register, documents, tenant, new FakeUser());
        return new Harness(service, register, documents);
    }

    private static ControlledDocument SeedControlledDocument(Harness f, Guid tenantId)
    {
        var doc = new ControlledDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentKey = "DOC-" + Guid.NewGuid().ToString("N")[..8],
            CompanyId = CompanyId,
            OwnerCompanyId = CompanyId,
            DocumentScope = DocumentScope.Company,
            ScopeOwnerId = CompanyId,
            CollectionInstanceId = Guid.NewGuid(),
            FolderId = Guid.NewGuid(),
            CollectionPath = "/qms",
            Title = "Linked Document"
        };
        f.Documents.Items.Add(doc);
        if (tenantId == TenantId && f.Register.Items.LastOrDefault() is { CollectionInstanceId: var collectionId } entry
            && collectionId == Guid.Empty)
        {
            entry.DocumentScope = doc.DocumentScope;
            entry.ScopeOwnerId = doc.ScopeOwnerId;
            entry.CollectionInstanceId = doc.CollectionInstanceId;
            entry.FolderId = doc.FolderId;
        }
        return doc;
    }

    private static CreateMasterRegisterEntryInput CreateInput() => new(
        DocumentTitle: "Document Control",
        DocumentClass: "Sop",
        Criticality: "Critical",
        DocumentType: "Sop",
        PermanentUid: null,
        DocumentCode: null,
        LegacyCode: null,
        ProcessOwnerRole: "Global Quality Director",
        ProcessOwnerUserId: null,
        AuthorUserId: Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444"),
        OwnerFunction: null,
        OwnerCompanyId: CompanyId,
        GoverningLanguage: "en",
        ReviewCycleMonths: 24,
        RetentionClass: null,
        IsControlledDocument: true,
        IsRecord: false,
        IsExternalDocument: false,
        IsTemplate: false,
        IsVariant: false,
        ParentDocumentUid: null,
        ParentDocumentCode: null,
        SourceSystem: null,
        SourceLegacyId: null);

    private static UpdateMasterRegisterMetadataInput UpdateInput() => new(
        DocumentTitle: "Document Control",
        DocumentClass: "Sop",
        Criticality: "Critical",
        DocumentType: null,
        LegacyCode: null,
        ProcessOwnerRole: null,
        ProcessOwnerUserId: null,
        AuthorUserId: null,
        OwnerFunction: null,
        OwnerCompanyId: CompanyId,
        GoverningLanguage: "en",
        ReviewCycleMonths: 24,
        RetentionClass: null,
        ApprovedRepositoryId: null,
        ApprovedRepositoryName: null,
        ApprovedRepositoryPath: null,
        ParentDocumentUid: null,
        ParentDocumentCode: null);

    private sealed record Harness(
        DocumentMasterRegisterService Service,
        FakeMasterRegisterRepository Register,
        FakeTenantScopedControlledDocumentRepository Documents);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444444");
        public string? Email => "fu06@example.test";
        public string? DisplayName => "FU06 Tester";
        public string ActorName => "fu06@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeMasterRegisterRepository(ITenantContext tenant) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];

        private IEnumerable<DocumentMasterRegisterEntry> Scoped =>
            Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default)
        {
            Items.Add(entry);
            return Task.FromResult(entry);
        }

        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));

        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string permanentUid, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.PermanentUid == permanentUid));

        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string documentCode, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.DocumentCode == documentCode));

        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.ControlledDocumentId == controlledDocumentId));

        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default)
        {
            var q = Scoped;
            if (filter.RegisterStatus is { } rs) q = q.Where(x => x.RegisterStatus == rs);
            if (filter.LifecycleStatus is { } ls) q = q.Where(x => x.LifecycleStatus == ls);
            if (filter.Criticality is { } c) q = q.Where(x => x.Criticality == c);
            if (filter.DocumentClass is { } dc) q = q.Where(x => x.DocumentClass == dc);
            if (filter.OwnerCompanyId is { } oc) q = q.Where(x => x.OwnerCompanyId == oc);
            return Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(q.ToList());
        }

        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());

        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == entry.Id);
            if (i >= 0) Items[i] = entry;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeTenantScopedControlledDocumentRepository(ITenantContext tenant) : IControlledDocumentRepository
    {
        public List<ControlledDocument> Items { get; } = [];

        private IEnumerable<ControlledDocument> Scoped =>
            Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<ControlledDocument> CreateAsync(ControlledDocument document, CancellationToken ct = default) { Items.Add(document); return Task.FromResult(document); }
        public Task<ControlledDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<ControlledDocument?> GetByDocumentKeyAsync(string documentKey, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.DocumentKey == documentKey));
        public Task<IReadOnlyList<ControlledDocument>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ControlledDocument>>(Scoped.ToList());
        public Task<IReadOnlyList<ControlledDocument>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ControlledDocument>>(Scoped.Where(x => x.OwnerCompanyId == companyId).ToList());
        public Task<IReadOnlyList<ControlledDocument>> GetByCollectionInstanceAsync(Guid collectionInstanceId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ControlledDocument>>(Scoped.Where(x => x.CollectionInstanceId == collectionInstanceId).ToList());
        public Task<bool> UpdateAsync(ControlledDocument document, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == document.Id); if (i >= 0) Items[i] = document; return Task.FromResult(i >= 0); }
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) { var d = Items.FirstOrDefault(x => x.Id == id); if (d is not null) d.IsDeleted = true; return Task.CompletedTask; }
    }
}
