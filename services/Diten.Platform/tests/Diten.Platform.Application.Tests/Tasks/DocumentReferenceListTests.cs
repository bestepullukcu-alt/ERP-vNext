using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// DCP-005 slice 2 — the controlled-document reference list, read against THE REAL FILE.
///
/// <para>The counterparty's own register is in the repository, so these are measurements rather than fixtures:
/// a hand-written sample would agree with whatever the parser happens to do.</para>
/// </summary>
public sealed class DocumentReferenceListTests
{
    private static string RealFile()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "docs", "integration", "gmg-qms")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(
            dir!, "docs", "integration", "gmg-qms", "GMG_ERP_Document_Reference_List_2026-08-24.csv"));
    }

    [Fact]
    public void The_whole_register_parses__358_rows_with_no_errors()
    {
        var result = DocumentReferenceListParser.Parse(RealFile());

        Assert.Empty(result.Errors);
        Assert.Equal(358, result.Entries.Count);
    }

    [Fact]
    public void Every_one_of_the_seventeen_columns_is_read()
    {
        /*
         * MUTATION GUARD: drop a column from `ExpectedColumns` and this goes red — either as a missing column
         * or as an unread one, depending on which end it is dropped from.
         */
        var result = DocumentReferenceListParser.Parse(RealFile());

        Assert.Empty(result.MissingColumns);
        Assert.Empty(result.UnreadColumns);
        Assert.Equal(17, DocumentReferenceListParser.ExpectedColumns.Length);
    }

    [Fact]
    public void The_blocked_rows_are_IMPORTED__322_linkable_and_36_not()
    {
        /*
         * MUTATION GUARD: drop the non-linkable rows on import and the count falls to 322.
         *
         * They are shown with a reason rather than hidden — the OPPOSITE of the zero-count chip decision, and
         * deliberately: there the population did not exist, here the document exists and cannot be cited.
         */
        var result = DocumentReferenceListParser.Parse(RealFile());

        Assert.Equal(322, result.LinkableCount);
        Assert.Equal(36, result.Entries.Count(e => !e.LinkableInErp));
        // …and every blocked row can say why.
        Assert.All(result.Entries.Where(e => !e.LinkableInErp), e => Assert.False(string.IsNullOrWhiteSpace(e.LinkBlockedReason)));
    }

    [Fact]
    public void The_six_unregistered_rows_survive_the_import()
    {
        /*
         * QA's own open finding: mandatory Group SOPs with a UID allocated and no row in the master register.
         * Skipping them silently would hide a finding the counterparty is tracking.
         */
        var result = DocumentReferenceListParser.Parse(RealFile());
        var notRegistered = result.Entries.Where(e => e.Status == "NOT REGISTERED").ToList();

        Assert.Equal(6, notRegistered.Count);
        Assert.All(notRegistered, e => Assert.False(e.LinkableInErp));
    }

    [Fact]
    public void A_quoted_comma_does_not_split_the_reason_that_explains_the_block()
    {
        /*
         * The register's own finding sentence contains a comma inside quotes, and it sits in
         * `link_blocked_reason` — the column whose whole job is explaining why a document cannot be cited.
         * Naive splitting corrupts exactly that.
         */
        var result = DocumentReferenceListParser.Parse(RealFile());
        var finding = result.Entries.First(e => e.Status == "NOT REGISTERED");

        Assert.Contains("Document Master Register", finding.LinkBlockedReason);
    }

    [Fact]
    public void The_same_bytes_hash_the_same__which_is_what_makes_a_re_upload_recognisable()
    {
        var content = RealFile();
        Assert.Equal(
            DocumentReferenceListParser.HashContent(content),
            DocumentReferenceListParser.HashContent(content));
        Assert.NotEqual(
            DocumentReferenceListParser.HashContent(content),
            DocumentReferenceListParser.HashContent(content + "\n"));
    }

    [Fact]
    public void A_blocked_row_with_no_reason_is_refused()
    {
        // "You cannot cite this" without "because" is the shape of message this programme keeps removing.
        var header = string.Join(",", DocumentReferenceListParser.ExpectedColumns);
        var row = "UID-1,CODE-1,Title,,,,,,,,,,,,,no,";
        var result = DocumentReferenceListParser.Parse(header + "\n" + row);

        Assert.Empty(result.Entries);
        Assert.Contains(result.Errors, e => e.Contains("gives no reason"));
    }

    [Fact]
    public void A_duplicated_uid_is_refused__it_is_the_key_a_task_will_freeze()
    {
        var header = string.Join(",", DocumentReferenceListParser.ExpectedColumns);
        var rows = "UID-1,C1,T1,,,,,,,,,,,,,yes,\nUID-1,C2,T2,,,,,,,,,,,,,yes,";
        var result = DocumentReferenceListParser.Parse(header + "\n" + rows);

        Assert.Single(result.Entries);
        Assert.Contains(result.Errors, e => e.Contains("more than once"));
    }
}

