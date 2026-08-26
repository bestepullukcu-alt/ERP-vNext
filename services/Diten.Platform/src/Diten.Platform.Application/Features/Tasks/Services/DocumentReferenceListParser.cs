using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Domain.Entities.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// DCP-005 slice 2 — reads the counterparty's controlled-document register into lookup rows.
///
/// <para><b>⚠ THE FILE IS NEVER EDITED.</b> Everything under <c>docs/integration/gmg-qms/</c> is the other
/// side's input. A column "corrected" on the way in is an edit to somebody else's register, made invisibly.
/// Values are trimmed and passed through; nothing is normalised into a different word.</para>
/// </summary>
public static class DocumentReferenceListParser
{
    /// <summary>Every column the register carries. All seventeen are read; see <see cref="Parse"/>.</summary>
    public static readonly string[] ExpectedColumns =
    [
        "document_uid", "document_code", "title", "gqms_domain", "gqms_type", "erp_document_type",
        "version", "status", "criticality", "owner", "effective_date", "review_cycle",
        "folder_id", "folder_path", "is_mandatory_group_sop", "linkable_in_erp", "link_blocked_reason"
    ];

    public sealed record ParseResult(
        IReadOnlyList<DocumentReferenceEntry> Entries,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> MissingColumns,
        IReadOnlyList<string> UnreadColumns,
        int LinkableCount,
        string ContentHash);

    /// <summary>
    /// SHA-256 over the raw bytes. Deterministic, so re-uploading the same file is RECOGNISABLE rather than
    /// silently duplicated — the same role <c>SnapshotHash</c> plays for the folder taxonomy.
    /// </summary>
    public static string HashContent(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    /// <summary>
    /// ⚠ TENANT AND VERSION ARE PARAMETERS, not fields patched afterwards. `ListVersionId` and `TenantId` are
    /// `required`, so a handler cannot fill them in later — and that is the type system doing its job: a row
    /// that could exist without knowing which import it belongs to is a row that can be orphaned.
    /// </summary>
    public static ParseResult Parse(
        string csv, Guid tenantId = default, Guid listVersionId = default, string? createdBy = null)
    {
        var errors = new List<string>();
        var entries = new List<DocumentReferenceEntry>();
        var lines = SplitLines(csv);

        if (lines.Count == 0)
        {
            return new ParseResult([], ["The file is empty."], ExpectedColumns, [], 0, HashContent(csv));
        }

        var header = SplitCsvLine(lines[0]).Select(h => h.Trim().TrimStart('﻿').ToLowerInvariant()).ToList();
        var missing = ExpectedColumns.Where(c => !header.Contains(c)).ToList();
        /*
         * ⚠ COLUMNS WE DO NOT READ ARE REPORTED, not ignored. A register that grows a column is telling us
         * something; discovering it months later from a support question is the expensive way to find out.
         */
        var unread = header.Where(h => h.Length > 0 && !ExpectedColumns.Contains(h)).ToList();

        if (missing.Count > 0)
        {
            return new ParseResult([], [$"Missing columns: {string.Join(", ", missing)}"], missing, unread, 0,
                HashContent(csv));
        }

        var index = ExpectedColumns.ToDictionary(c => c, c => header.IndexOf(c), StringComparer.Ordinal);
        var seenUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var cells = SplitCsvLine(lines[i]);
            string Cell(string column)
            {
                var at = index[column];
                return at >= 0 && at < cells.Count ? cells[at].Trim() : string.Empty;
            }

            var uid = Cell("document_uid");
            if (uid.Length == 0)
            {
                errors.Add($"Row {i + 1}: document_uid is empty.");
                continue;
            }

            // The UID is the join key a task will freeze; two rows claiming it would make that freeze ambiguous.
            if (!seenUids.Add(uid))
            {
                errors.Add($"Row {i + 1}: document_uid '{uid}' appears more than once.");
                continue;
            }

            var linkable = string.Equals(Cell("linkable_in_erp"), "yes", StringComparison.OrdinalIgnoreCase);
            var blockedReason = Cell("link_blocked_reason");

            /*
             * ⚠ A BLOCKED ROW WITH NO REASON IS AN ERROR, not a silent import. "You cannot cite this" without
             * "because" is the shape of message this programme has spent a session removing.
             */
            if (!linkable && blockedReason.Length == 0)
            {
                errors.Add($"Row {i + 1}: '{uid}' is not linkable but gives no reason.");
                continue;
            }

            entries.Add(new DocumentReferenceEntry
            {
                TenantId = tenantId,
                ListVersionId = listVersionId,
                CreatedBy = createdBy ?? string.Empty,
                DocumentUid = uid,
                DocumentCode = Cell("document_code"),
                Title = Cell("title"),
                GqmsDomain = NullIfEmpty(Cell("gqms_domain")),
                GqmsType = NullIfEmpty(Cell("gqms_type")),
                ErpDocumentType = NullIfEmpty(Cell("erp_document_type")),
                DocumentVersion = NullIfEmpty(Cell("version")),
                // Passed through unchanged — including "NOT REGISTERED", which is QA's own open finding and
                // theirs to close.
                Status = NullIfEmpty(Cell("status")),
                Criticality = NullIfEmpty(Cell("criticality")),
                Owner = NullIfEmpty(Cell("owner")),
                EffectiveDate = NullIfEmpty(Cell("effective_date")),
                ReviewCycle = NullIfEmpty(Cell("review_cycle")),
                FolderId = NullIfEmpty(Cell("folder_id")),
                FolderPath = NullIfEmpty(Cell("folder_path")),
                IsMandatoryGroupSop = IsYes(Cell("is_mandatory_group_sop")),
                LinkableInErp = linkable,
                LinkBlockedReason = NullIfEmpty(blockedReason)
            });
        }

        return new ParseResult(
            entries, errors, missing, unread, entries.Count(e => e.LinkableInErp), HashContent(csv));
    }

    private static bool IsYes(string value)
        => string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static List<string> SplitLines(string csv)
        => csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();

    /// <summary>
    /// A minimal CSV splitter: commas separate, double quotes group, a doubled quote is a literal one.
    ///
    /// <para>Written here rather than pulled in: the register's own file uses quoted commas in
    /// <c>link_blocked_reason</c> (QA's finding sentence contains one), so naive splitting corrupts exactly the
    /// column that explains why a document cannot be cited.</para>
    /// </summary>
    private static List<string> SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }
}
