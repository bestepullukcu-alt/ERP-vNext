using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// DCP-005 Phase 2a — controlled-document CITATION contract tests (WP-DM-2a). Proves the rich sibling of the
/// effectiveness resolver: Resolve maps register rows to citation items (uid/code/title/version/lifecycle + citable +
/// reason), by=uid/code resolve from the correct field, unresolved/incomplete rows are omitted, Search returns blocked
/// rows with Citable=false, the read is fail-closed (propagates), Source=MasterRegister, and the port returns exactly
/// what the query returns (single-resolver proof).
/// </summary>
public sealed class DocumentCitationResolverTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string Corr = "dcp005-cite-corr";

    // ── Resolve: mapping + by-field ──────────────────────────────────────────────

    [Fact]
    public async Task Resolve_by_uid_maps_the_full_citation_shape()
    {
        var entry = Entry(uid: "UID-0000104", code: "GMG-QMS-SOP-0001", title: "Document Control",
            version: "V1.0", status: ControlledDocumentLifecycleStatus.Effective);
        var handler = ResolveHandlerWith(entry);

        var response = await handler.Handle(ResolveQuery("UID-0000104", DocumentIdentifierKind.Uid), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items);
        Assert.Equal("UID-0000104", item.Uid);
        Assert.Equal("GMG-QMS-SOP-0001", item.Code);
        Assert.Equal("Document Control", item.Title);
        Assert.Equal("V1.0", item.Version);
        Assert.Equal("Effective", item.Lifecycle);
        Assert.True(item.Citable);
        Assert.Null(item.BlockedReason);
        Assert.Equal(DocumentCitationSource.MasterRegister, item.Source);
        Assert.Equal(entry.Id, item.RegisterEntryId);
    }

    [Fact]
    public async Task Resolve_by_uid_matches_permanent_uid_only_not_code_decoys()
    {
        var handler = ResolveHandlerWith(
            Entry(uid: "UID-1", code: "C-1", status: ControlledDocumentLifecycleStatus.Effective),
            Entry(uid: "UID-2", code: "UID-1", status: ControlledDocumentLifecycleStatus.Retired)); // decoy: code == requested uid

        var response = await handler.Handle(ResolveQuery("UID-1", DocumentIdentifierKind.Uid), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items);
        Assert.Equal("UID-1", item.Uid);
        Assert.True(item.Citable);
    }

    [Fact]
    public async Task Resolve_by_code_matches_document_code_only_not_uid_decoys()
    {
        var handler = ResolveHandlerWith(
            Entry(uid: "C-1", code: "C-9", status: ControlledDocumentLifecycleStatus.Retired), // decoy: uid == requested code
            Entry(uid: "UID-1", code: "C-1", status: ControlledDocumentLifecycleStatus.Effective));

        var response = await handler.Handle(ResolveQuery("C-1", DocumentIdentifierKind.Code), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items);
        Assert.Equal("C-1", item.Code);
        Assert.Equal("UID-1", item.Uid);
    }

    // ── Citable judgment (same as the effectiveness gate) ────────────────────────

    [Theory]
    [InlineData(ControlledDocumentLifecycleStatus.Effective)]
    [InlineData(ControlledDocumentLifecycleStatus.UnderRevision)]
    public async Task Operationally_effective_statuses_are_citable(ControlledDocumentLifecycleStatus status)
    {
        var handler = ResolveHandlerWith(Entry(uid: "UID-1", code: "C-1", status: status));

        var response = await handler.Handle(ResolveQuery("UID-1", DocumentIdentifierKind.Uid), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items);
        Assert.True(item.Citable);
        Assert.Null(item.BlockedReason);
    }

    [Theory]
    [InlineData(ControlledDocumentLifecycleStatus.Draft)]
    [InlineData(ControlledDocumentLifecycleStatus.InReview)]
    [InlineData(ControlledDocumentLifecycleStatus.ApprovedPendingEffective)]
    [InlineData(ControlledDocumentLifecycleStatus.Suspended)]
    [InlineData(ControlledDocumentLifecycleStatus.Superseded)]
    [InlineData(ControlledDocumentLifecycleStatus.Retired)]
    [InlineData(ControlledDocumentLifecycleStatus.ObsoleteCopy)]
    public async Task Non_effective_statuses_are_not_citable_with_reason(ControlledDocumentLifecycleStatus status)
    {
        var handler = ResolveHandlerWith(Entry(uid: "UID-1", code: "C-1", status: status));

        var response = await handler.Handle(ResolveQuery("UID-1", DocumentIdentifierKind.Uid), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items);
        Assert.False(item.Citable);
        Assert.Equal(status.ToString(), item.BlockedReason);
        Assert.Equal(status.ToString(), item.Lifecycle);
    }

    // ── Omission: unresolved + incomplete rows ───────────────────────────────────

    [Fact]
    public async Task Resolve_omits_identifiers_with_no_register_row()
    {
        var handler = ResolveHandlerWith(Entry(uid: "UID-1", code: "C-1", status: ControlledDocumentLifecycleStatus.Effective));

        var response = await handler.Handle(
            ResolveQuery(new[] { "UID-1", "UID-MISSING" }, DocumentIdentifierKind.Uid), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items); // only the resolved one
        Assert.Equal("UID-1", item.Uid);
    }

    [Fact]
    public async Task Resolve_omits_rows_missing_the_counterpart_identity()
    {
        // Found by uid, but no DocumentCode → not a citation (needs both identities).
        var handler = ResolveHandlerWith(Entry(uid: "UID-1", code: null, status: ControlledDocumentLifecycleStatus.Effective));

        var response = await handler.Handle(ResolveQuery("UID-1", DocumentIdentifierKind.Uid), CancellationToken.None);

        Assert.Empty(response.Data!.Items);
    }

    // ── Search: term filter + blocked visible ────────────────────────────────────

    [Fact]
    public async Task Search_returns_blocked_rows_but_marks_them_not_citable()
    {
        var handler = SearchHandlerWith(
            Entry(uid: "UID-EFF", code: "C-EFF", title: "Effective Doc", status: ControlledDocumentLifecycleStatus.Effective),
            Entry(uid: "UID-BLK", code: "C-BLK", title: "Retired Doc", status: ControlledDocumentLifecycleStatus.Retired));

        var response = await handler.Handle(new SearchDocumentCitationQuery("Doc", 50, Corr), CancellationToken.None);

        Assert.Equal(2, response.Data!.Items.Count);
        var eff = response.Data.Items.Single(i => i.Uid == "UID-EFF");
        var blk = response.Data.Items.Single(i => i.Uid == "UID-BLK");
        Assert.True(eff.Citable);
        Assert.False(blk.Citable);                 // shown, not hidden
        Assert.Equal("Retired", blk.BlockedReason);
    }

    [Fact]
    public async Task Search_filters_by_term_across_uid_code_and_title()
    {
        var handler = SearchHandlerWith(
            Entry(uid: "UID-1", code: "GMG-QMS-SOP-0001", title: "Document Control", status: ControlledDocumentLifecycleStatus.Effective),
            Entry(uid: "UID-2", code: "GMG-GMP-TPL-0001", title: "Master Formula", status: ControlledDocumentLifecycleStatus.Effective));

        var byCode = await handler.Handle(new SearchDocumentCitationQuery("SOP-0001", 50, Corr), CancellationToken.None);
        var byTitle = await handler.Handle(new SearchDocumentCitationQuery("formula", 50, Corr), CancellationToken.None);

        Assert.Equal("GMG-QMS-SOP-0001", Assert.Single(byCode.Data!.Items).Code);
        Assert.Equal("UID-2", Assert.Single(byTitle.Data!.Items).Uid);
    }

    [Fact]
    public async Task Search_is_bounded_by_limit()
    {
        var entries = Enumerable.Range(0, 10)
            .Select(i => Entry(uid: $"UID-{i:D3}", code: $"C-{i:D3}", status: ControlledDocumentLifecycleStatus.Effective))
            .ToArray();
        var handler = SearchHandlerWith(entries);

        var response = await handler.Handle(new SearchDocumentCitationQuery(null, 3, Corr), CancellationToken.None);

        Assert.Equal(3, response.Data!.Items.Count);
    }

    // ── Fail-closed ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_propagates_a_register_read_failure()
    {
        var handler = new ResolveDocumentCitationHandler(new ThrowingRegisterRepository(), Tenant());

        await Assert.ThrowsAsync<TimeoutException>(() =>
            handler.Handle(ResolveQuery("UID-1", DocumentIdentifierKind.Uid), CancellationToken.None));
    }

    [Fact]
    public async Task Search_propagates_a_register_read_failure()
    {
        var handler = new SearchDocumentCitationHandler(new ThrowingRegisterRepository(), Tenant());

        await Assert.ThrowsAsync<TimeoutException>(() =>
            handler.Handle(new SearchDocumentCitationQuery("x", 50, Corr), CancellationToken.None));
    }

    // ── Port == query (single-resolver proof) ────────────────────────────────────

    [Fact]
    public async Task Port_returns_the_same_result_as_the_query_for_resolve_and_search()
    {
        var register = new FakeRegisterRepository();
        register.Rows.AddRange(new[]
        {
            Entry(uid: "UID-EFF", code: "C-EFF", title: "Effective Doc", status: ControlledDocumentLifecycleStatus.Effective),
            Entry(uid: "UID-BLK", code: "C-BLK", title: "Blocked Doc", status: ControlledDocumentLifecycleStatus.Superseded)
        });
        var tenant = Tenant();
        var resolveHandler = new ResolveDocumentCitationHandler(register, tenant);
        var searchHandler = new SearchDocumentCitationHandler(register, tenant);

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ResolveDocumentCitationQuery>(), It.IsAny<CancellationToken>()))
            .Returns((ResolveDocumentCitationQuery q, CancellationToken token) => resolveHandler.Handle(q, token));
        mediator.Setup(m => m.Send(It.IsAny<SearchDocumentCitationQuery>(), It.IsAny<CancellationToken>()))
            .Returns((SearchDocumentCitationQuery q, CancellationToken token) => searchHandler.Handle(q, token));
        var port = new ControlledDocumentCitationPort(mediator.Object);

        var ids = new[] { "UID-EFF", "UID-BLK", "UID-MISSING" };
        var viaQueryResolve = await resolveHandler.Handle(ResolveQuery(ids, DocumentIdentifierKind.Uid), CancellationToken.None);
        var viaPortResolve = await port.ResolveAsync(new DocumentCitationQuery(ids, DocumentIdentifierKind.Uid), CancellationToken.None);
        Assert.Equal(viaQueryResolve.Data!.Items, viaPortResolve.Items); // element-wise record equality

        var viaQuerySearch = await searchHandler.Handle(new SearchDocumentCitationQuery("Doc", 50, Corr), CancellationToken.None);
        var viaPortSearch = await port.SearchAsync("Doc", 50, CancellationToken.None);
        Assert.Equal(viaQuerySearch.Data!.Items, viaPortSearch.Items);
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    private static TenantContext Tenant()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        return tenant;
    }

    private static ResolveDocumentCitationHandler ResolveHandlerWith(params DocumentMasterRegisterEntry[] entries)
    {
        var register = new FakeRegisterRepository();
        register.Rows.AddRange(entries);
        return new ResolveDocumentCitationHandler(register, Tenant());
    }

    private static SearchDocumentCitationHandler SearchHandlerWith(params DocumentMasterRegisterEntry[] entries)
    {
        var register = new FakeRegisterRepository();
        register.Rows.AddRange(entries);
        return new SearchDocumentCitationHandler(register, Tenant());
    }

    private static DocumentMasterRegisterEntry Entry(
        string? uid, string? code, ControlledDocumentLifecycleStatus status, string title = "Document Control", string? version = "V1.0") => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        PermanentUid = uid,
        DocumentCode = code,
        DocumentTitle = title,
        CurrentVersionLabel = version,
        LifecycleStatus = status
    };

    private static ResolveDocumentCitationQuery ResolveQuery(string identifier, DocumentIdentifierKind by) =>
        ResolveQuery(new[] { identifier }, by);

    private static ResolveDocumentCitationQuery ResolveQuery(IReadOnlyList<string> identifiers, DocumentIdentifierKind by) =>
        new(identifiers, by, Corr);

    /// <summary>Implements only GetAllForTenantAsync; the batch $in methods use the interface default (full read + filter).</summary>
    private sealed class FakeRegisterRepository : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Rows { get; } = [];

        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Rows.Where(r => r.TenantId == TenantId && !r.IsDeleted).ToList());

        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string permanentUid, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string documentCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingRegisterRepository : IDocumentMasterRegisterRepository
    {
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            throw new TimeoutException("register read timed out");

        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string permanentUid, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string documentCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
