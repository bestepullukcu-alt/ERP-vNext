using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Commands;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Handlers.CommandHandlers;
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
/// WP-DM-DCP005-REGISTER-IMPORT-UI-01 — the audited two-step register CSV import: preview (never writes), commit
/// (hash idempotency, then delegates the actual upsert to <c>IngestDocumentMasterRegisterCommand</c> unchanged).
/// </summary>
public sealed class DocumentRegisterImportTests
{
    private static readonly Guid TenantId = Guid.Parse("22222222-3333-4444-5555-666666666666");
    private const string Corr = "wp-dm-import-corr";

    private const string Header =
        "document_uid,document_code,title,gqms_domain,gqms_type,erp_document_type,version,status,criticality,owner,effective_date,review_cycle,folder_id,folder_path,is_mandatory_group_sop,linkable_in_erp,link_blocked_reason";

    private static string Csv(params string[] rows) => string.Join("\n", new[] { Header }.Concat(rows));

    private static string Row(string uid, string code, string title, string status, bool linkable, string? blockedReason = null, string version = "1.0") =>
        $"{uid},{code},{title},GMP,SOP,Sop,{version},{status},Major,QA,,,F1,/f1,,{(linkable ? "yes" : "no")},{blockedReason}";

    private sealed class Harness
    {
        public required FakeRegisterRepository Register { get; init; }
        public required FakeBatchRepository Batches { get; init; }
        public required DryRunDocumentRegisterImportHandler DryRun { get; init; }
        public required CommitDocumentRegisterImportHandler Commit { get; init; }
    }

    private static Harness Setup()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var register = new FakeRegisterRepository(TenantId);
        var batches = new FakeBatchRepository(TenantId);
        var preview = new DocumentRegisterImportPreviewService(register, batches);

