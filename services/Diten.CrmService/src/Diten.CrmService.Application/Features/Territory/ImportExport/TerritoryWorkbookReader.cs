using System.Globalization;
using ClosedXML.Excel;

namespace Diten.CrmService.Application.Features.Territory.ImportExport;

/// <summary>One parsed sheet row. <see cref="RowNumber"/> is the REAL Excel row number so a preview message points at
/// the line the user sees. Values are keyed by the canonical schema column name.</summary>
public sealed record TerritoryParsedRow(string Sheet, int RowNumber, IReadOnlyDictionary<string, string?> Values)
{
    public string? Get(string column) => Values.TryGetValue(column, out var value) ? value?.Trim() : null;

    public bool Has(string column) => !string.IsNullOrWhiteSpace(Get(column));

    /// <summary>True when the cell explicitly asks for the field to be emptied (<c>&lt;CLEAR&gt;</c>).</summary>
    public bool IsClear(string column)
        => string.Equals(Get(column), TerritoryWorkbookSchema.ClearToken, StringComparison.OrdinalIgnoreCase);
}

public sealed record TerritoryParsedWorkbook(
    IReadOnlyDictionary<string, IReadOnlyList<TerritoryParsedRow>> Sheets,
    IReadOnlyList<string> FileErrors,
    IReadOnlyList<string> FileWarnings,
    IReadOnlyList<string> PresentSheets)
{
    public bool IsReadable => FileErrors.Count == 0;

    public IReadOnlyList<TerritoryParsedRow> Rows(string sheet)
        => Sheets.TryGetValue(sheet, out var rows) ? rows : [];
}

/// <summary>
/// MOD-0151 FU08 reader — parses a FU08 workbook back into rows.
///
/// <para>Contract notes:</para>
/// <list type="bullet">
/// <item>Headers match case/whitespace-insensitively, so a reordered header row still imports; a missing mandatory
/// column is a FILE-level error (row-level messages would be meaningless).</item>
/// <item>Unknown extra columns are a warning and are ignored — a user annotating the sheet must not be blocked.</item>
/// <item>A <c>TenantId</c> column is ignored and reported: tenancy comes from the caller's claim (pack §22.5).</item>
/// <item>Every cell is read as text; Excel may have retyped a code into a number or a date into a serial, and both are
/// converted back so the value round-trips unchanged.</item>
/// <item>The ReferenceValues sheet is NEVER trusted as a validation source — it is a helper for the human. Reference
/// values are validated against MOD-0048 published values by the engine, exactly like the single-write path.</item>
/// <item>CoverageSummary / PlanVsCurrent are not parsed at all: they are export-only read models, so "import them"
/// is not expressible here.</item>
/// </list>
/// </summary>
public static class TerritoryWorkbookReader
{
    public const string TenantIdColumnWarning =
        "A 'TenantId' column was found and ignored — tenancy always comes from your login, never from the file.";

    public static TerritoryParsedWorkbook Read(Stream stream)
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
            return new TerritoryParsedWorkbook(
                new Dictionary<string, IReadOnlyList<TerritoryParsedRow>>(),
                ["The file could not be read as an .xlsx workbook. Download a fresh template and try again."],
                warnings, []);
        }

        using (workbook)
        {
            var sheets = new Dictionary<string, IReadOnlyList<TerritoryParsedRow>>(StringComparer.OrdinalIgnoreCase);
            var present = new List<string>();

            foreach (var sheetName in TerritoryWorkbookSchema.ImportableSheets)
            {
                var rows = ReadSheet(workbook, sheetName, errors, warnings, out var exists);
                sheets[sheetName] = rows;
                if (exists) present.Add(sheetName);
            }

            foreach (var exportOnly in TerritoryWorkbookSchema.ExportOnlySheets)
            {
                if (workbook.Worksheets.TryGetWorksheet(exportOnly, out _))
                {
                    warnings.Add($"Sheet '{exportOnly}' is an export-only read model and was ignored; it cannot be imported.");
                }
            }

            if (present.Count == 0)
            {
                errors.Add("The workbook contains none of the importable sheets ("
                           + string.Join(", ", TerritoryWorkbookSchema.ImportableSheets) + ").");
            }

            return new TerritoryParsedWorkbook(sheets, errors, warnings, present);
        }
    }

    private static List<TerritoryParsedRow> ReadSheet(
        XLWorkbook workbook, string sheetName, List<string> errors, List<string> warnings, out bool exists)
    {
        var rows = new List<TerritoryParsedRow>();
        exists = workbook.Worksheets.TryGetWorksheet(sheetName, out var sheet);
        if (!exists)
        {
            return rows;
        }

        var schemaColumns = TerritoryWorkbookSchema.ColumnsFor(sheetName);
        var map = new Dictionary<int, string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unknown = new List<string>();
        var tenantIdSeen = false;

        foreach (var cell in sheet.Row(1).CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (header.Length == 0) continue;

            if (Normalize(header) == Normalize(TerritoryWorkbookSchema.TenantIdColumn))
            {
                tenantIdSeen = true;
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

        if (tenantIdSeen)
        {
            warnings.Add($"Sheet '{sheetName}': {TenantIdColumnWarning}");
        }

        if (unknown.Count > 0)
        {
            warnings.Add($"Sheet '{sheetName}' has unrecognised column(s) that were ignored: {string.Join(", ", unknown)}.");
        }

        foreach (var column in TerritoryWorkbookSchema.MandatoryColumnsFor(sheetName).Where(c => !seen.Contains(c)))
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
                if (!string.IsNullOrWhiteSpace(text)) any = true;
            }

            // Entirely blank rows are skipped silently — Excel leaves plenty of them behind.
            if (any) rows.Add(new TerritoryParsedRow(sheetName, r, values));
        }

        return rows;
    }

    /// <summary>
    /// Reads a cell back as the literal text the user meant. Excel silently retypes input, so: a number keeps its
    /// digits (no scientific notation, no trailing ".0"), a date/serial becomes ISO <c>yyyy-MM-dd</c>, and a boolean
    /// becomes TRUE/FALSE.
    /// </summary>
    private static string? ReadCell(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;

        switch (cell.DataType)
        {
            case XLDataType.DateTime:
                return cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            case XLDataType.Boolean:
                return cell.GetBoolean() ? "TRUE" : "FALSE";

            case XLDataType.Number:
            {
                var number = cell.GetDouble();
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