/// <summary>
/// DCP-005 slice 2 — the IMPORT, not the parser.
///
/// <para>These exist because a mutation survived the parser tests: removing the "have I seen these bytes
/// before" check left every assertion green, because nothing asserted the RECOGNITION — only that the hash was
/// stable. A hash nobody compares is a checksum, not a version.</para>
/// </summary>
public sealed class DocumentReferenceListImportTests
{
    private static string RealFileBase64()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "docs", "integration", "gmg-qms")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        var bytes = File.ReadAllBytes(Path.Combine(
            dir!, "docs", "integration", "gmg-qms", "GMG_ERP_Document_Reference_List_2026-08-24.csv"));
        return Convert.ToBase64String(bytes);
    }

    private static (ImportDocumentReferenceListHandler Handler, FakeDocumentReferenceListRepository Repo) Subject()
    {
        var repo = new FakeDocumentReferenceListRepository();
        return (new ImportDocumentReferenceListHandler(
            repo, new FakeTenantContext(Guid.NewGuid()), new FakeCurrentUserContext(Guid.NewGuid())), repo);
    }

    private static ImportDocumentReferenceListRequest Request() => new(
        "GMG_ERP_Document_Reference_List_2026-08-24.csv", RealFileBase64(), "GMG-QMS-DOCREG", "2026-08-24");

    [Fact]
    public async Task The_whole_register_is_stored_as_ONE_version()
    {
        var (handler, repo) = Subject();

        var response = await handler.Handle(
            new ImportDocumentReferenceListCommand(Request(), "c1"), CancellationToken.None);

        Assert.Equal(201, response.StatusCode);
        Assert.Equal(358, response.Data!.EntryCount);
        Assert.Equal(322, response.Data.LinkableCount);
        Assert.Single(repo.Versions);
        Assert.Equal(358, repo.Entries.Count);
    }

    [Fact]
    public async Task The_SAME_bytes_are_RECOGNISED__not_stored_a_second_time()
    {
        /*
         * MUTATION GUARD: delete the hash lookup in the handler and this goes red — which it did not before,
         * because the only hash assertion was about determinism.
         *
         * Two people importing the register on the same afternoon must not produce two "current" lists: the
         * next question is always "which one did the task resolve against", and two answers is no answer.
         */
        var (handler, repo) = Subject();
        await handler.Handle(new ImportDocumentReferenceListCommand(Request(), "c1"), CancellationToken.None);

        var second = await handler.Handle(
            new ImportDocumentReferenceListCommand(Request(), "c2"), CancellationToken.None);

        Assert.Equal(409, second.StatusCode);
        Assert.Equal(TaskReasonCodes.DocumentListAlreadyImported, second.ReasonCode);
        // Nothing was written the second time — not a version, and not 358 more rows.
        Assert.Single(repo.Versions);
        Assert.Equal(358, repo.Entries.Count);
    }

    [Fact]
    public async Task Every_stored_row_belongs_to_the_version_that_imported_it()
    {
        // What makes "which list did this task see" answerable at all.
        var (handler, repo) = Subject();
        var response = await handler.Handle(
            new ImportDocumentReferenceListCommand(Request(), "c1"), CancellationToken.None);

        Assert.All(repo.Entries, e => Assert.Equal(response.Data!.Id, e.ListVersionId));
    }

    [Fact]
    public async Task A_file_that_will_not_parse_stores_NOTHING()
    {
        /*
         * All or nothing. A partially imported register is worse than none: the search would answer
         * confidently from a list nobody can characterise, and the failed rows would be invisible.
         */
        var (handler, repo) = Subject();
        var header = string.Join(",", DocumentReferenceListParser.ExpectedColumns);
        var broken = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(header + "\nUID-1,C1,T1,,,,,,,,,,,,,no,"));

        var response = await handler.Handle(
            new ImportDocumentReferenceListCommand(Request() with { ContentBase64 = broken }, "c1"),
            CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.Empty(repo.Versions);
        Assert.Empty(repo.Entries);
    }
}


/// <summary>The list in memory. No update method, because the interface has none — see its own note.</summary>
internal sealed class FakeDocumentReferenceListRepository : IDocumentReferenceListRepository
{
    public List<DocumentReferenceListVersion> Versions { get; } = [];
    public List<DocumentReferenceEntry> Entries { get; } = [];

    public Task<DocumentReferenceListVersion> CreateVersionAsync(
        DocumentReferenceListVersion version, CancellationToken ct = default)
    {
        Versions.Add(version);
        return Task.FromResult(version);
    }

    public Task<DocumentReferenceListVersion?> FindVersionByHashAsync(string contentHash, CancellationToken ct = default)
        => Task.FromResult(Versions.FirstOrDefault(v => v.ContentHash == contentHash));

    public Task<IReadOnlyList<DocumentReferenceListVersion>> ListVersionsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DocumentReferenceListVersion>>(
            Versions.OrderByDescending(v => v.ImportedAt).ToList());

    public Task<DocumentReferenceListVersion?> GetLatestVersionAsync(CancellationToken ct = default)
        => Task.FromResult(Versions.OrderByDescending(v => v.ImportedAt).FirstOrDefault());

    public Task AddEntriesAsync(IReadOnlyList<DocumentReferenceEntry> entries, CancellationToken ct = default)
    {
        Entries.AddRange(entries);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentReferenceEntry>> SearchAsync(
        Guid listVersionId, string? term, int limit, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DocumentReferenceEntry>>(
            Entries.Where(e => e.ListVersionId == listVersionId).Take(limit).ToList());
}