        var ingestHandler = new IngestDocumentMasterRegisterHandler(register, tenant);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<IngestDocumentMasterRegisterCommand>(), It.IsAny<CancellationToken>()))
            .Returns((IngestDocumentMasterRegisterCommand c, CancellationToken t) => ingestHandler.Handle(c, t));

        var currentUser = new FakeCurrentUserContext("qa.import@diten.test");

        return new Harness
        {
            Register = register,
            Batches = batches,
            DryRun = new DryRunDocumentRegisterImportHandler(preview, tenant),
            Commit = new CommitDocumentRegisterImportHandler(mediator.Object, batches, tenant, currentUser)
        };
    }

    // ── Dry-run never writes ──────────────────────────────────────────────

    [Fact]
    public async Task Dry_run_reports_all_created_on_an_empty_register_and_writes_nothing()
    {
        var h = Setup();
        var csv = Csv(Row("UID-1", "C-1", "Doc One", "Draft", linkable: true));

        var response = await h.DryRun.Handle(new DryRunDocumentRegisterImportCommand("r.csv", csv, Corr), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var preview = response.Data!;
        Assert.Equal(1, preview.TotalRows);
        Assert.Equal(1, preview.Created);
        Assert.Equal(0, preview.Updated);
        Assert.Equal(0, preview.Unchanged);
        Assert.False(preview.AlreadyImported);
        // MUTATION GUARD: the register must be untouched by a dry-run.
        Assert.Empty(h.Register.Items);
    }

    [Fact]
    public async Task Dry_run_reports_unchanged_when_the_register_already_matches_and_writes_nothing()
    {
        var h = Setup();
        var csv = Csv(Row("UID-1", "C-1", "Doc One", "Draft", linkable: true));
        await h.Commit.Handle(new CommitDocumentRegisterImportCommand(
            "r.csv", csv, DocumentReferenceListParserHash(csv), Corr), CancellationToken.None);
        var beforeTitle = h.Register.Items.Single().DocumentTitle;

        // A SECOND file, byte-different (so it is a new hash / new dry-run), but describing the SAME row unchanged.
        var csv2 = Csv(Row("UID-1", "C-1", "Doc One", "Draft", linkable: true), Row("UID-2", "C-2", "Doc Two", "Draft", linkable: true));
        var response = await h.DryRun.Handle(new DryRunDocumentRegisterImportCommand("r2.csv", csv2, Corr), CancellationToken.None);

        var preview = response.Data!;
        Assert.Equal(2, preview.TotalRows);
        Assert.Equal(1, preview.Created);   // UID-2 is new
        Assert.Equal(0, preview.Updated);
        Assert.Equal(1, preview.Unchanged); // UID-1 already matches
        // MUTATION GUARD: the dry-run touched nothing — the existing row is byte-identical to what commit wrote.
        Assert.Equal(beforeTitle, h.Register.Items.Single(e => e.PermanentUid == "UID-1").DocumentTitle);
        Assert.Single(h.Register.Items); // still just the one row committed earlier — UID-2 was NOT written
    }

    [Fact]
    public async Task Dry_run_reports_updated_when_a_mapped_field_would_actually_change()
    {
        var h = Setup();
        var csv = Csv(Row("UID-1", "C-1", "Doc One", "Draft", linkable: true));
        await h.Commit.Handle(new CommitDocumentRegisterImportCommand(
            "r.csv", csv, DocumentReferenceListParserHash(csv), Corr), CancellationToken.None);

        // Same UID, renamed — a genuine change to a field Apply sets (DocumentTitle).
        var renamed = Csv(Row("UID-1", "C-1", "Doc One Renamed", "Draft", linkable: true));
        var response = await h.DryRun.Handle(new DryRunDocumentRegisterImportCommand("r3.csv", renamed, Corr), CancellationToken.None);

        var preview = response.Data!;
        Assert.Equal(0, preview.Created);
        Assert.Equal(1, preview.Updated);
        Assert.Equal(0, preview.Unchanged);
        // MUTATION GUARD: dry-run must not have applied the rename.
        Assert.Equal("Doc One", h.Register.Items.Single().DocumentTitle);
    }

    [Fact]
    public async Task Dry_run_reports_the_already_imported_flag_for_a_previously_committed_file()
    {
        var h = Setup();
        var csv = Csv(Row("UID-1", "C-1", "Doc One", "Draft", linkable: true));
        await h.Commit.Handle(new CommitDocumentRegisterImportCommand(
            "r.csv", csv, DocumentReferenceListParserHash(csv), Corr), CancellationToken.None);

        var response = await h.DryRun.Handle(new DryRunDocumentRegisterImportCommand("r.csv", csv, Corr), CancellationToken.None);

        Assert.True(response.Data!.AlreadyImported);
        Assert.Equal("qa.import@diten.test", response.Data.AlreadyImportedBy);
        Assert.NotNull(response.Data.AlreadyImportedAt);
    }

    [Fact]
    public async Task Dry_run_reports_the_citable_by_quality_decision_split()
    {
        var h = Setup();
        var csv = Csv(
            Row("UID-1", "C-1", "Doc One", "Draft", linkable: true),
            Row("UID-2", "C-2", "Doc Two", "Void", linkable: false, blockedReason: "terminal"));

        var response = await h.DryRun.Handle(new DryRunDocumentRegisterImportCommand("r.csv", csv, Corr), CancellationToken.None);

        var preview = response.Data!;
        Assert.Equal(1, preview.CitableByQualityDecisionYes);
        Assert.Equal(1, preview.CitableByQualityDecisionNo);
        Assert.Equal(1, preview.Blocked);
        Assert.Equal(1, preview.LifecycleDistribution["Draft"]);
        Assert.Equal(1, preview.LifecycleDistribution["Retired"]);
    }

    // ── Commit: hash idempotency ──────────────────────────────────────────

    [Fact]
    public async Task Commit_refuses_when_the_content_no_longer_matches_the_previewed_hash()
    {
        var h = Setup();
        var csv = Csv(Row("UID-1", "C-1", "Doc One", "Draft", linkable: true));

        var result = await h.Commit.Handle(
            new CommitDocumentRegisterImportCommand("r.csv", csv, "stale-hash-from-an-older-preview", Corr), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("IMPORT_CONTENT_CHANGED", result.ReasonCode);
        Assert.Empty(h.Register.Items); // MUTATION GUARD: refused before any write
    }

    [Fact]
    public async Task A_second_commit_of_the_exact_same_bytes_is_refused_not_duplicated()
    {
        var h = Setup();
        var csv = Csv(Row("UID-1", "C-1", "Doc One", "Draft", linkable: true));
        var hash = DocumentReferenceListParserHash(csv);

        var first = await h.Commit.Handle(new CommitDocumentRegisterImportCommand("r.csv", csv, hash, Corr), CancellationToken.None);
        var second = await h.Commit.Handle(new CommitDocumentRegisterImportCommand("r.csv", csv, hash, Corr), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal("IMPORT_ALREADY_APPLIED", second.ReasonCode);
        Assert.Single(h.Register.Items);  // not duplicated
        Assert.Single(h.Batches.Items);   // not a second batch row
    }

    [Fact]
    public async Task A_committed_batch_records_the_actor_hash_and_counts()
    {
        var h = Setup();
        var csv = Csv(
            Row("UID-1", "C-1", "Doc One", "Draft", linkable: true),
            Row("UID-2", "C-2", "Doc Two", "Void", linkable: false, blockedReason: "terminal"));
        var hash = DocumentReferenceListParserHash(csv);

        var result = await h.Commit.Handle(new CommitDocumentRegisterImportCommand("register.csv", csv, hash, Corr), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var batch = Assert.Single(h.Batches.Items);
        Assert.Equal(hash, batch.ContentHash);
        Assert.Equal("register.csv", batch.FileName);
        Assert.Equal("qa.import@diten.test", batch.Actor);
        Assert.Equal(2, batch.TotalRows);
        Assert.Equal(2, batch.Created);
        Assert.Equal(1, batch.Blocked);
        Assert.Equal(TenantId, batch.TenantId);
    }

    // ── Cross-tenant isolation ─────────────────────────────────────────────

    [Fact]
    public async Task Another_tenants_rows_are_never_touched_by_this_tenants_import()
    {
        var otherTenant = Guid.NewGuid();
        var h = Setup();
        h.Register.Items.Add(new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = otherTenant, PermanentUid = "UID-OTHER", DocumentCode = "C-OTHER",
            DocumentTitle = "Other Tenant Doc", LifecycleStatus = ControlledDocumentLifecycleStatus.Draft,
        });

        var csv = Csv(Row("UID-1", "C-1", "Doc One", "Draft", linkable: true));
        await h.Commit.Handle(new CommitDocumentRegisterImportCommand(
            "r.csv", csv, DocumentReferenceListParserHash(csv), Corr), CancellationToken.None);

        var otherRow = h.Register.Items.Single(e => e.TenantId == otherTenant);
        Assert.Equal("Other Tenant Doc", otherRow.DocumentTitle);
        Assert.Null(otherRow.UpdatedAt);
        Assert.False(otherRow.CitableByQualityDecision); // never touched by the mapping at all
    }

    private static string DocumentReferenceListParserHash(string csv) =>
        Diten.Platform.Application.Features.Tasks.Services.DocumentReferenceListParser.HashContent(csv);

    // ── fixtures ──────────────────────────────────────────────────────────

    private sealed class FakeCurrentUserContext(string actorName) : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string? Email { get; } = actorName;
        public string? DisplayName { get; } = actorName;
        public string ActorName { get; } = actorName;
        public bool IsAuthenticated { get; } = true;
    }

    private sealed class FakeRegisterRepository(Guid tenantId) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];

        private IEnumerable<DocumentMasterRegisterEntry> Scoped =>
            Items.Where(x => x.TenantId == tenantId && !x.IsDeleted);

        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default)
        {
            Items.Add(entry);
            return Task.FromResult(entry);
        }

        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string permanentUid, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.PermanentUid == permanentUid));

        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == entry.Id);
            if (i >= 0) Items[i] = entry;
            return Task.FromResult(i >= 0);
        }

        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());

        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string documentCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeBatchRepository(Guid tenantId) : IDocumentRegisterImportBatchRepository
    {
        public List<DocumentRegisterImportBatch> Items { get; } = [];

        public Task<DocumentRegisterImportBatch> CreateAsync(DocumentRegisterImportBatch batch, CancellationToken ct = default)
        {
            Items.Add(batch);
            return Task.FromResult(batch);
        }

        public Task<DocumentRegisterImportBatch?> FindByContentHashAsync(string contentHash, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(b => b.TenantId == tenantId && b.ContentHash == contentHash));

        public Task<IReadOnlyList<DocumentRegisterImportBatch>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRegisterImportBatch>>(
                Items.Where(b => b.TenantId == tenantId).OrderByDescending(b => b.AppliedAt).ToList());
    }
}
