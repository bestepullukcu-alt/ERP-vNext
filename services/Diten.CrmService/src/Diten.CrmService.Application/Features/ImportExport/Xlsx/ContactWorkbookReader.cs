using System.Globalization;
using ClosedXML.Excel;

namespace Diten.CrmService.Application.Features.ImportExport.Xlsx;

/// <summary>One parsed sheet row. <see cref="RowNumber"/> is the REAL Excel row number so a preview message points at
/// the line the user sees. Values are keyed by the canonical schema column name.</summary>
public sealed record ParsedRow(string Sheet, int RowNumber, IReadOnlyDictionary<string, string?> Values)
{
    public string? Get(string column) => Values.TryGetValue(column, out var value) ? value : null;

    public bool Has(string column) => Values.ContainsKey(column) && !string.IsNullOrWhiteSpace(Values[column]);
}

public sealed record ParsedWorkbook(
    IReadOnlyList<ParsedRow> ContactRows,
    IReadOnlyList<ParsedRow> LinkRows,
    IReadOnlyList<string> FileErrors,
    IReadOnlyList<string> FileWarnings,
    bool HasContactsSheet,
    bool HasLinksSheet)
{
    public bool IsReadable => FileErrors.Count == 0;
}

/// <summary>
/// MOD-0150 Import/Export Task 2 — reads a Task 1 workbook back into rows.
///
/// Contract notes:
/// • Headers are matched case/whitespace-insensitively against <see cref="ContactWorkbookSchema"/>; a file whose
///   header row was reordered still imports, a file missing a required column fails at FILE level (never row level).
/// • Unknown extra columns are reported as a warning and ignored — a user annotating the sheet must not be blocked.
/// • Every cell is read as text. Excel may have turned a phone/postal/external id into a number or a date into a
///   serial; both are converted back to their literal/ISO form so the value round-trips unchanged.
/// • The ReferenceData sheet is NEVER trusted as a validation source — it is a helper for the human. Reference values
///   are validated against MOD-0048 published values by the import engine, exactly like the single-write path.
/// </summary>
public static class ContactWorkbookReader
{
    public static ParsedWorkbook Read(Stream stream)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception)
        {
            // Wrong format / corrupt / password-protected. The exception text can carry the file path — never surface it.
            return new ParsedWorkbook(
                Array.Empty<ParsedRow>(), Array.Empty<ParsedRow>(),
                new[] { "The file could not be read as an .xlsx workbook. Download a fresh template and try again." },
                warnings, false, false);
        }

        using (workbook)
        {
            var contacts = ReadSheet(
                workbook, ContactWorkbookSchema.ContactsSheet, ContactWorkbookSchema.ContactColumns,
                required: true, errors, warnings, out var hasContacts);

            var links = ReadSheet(
                workbook, ContactWorkbookSchema.AccountLinksSheet, ContactWorkbookSchema.AccountLinkColumns,
                required: false, errors, warnings, out var hasLinks);

            if (!hasContacts && !hasLinks)
            {
                errors.Add($"The workbook contains neither a '{ContactWorkbookSchema.ContactsSheet}' nor an "
                           + $"'{ContactWorkbookSchema.AccountLinksSheet}' sheet.");
            }

            return new ParsedWorkbook(contacts, links, errors, warnings, hasContacts, hasLinks);
        }
    }

    private static List<ParsedRow> ReadSheet(
        XLWorkbook workbook,
        string sheetName,
        IReadOnlyList<string> schemaColumns,
        bool required,
        List<string> errors,
        List<string> warnings,
        out bool exists)
    {
        var rows = new List<ParsedRow>();
        exists = workbook.Worksheets.TryGetWorksheet(sheetName, out var sheet);
        if (!exists)
        {
            if (required)
            {
                // Not fatal on its own: a links-only file is legitimate. The caller decides (see Read()).
                warnings.Add($"The workbook has no '{sheetName}' sheet; nothing was imported from it.");
            }

            return rows;
        }

        var headerRow = sheet.Row(1);
        var map = new Dictionary<int, string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unknown = new List<string>();

        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (header.Length == 0)
            {
                continue;
            }

            var match = schemaColumns.FirstOrDefault(c => Normalize(c) == Normalize(header));
            if (match is null)
            {
                unknown.Add(header);
                continue;
            }

            if (!seen.Add(match))
            {
                errors.Add($"Sheet '{sheetName}' has the column '{match}' more than once.");
                continue;
            }

            map[cell.Address.ColumnNumber] = match;
        }

        if (unknown.Count > 0)
        {
            warnings.Add($"Sheet '{sheetName}' has unrecognised column(s) that were ignored: {string.Join(", ", unknown)}.");
        }

        // Only the identity/decision columns are structurally required; everything else may legitimately be absent
        // from a hand-built file. A missing one is a FILE error, because row-level messages would be meaningless.
        var mandatory = sheetName == ContactWorkbookSchema.ContactsSheet
            ? new[] { ContactWorkbookSchema.OperationColumn, ContactWorkbookSchema.ContactIdColumn }
            : new[] { ContactWorkbookSchema.OperationColumn };

        foreach (var column in mandatory.Where(c => !seen.Contains(c)))
        {
            errors.Add($"Sheet '{sheetName}' is missing the required column '{column}'.");
        }

        if (map.Count == 0)
        {
            errors.Add($"Sheet '{sheetName}' has no recognised columns. Use the current import template.");
            return rows;
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var any = false;

            foreach (var (columnNumber, column) in map)
            {
                var text = ReadCell(sheet.Cell(r, columnNumber));
                values[column] = text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    any = true;
                }
            }

            if (any)
            {
                rows.Add(new ParsedRow(sheetName, r, values));
            }
        }

        return rows;
    }

    /// <summary>
    /// Reads a cell back as the literal text the user meant. Excel silently retypes input, so:
    /// a number keeps its digits (no scientific notation, no trailing ".0" — a phone stays a phone), a date/serial
    /// becomes ISO <c>yyyy-MM-dd</c>, and a boolean becomes TRUE/FALSE.
    /// </summary>
    private static string? ReadCell(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return null;
        }

        switch (cell.DataType)
        {
            case XLDataType.DateTime:
                return cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            case XLDataType.Boolean:
                return cell.GetBoolean() ? "TRUE" : "FALSE";

            case XLDataType.Number:
            {
                var number = cell.GetDouble();
                // A whole number must not come back as "5.32123E+09" or "905321234567.0".
                return number == Math.Floor(number) && Math.Abs(number) < 1e15
                    ? ((long)number).ToString(CultureInfo.InvariantCulture)
                    : number.ToString("0.################", CultureInfo.InvariantCulture);
            }

            default:
            {
                var text = cell.GetString().Trim();
                return text.Length == 0 ? null : text;
            }
        }
    }

    private static string Normalize(string value)
        => new(value.Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-').Select(char.ToLowerInvariant).ToArray());
}
