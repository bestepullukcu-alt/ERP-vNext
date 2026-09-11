using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Commands;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Handlers.CommandHandlers;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// WP-DM-1a — Document Master Register CSV ingest tests. Proves the DM-0 status mapping (all six values), that the 36
/// CSV-blocked rows are imported/visible (never dropped) with a reason, CollectionInstanceId=Guid.Empty +
/// IsSystemAllocated=false, idempotent upsert by (TenantId, PermanentUid), fail-closed on an unexpected status, and
/// an empty title reported (not invented). The last test ingests the real 358-row GMG CSV end-to-end.
/// </summary>
public sealed class DocumentRegisterIngestTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string Corr = "wp-dm-1a-corr";

    private const string Header =
        "document_uid,document_code,title,gqms_domain,gqms_type,erp_document_type,version,status,criticality,owner,effective_date,review_cycle,folder_id,folder_path,is_mandatory_group_sop,linkable_in_erp,link_blocked_reason";

    // One row per DM-0 status. Blocked rows (Void/Planned/NOT REGISTERED) carry a reason (the parser requires it).
    private static readonly string SampleCsv = string.Join("\n",
        Header,
        "UID-1,C-1,Draft Doc,GMP,SOP,Sop,V1.0,Draft,Major,QA,,,F1,/f1,,yes,",
        "UID-2,C-2,Final Draft Doc,GMP,SOP,Sop,V0.9,Draft — final draft for approval,Major,QA,,,F2,/f2,,yes,",
        "UID-3,C-3,Void Doc,GMP,SOP,Sop,V1.0,Void,Major,QA,,,F3,/f3,,no,terminal non-citable",
        "UID-4,C-4,Planned Doc,GMP,SOP,Sop,V1.0,Planned,Major,QA,,,F4,/f4,,no,does not exist yet",
        "UID-5,C-5,NotReg Doc,GMP,SOP,Sop,V1.0,NOT REGISTERED,Major,QA,,,F5,/f5,,no,mandatory-but-unregistered",
        "UID-6,C-6,Executed Doc,GMP,SOP,Record,V1.0,Executed (source on file),Major,QA,,,F6,/f6,,yes,");

    [Fact]
    public async Task Ingest_maps_all_dm0_statuses_and_common_fields()
    {
        var f = Fixture();

        var response = await f.Handler.Handle(Cmd(SampleCsv), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(6, response.Data!.TotalRows);
        Assert.Equal(6, response.Data.Created);

        var byUid = f.Register.Items.ToDictionary(e => e.PermanentUid!, StringComparer.Ordinal);
        Assert.Equal(ControlledDocumentLifecycleStatus.Draft, byUid["UID-1"].LifecycleStatus);
        Assert.Equal(ControlledDocumentLifecycleStatus.InReview, byUid["UID-2"].LifecycleStatus);
        Assert.Equal(ControlledDocumentLifecycleStatus.Retired, byUid["UID-3"].LifecycleStatus);
        Assert.Equal(ControlledDocumentLifecycleStatus.Draft, byUid["UID-4"].LifecycleStatus);
        Assert.Equal(ControlledDocumentLifecycleStatus.Draft, byUid["UID-5"].LifecycleStatus);
        Assert.Equal(ControlledDocumentLifecycleStatus.Draft, byUid["UID-6"].LifecycleStatus);

        // Common map invariants (spot-check one row).
        var d1 = byUid["UID-1"];
        Assert.Equal("C-1", d1.DocumentCode);
        Assert.Equal("Draft Doc", d1.DocumentTitle);
        Assert.Equal("V1.0", d1.CurrentVersionLabel);
        Assert.False(d1.IsSystemAllocated);
        Assert.Equal(Guid.Empty, d1.CollectionInstanceId);
        Assert.Equal(TenantId, d1.TenantId);
    }

    [Fact]
    public async Task LinkableInErp_is_copied_verbatim_into_CitableByQualityDecision()
    {
        /*
         * DCP-005 Step 0, Part B — MUTATION GUARD: drop the `target.CitableByQualityDecision = src.LinkableInErp`
         * assignment in DocumentRegisterIngestMapping.Apply and this goes red for every yes/no row.
         */
        var f = Fixture();

        var response = await f.Handler.Handle(Cmd(SampleCsv), CancellationToken.None);
        var byUid = f.Register.Items.ToDictionary(e => e.PermanentUid!, StringComparer.Ordinal);

        // linkable_in_erp=yes: UID-1, UID-2, UID-6.
        Assert.True(byUid["UID-1"].CitableByQualityDecision);
        Assert.True(byUid["UID-2"].CitableByQualityDecision);
        Assert.True(byUid["UID-6"].CitableByQualityDecision);
        // linkable_in_erp=no: UID-3 (Void→Retired), UID-4, UID-5.
        Assert.False(byUid["UID-3"].CitableByQualityDecision);
        Assert.False(byUid["UID-4"].CitableByQualityDecision);
        Assert.False(byUid["UID-5"].CitableByQualityDecision);
        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task Blocked_rows_are_imported_visible_with_reason_and_not_counted_with_executed()
    {
        var f = Fixture();

        var response = await f.Handler.Handle(Cmd(SampleCsv), CancellationToken.None);

        // Exactly the 3 CSV-non-linkable rows (Void/Planned/NOT REGISTERED); Executed (linkable=yes) is NOT counted.
        Assert.Equal(3, response.Data!.Blocked);
        Assert.Equal(6, f.Register.Items.Count); // none dropped

        foreach (var uid in new[] { "UID-3", "UID-4", "UID-5" })
        {
            var e = f.Register.Items.Single(x => x.PermanentUid == uid);
            Assert.Equal(DocumentLinkScopeCompatibilityStatus.Invalid, e.LinkScopeCompatibilityStatus);
            Assert.False(string.IsNullOrWhiteSpace(e.StatusReason));
        }

        // Executed record: not blocked, own StatusReason, Unvalidated compatibility.
        var executed = f.Register.Items.Single(x => x.PermanentUid == "UID-6");
        Assert.Equal(DocumentLinkScopeCompatibilityStatus.Unvalidated, executed.LinkScopeCompatibilityStatus);
        Assert.Contains("Executed record", executed.StatusReason);

        // A plain Draft carries no blocked reason.
        Assert.Null(f.Register.Items.Single(x => x.PermanentUid == "UID-1").StatusReason);
    }

    [Fact]
    public async Task Re_import_is_idempotent_and_never_duplicates()
    {
        var f = Fixture();

        var first = await f.Handler.Handle(Cmd(SampleCsv), CancellationToken.None);
        var second = await f.Handler.Handle(Cmd(SampleCsv), CancellationToken.None);

        Assert.Equal(6, first.Data!.Created);
        Assert.Equal(0, first.Data.Updated);
        Assert.Equal(0, second.Data!.Created);   // re-import updates, never re-creates
        Assert.Equal(6, second.Data.Updated);
        Assert.Equal(6, f.Register.Items.Count);  // no duplicates
    }

    [Fact]
    public async Task Unexpected_status_fails_closed_without_writing()
    {
        var f = Fixture();
        var csv = string.Join("\n", Header,
            "UID-1,C-1,Draft Doc,GMP,SOP,Sop,V1.0,Draft,Major,QA,,,F1,/f1,,yes,",
            "UID-9,C-9,Bogus Doc,GMP,SOP,Sop,V1.0,SomethingUnmapped,Major,QA,,,F9,/f9,,yes,");

        var response = await f.Handler.Handle(Cmd(csv), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(422, response.StatusCode);
        Assert.Empty(f.Register.Items); // all-or-nothing: nothing written
    }

    [Fact]
    public async Task Empty_title_is_reported_and_row_skipped()
    {
        var f = Fixture();
        var csv = string.Join("\n", Header,
            "UID-1,C-1,,GMP,SOP,Sop,V1.0,Draft,Major,QA,,,F1,/f1,,yes,");

        var response = await f.Handler.Handle(Cmd(csv), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(0, response.Data!.Created);
        Assert.Contains(response.Data.Errors, e => e.Contains("title is empty"));
        Assert.Empty(f.Register.Items);
    }

    [Fact]
    public async Task Ingests_the_real_358_row_gmg_csv_end_to_end()
    {
        var path = FindRepoCsv();
        Assert.True(path is not null, "GMG reference CSV not found by walking up from the test base directory.");
        var csv = await File.ReadAllTextAsync(path!);
        var f = Fixture();

        var response = await f.Handler.Handle(Cmd(csv), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(358, response.Data!.TotalRows);
        Assert.Equal(358, response.Data.Created);
        Assert.Equal(36, response.Data.Blocked);            // Void(7)+Planned(23)+NOT REGISTERED(6)
        Assert.Empty(response.Data.Errors);                 // no unexpected status, no empty title
        Assert.Equal(358, f.Register.Items.Count);
        Assert.All(f.Register.Items, e => Assert.Equal(Guid.Empty, e.CollectionInstanceId));
        Assert.All(f.Register.Items, e => Assert.False(e.IsSystemAllocated));
        // 0 Effective today (Faz 1 truth) — everything maps to a non-effective lifecycle.
        Assert.DoesNotContain(f.Register.Items, e => e.LifecycleStatus == ControlledDocumentLifecycleStatus.Effective);
        // DCP-005 Step 0, AC2 — measured against the real CSV: 358 - 36 blocked = 322 rows carry
        // linkable_in_erp=yes, and that count lands verbatim on CitableByQualityDecision.
        Assert.Equal(322, f.Register.Items.Count(e => e.CitableByQualityDecision));
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    private static string? FindRepoCsv()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "reference", "integrations", "gmg-qms",
                "GMG_ERP_Document_Reference_List_2026-08-24.csv");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static IngestDocumentMasterRegisterCommand Cmd(string csv) => new(csv, "test", Corr);

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var register = new FakeRegisterRepository(tenant);
        return new Harness(new IngestDocumentMasterRegisterHandler(register, tenant), register);
    }

    private sealed record Harness(IngestDocumentMasterRegisterHandler Handler, FakeRegisterRepository Register);

    private sealed class FakeRegisterRepository(ITenantContext tenant) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];

        private IEnumerable<DocumentMasterRegisterEntry> Scoped =>
            Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

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
}
