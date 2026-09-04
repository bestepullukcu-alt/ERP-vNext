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
/// DCP-005 Phase 1 — controlled-document effectiveness resolver tests. Exercises the single resolver
/// (ResolveDocumentEffectivenessQuery / Handler) and the in-process gate (IControlledDocumentEffectivenessPort) over a
/// tenant-aware in-memory register, proving the three disjoint states, the by=Code/by=Uid split, fail-closed
/// propagation of an infrastructure failure, and that the port returns the resolver's result verbatim.
/// </summary>
public sealed class DocumentEffectivenessResolverTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string Corr = "dcp005-corr-1";

    // Effective ⇔ LifecycleStatus ∈ {Effective, UnderRevision} (ControlledDocumentLifecyclePolicy.IsOperationallyEffective).
    [Theory]
    [InlineData(ControlledDocumentLifecycleStatus.Effective)]
    [InlineData(ControlledDocumentLifecycleStatus.UnderRevision)]
    public async Task Operationally_effective_statuses_resolve_to_effective(ControlledDocumentLifecycleStatus status)
    {
        var handler = HandlerWith(Entry(uid: "UID-0000104", code: "GMG-QMS-SOP-0001", status: status));

        var response = await handler.Handle(Query("UID-0000104", DocumentIdentifierKind.Uid), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items);
        Assert.Equal(DocumentEffectivenessState.Effective, item.State);
        Assert.Null(item.BlockedReason);
        Assert.Equal(status.ToString(), item.LifecycleStatus);
        Assert.Equal("UID-0000104", item.PermanentUid);
        Assert.Equal("GMG-QMS-SOP-0001", item.DocumentCode);
    }

    // The remaining seven members are all Blocked, with BlockedReason echoing the register's own lifecycle word.
    [Theory]
    [InlineData(ControlledDocumentLifecycleStatus.Draft)]
    [InlineData(ControlledDocumentLifecycleStatus.InReview)]
    [InlineData(ControlledDocumentLifecycleStatus.ApprovedPendingEffective)]
    [InlineData(ControlledDocumentLifecycleStatus.Suspended)]
    [InlineData(ControlledDocumentLifecycleStatus.Superseded)]
    [InlineData(ControlledDocumentLifecycleStatus.Retired)]
    [InlineData(ControlledDocumentLifecycleStatus.ObsoleteCopy)]
    public async Task Non_effective_statuses_resolve_to_blocked_with_reason(ControlledDocumentLifecycleStatus status)
    {
        var handler = HandlerWith(Entry(uid: "UID-0000104", code: "GMG-QMS-SOP-0001", status: status));

        var response = await handler.Handle(Query("UID-0000104", DocumentIdentifierKind.Uid), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items);
        Assert.Equal(DocumentEffectivenessState.Blocked, item.State);
        Assert.Equal(status.ToString(), item.BlockedReason);
        Assert.Equal(status.ToString(), item.LifecycleStatus);
    }

    [Fact]
    public async Task Identifier_with_no_register_row_is_unresolved()
    {
        var handler = HandlerWith(Entry(uid: "UID-0000104", code: "GMG-QMS-SOP-0001", status: ControlledDocumentLifecycleStatus.Effective));

        var response = await handler.Handle(Query("UID-9999999", DocumentIdentifierKind.Uid), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items);
        Assert.Equal(DocumentEffectivenessState.Unresolved, item.State);
        Assert.Null(item.DocumentCode);
        Assert.Null(item.PermanentUid);
        Assert.Null(item.LifecycleStatus);
        Assert.Null(item.BlockedReason);
    }

    [Fact]
    public async Task By_uid_resolves_from_permanent_uid_field_only()
    {
        // A row whose CODE equals the requested UID string must NOT match when resolving By=Uid.
        var handler = HandlerWith(
            Entry(uid: "UID-0000104", code: "GMG-QMS-SOP-0001", status: ControlledDocumentLifecycleStatus.Effective),
            Entry(uid: "UID-0000200", code: "UID-0000104", status: ControlledDocumentLifecycleStatus.Retired));

        var response = await handler.Handle(Query("UID-0000104", DocumentIdentifierKind.Uid), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items);
        Assert.Equal(DocumentEffectivenessState.Effective, item.State);
        Assert.Equal("UID-0000104", item.PermanentUid);
    }

    [Fact]
    public async Task By_code_resolves_from_document_code_field_only()
    {
        // A row whose UID equals the requested code string must NOT match when resolving By=Code.
        var handler = HandlerWith(
            Entry(uid: "GMG-QMS-SOP-0001", code: "GMG-QMS-SOP-9999", status: ControlledDocumentLifecycleStatus.Retired),
            Entry(uid: "UID-0000104", code: "GMG-QMS-SOP-0001", status: ControlledDocumentLifecycleStatus.Effective));

        var response = await handler.Handle(Query("GMG-QMS-SOP-0001", DocumentIdentifierKind.Code), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items);
        Assert.Equal(DocumentEffectivenessState.Effective, item.State);
        Assert.Equal("GMG-QMS-SOP-0001", item.DocumentCode);
    }

    [Fact]
    public async Task Register_read_failure_propagates_and_is_not_converted_to_unresolved()
    {
        // Fail-closed: an infrastructure failure is "could not check", never the Unresolved data fact.
        var handler = new ResolveDocumentEffectivenessHandler(new ThrowingRegisterRepository(), Tenant());

        await Assert.ThrowsAsync<TimeoutException>(() =>
            handler.Handle(Query("UID-0000104", DocumentIdentifierKind.Uid), CancellationToken.None));
    }

    [Fact]
    public async Task Mixed_batch_places_each_identifier_in_its_own_branch()
    {
        var handler = HandlerWith(
            Entry(uid: "UID-EFF", code: "C-EFF", status: ControlledDocumentLifecycleStatus.Effective),
            Entry(uid: "UID-BLK", code: "C-BLK", status: ControlledDocumentLifecycleStatus.Superseded));

        var response = await handler.Handle(
            Query(new[] { "UID-EFF", "UID-BLK", "UID-MISSING" }, DocumentIdentifierKind.Uid), CancellationToken.None);

        var items = response.Data!.Items;
        Assert.Equal(3, items.Count);
        Assert.Equal(DocumentEffectivenessState.Effective, items.Single(i => i.Identifier == "UID-EFF").State);
        var blocked = items.Single(i => i.Identifier == "UID-BLK");
        Assert.Equal(DocumentEffectivenessState.Blocked, blocked.State);
        Assert.Equal(nameof(ControlledDocumentLifecycleStatus.Superseded), blocked.BlockedReason);
        Assert.Equal(DocumentEffectivenessState.Unresolved, items.Single(i => i.Identifier == "UID-MISSING").State);
    }

    [Fact]
    public async Task Port_returns_the_same_result_as_the_query_for_the_same_input()
    {
        // Single-resolver proof: the port is a thin adapter — it must return exactly what the resolver returns.
        var handler = HandlerWith(
            Entry(uid: "UID-EFF", code: "C-EFF", status: ControlledDocumentLifecycleStatus.Effective),
            Entry(uid: "UID-BLK", code: "C-BLK", status: ControlledDocumentLifecycleStatus.Retired));

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<ResolveDocumentEffectivenessQuery>(), It.IsAny<CancellationToken>()))
            .Returns((ResolveDocumentEffectivenessQuery q, CancellationToken token) => handler.Handle(q, token));
        var port = new ControlledDocumentEffectivenessPort(mediator.Object);

        var ids = new[] { "UID-EFF", "UID-BLK", "UID-MISSING" };
        var viaQuery = await handler.Handle(Query(ids, DocumentIdentifierKind.Uid), CancellationToken.None);
        var viaPort = await port.ResolveAsync(new DocumentEffectivenessQuery(ids, DocumentIdentifierKind.Uid), CancellationToken.None);

        Assert.Equal(viaQuery.Data!.Items, viaPort.Items); // element-wise record equality
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    private static TenantContext Tenant()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        return tenant;
    }

    private static ResolveDocumentEffectivenessHandler HandlerWith(params DocumentMasterRegisterEntry[] entries)
    {
        var register = new FakeRegisterRepository();
        register.Items.AddRange(entries);
        return new ResolveDocumentEffectivenessHandler(register, Tenant());
    }

    private static DocumentMasterRegisterEntry Entry(string? uid, string? code, ControlledDocumentLifecycleStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        PermanentUid = uid,
        DocumentCode = code,
        DocumentTitle = "Document Control",
        LifecycleStatus = status
    };

    private static ResolveDocumentEffectivenessQuery Query(string identifier, DocumentIdentifierKind by) =>
        Query(new[] { identifier }, by);

    private static ResolveDocumentEffectivenessQuery Query(IReadOnlyList<string> identifiers, DocumentIdentifierKind by) =>
        new(identifiers, by, Corr);

    private sealed class FakeRegisterRepository : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];

        private IEnumerable<DocumentMasterRegisterEntry> Scoped => Items.Where(x => x.TenantId == TenantId && !x.IsDeleted);

        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());

        // Unused by the effectiveness resolver (Phase 1 reads the whole tenant register in memory).
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
