using Diten.Platform.Application.Features.Tasks.Services;
using Xunit;

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
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "docs", "reference", "integrations", "gmg-qms")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(
            dir!, "docs", "reference", "integrations", "gmg-qms", "GMG_ERP_Document_Reference_List_2026-08-24.csv"));
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
