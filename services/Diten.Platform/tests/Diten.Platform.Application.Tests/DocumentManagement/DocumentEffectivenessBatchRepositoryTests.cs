using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// DCP-005 Phase 2 — batch <c>$in</c> seam tests. Two things are proven without a live Mongo:
/// (1) the interface's default batch fallback returns exactly the rows whose key is one of the requested identifiers
/// (the same set the Mongo <c>$in</c> override returns), and (2) the resolver now routes through the batch seam rather
/// than the full-tenant scan — proven by a repository whose <c>GetAllForTenantAsync</c> throws.
/// </summary>
public sealed class DocumentEffectivenessBatchRepositoryTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string Corr = "dcp005-p2-corr";

    // ── interface default batch fallback (the contract the Mongo $in override must match) ────────────────

    [Fact]
    public async Task Default_batch_by_uid_returns_only_permanent_uid_matches_not_code_decoys()
    {
        IDocumentMasterRegisterRepository repo = new FullScanOnlyFake
        {
            Rows =
            {
                Entry(uid: "UID-1", code: "C-1", status: ControlledDocumentLifecycleStatus.Effective),
                Entry(uid: "UID-2", code: "UID-1", status: ControlledDocumentLifecycleStatus.Retired) // decoy: code == requested UID
            }
        };

        var rows = await repo.GetByPermanentUidsAsync(new[] { "UID-1" });

        var row = Assert.Single(rows);
        Assert.Equal("UID-1", row.PermanentUid);
    }

    [Fact]
    public async Task Default_batch_by_code_returns_only_document_code_matches_not_uid_decoys()
    {
        IDocumentMasterRegisterRepository repo = new FullScanOnlyFake
        {
            Rows =
            {
                Entry(uid: "C-1", code: "C-9", status: ControlledDocumentLifecycleStatus.Retired), // decoy: uid == requested code
                Entry(uid: "UID-1", code: "C-1", status: ControlledDocumentLifecycleStatus.Effective)
            }
        };

        var rows = await repo.GetByDocumentCodesAsync(new[] { "C-1" });

        var row = Assert.Single(rows);
        Assert.Equal("C-1", row.DocumentCode);
    }

    [Fact]
    public async Task Default_batch_matches_multiple_and_trims_and_dedups_and_ignores_blanks()
    {
        IDocumentMasterRegisterRepository repo = new FullScanOnlyFake
        {
            Rows =
            {
                Entry(uid: "UID-1", code: "C-1", status: ControlledDocumentLifecycleStatus.Effective),
                Entry(uid: "UID-2", code: "C-2", status: ControlledDocumentLifecycleStatus.Draft),
                Entry(uid: "UID-3", code: "C-3", status: ControlledDocumentLifecycleStatus.Effective)
            }
        };

        // Whitespace around a key still matches (trim); duplicates/blanks do not add rows.
        var rows = await repo.GetByPermanentUidsAsync(new[] { " UID-1 ", "UID-2", "UID-1", "  ", "" });

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.PermanentUid == "UID-1");
        Assert.Contains(rows, r => r.PermanentUid == "UID-2");
    }

    [Fact]
    public async Task Default_batch_empty_or_all_blank_input_returns_empty()
    {
        IDocumentMasterRegisterRepository repo = new FullScanOnlyFake
        {
            Rows = { Entry(uid: "UID-1", code: "C-1", status: ControlledDocumentLifecycleStatus.Effective) }
        };

        Assert.Empty(await repo.GetByPermanentUidsAsync(Array.Empty<string>()));
        Assert.Empty(await repo.GetByDocumentCodesAsync(new[] { "   ", "" }));
    }

    // ── resolver now uses the batch seam, not the full scan ──────────────────────────────────────────────

    [Fact]
    public async Task Handler_resolves_through_the_batch_seam_and_never_full_scans()
    {
        // GetAllForTenantAsync THROWS here: if the handler still scanned the whole tenant it would blow up. Success
        // proves the resolver goes through the batch $in seam. The batch also records what it was asked for.
        var repo = new BatchOnlyFake
        {
            Rows =
            {
                Entry(uid: "UID-1", code: "C-1", status: ControlledDocumentLifecycleStatus.Effective)
            }
        };
        var handler = new ResolveDocumentEffectivenessHandler(repo, Tenant());

        var response = await handler.Handle(
            new ResolveDocumentEffectivenessQuery(new[] { "UID-1", "UID-X" }, DocumentIdentifierKind.Uid, Corr), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(DocumentEffectivenessState.Effective, response.Data!.Items.Single(i => i.Identifier == "UID-1").State);
        Assert.Equal(DocumentEffectivenessState.Unresolved, response.Data.Items.Single(i => i.Identifier == "UID-X").State);
        Assert.Equal(0, repo.FullScanCalls);
        Assert.Equal(new[] { "UID-1", "UID-X" }, repo.LastUidsQueried);
        Assert.Null(repo.LastCodesQueried); // by=Uid must not touch the code batch
    }

    [Fact]
    public async Task Handler_by_code_uses_the_code_batch_seam()
    {
        var repo = new BatchOnlyFake
        {
            Rows =
            {
                Entry(uid: "UID-1", code: "C-1", status: ControlledDocumentLifecycleStatus.Superseded)
            }
        };
        var handler = new ResolveDocumentEffectivenessHandler(repo, Tenant());

        var response = await handler.Handle(
            new ResolveDocumentEffectivenessQuery(new[] { "C-1" }, DocumentIdentifierKind.Code, Corr), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items);
        Assert.Equal(DocumentEffectivenessState.Blocked, item.State);
        Assert.Equal("Superseded", item.BlockedReason);
        Assert.Equal(new[] { "C-1" }, repo.LastCodesQueried);
        Assert.Null(repo.LastUidsQueried); // by=Code must not touch the uid batch
        Assert.Equal(0, repo.FullScanCalls);
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    private static TenantContext Tenant()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        return tenant;
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

    /// <summary>Implements only the non-default members; the batch methods use the interface default (full read + filter).</summary>
    private sealed class FullScanOnlyFake : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Rows { get; } = [];

        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Rows.Where(r => !r.IsDeleted).ToList());

        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string permanentUid, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string documentCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>Overrides the batch methods (records inputs) and THROWS on any full scan — to prove the resolver batches.</summary>
    private sealed class BatchOnlyFake : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Rows { get; } = [];
        public int FullScanCalls { get; private set; }
        public string[]? LastUidsQueried { get; private set; }
        public string[]? LastCodesQueried { get; private set; }

        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetByPermanentUidsAsync(IReadOnlyCollection<string> permanentUids, CancellationToken ct = default)
        {
            LastUidsQueried = permanentUids.ToArray();
            var wanted = new HashSet<string>(permanentUids, StringComparer.Ordinal);
            return Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(
                Rows.Where(r => r.PermanentUid is not null && wanted.Contains(r.PermanentUid)).ToList());
        }

        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetByDocumentCodesAsync(IReadOnlyCollection<string> documentCodes, CancellationToken ct = default)
        {
            LastCodesQueried = documentCodes.ToArray();
            var wanted = new HashSet<string>(documentCodes, StringComparer.Ordinal);
            return Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(
                Rows.Where(r => r.DocumentCode is not null && wanted.Contains(r.DocumentCode)).ToList());
        }

        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default)
        {
            FullScanCalls++;
            throw new InvalidOperationException("Full tenant scan must not be used by the effectiveness resolver in Phase 2.");
        }

        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string permanentUid, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string documentCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
