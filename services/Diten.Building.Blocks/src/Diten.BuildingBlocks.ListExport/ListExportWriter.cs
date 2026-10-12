using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace Diten.BuildingBlocks.ListExport;

/// <summary>
/// Writes the rows a list query returned as the file the reader asked for. It formats; it never filters, sorts or
/// chooses columns — those are the query's and the whitelist's, so what is written is exactly what was resolved.
/// </summary>
public static class ListExportWriter
{
    public static byte[] Write<T>(ListExportFormat format, IReadOnlyList<ListExportColumn<T>> columns, IEnumerable<T> rows, CultureInfo culture, string sheetName)
        => format == ListExportFormat.Xlsx
            ? WriteXlsx(columns, rows, culture, sheetName)
            : WriteCsv(columns, rows, culture);

    /// <summary>
    /// RFC 4180: comma separator, CRLF rows, a field holding a comma, quote or line break is quoted with its quotes doubled.
    /// UTF-8 WITH a byte-order mark — without it Excel opens a Turkish or Chinese CSV as mojibake.
    /// </summary>
    public static byte[] WriteCsv<T>(IReadOnlyList<ListExportColumn<T>> columns, IEnumerable<T> rows, CultureInfo culture)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', columns.Select(c => CsvField(c.Header(culture))))).Append("\r\n");
        foreach (var row in rows)
        {
            sb.Append(string.Join(',', columns.Select(c => CsvField(c.Value(row, culture))))).Append("\r\n");
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(sb.ToString())];
    }

    /// <summary>
    /// XLSX with every cell TEXT (number format "@", string values): a code like "0012" or "1-2" stays as typed instead of
    /// Excel turning it into 12 or a date. Header row bold and frozen; right-to-left sheet for Arabic.
    /// </summary>
    public static byte[] WriteXlsx<T>(IReadOnlyList<ListExportColumn<T>> columns, IEnumerable<T> rows, CultureInfo culture, string sheetName)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SafeSheetName(sheetName));
        sheet.RightToLeft = culture.TextInfo.IsRightToLeft;

        for (var c = 0; c < columns.Count; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Style.NumberFormat.Format = "@";
            cell.SetValue(columns[c].Header(culture));
            cell.Style.Font.Bold = true;
            sheet.Column(c + 1).Width = Math.Clamp(columns[c].Header(culture).Length + 6, 14, 60);
            sheet.Column(c + 1).Style.NumberFormat.Format = "@";
        }

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = sheet.Cell(r, c + 1);
                cell.Style.NumberFormat.Format = "@";
                cell.SetValue(columns[c].Value(row, culture) ?? string.Empty);
            }

            r++;
        }

        sheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// CSV injection (OWASP): a cell that starts with = + - @ TAB or CR is a FORMULA to a spreadsheet opening the file — a
    /// user named "=HYPERLINK(…)" would become a live link in every exporter's Excel. Such a cell gets a leading apostrophe,
    /// which spreadsheets show as text. A plain number ("-5", "+3") is data, not a formula, and is left alone.
    /// </summary>
    internal static string NeutralizeFormula(string value)
    {
        if (value.Length == 0) return value;
        var first = value[0];
        if (first is not ('=' or '+' or '-' or '@' or '\t' or '\r')) return value;
        if ((first is '-' or '+') && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) return value;
        return "'" + value;
    }

    private static string CsvField(string? value)
    {
        var text = NeutralizeFormula(value ?? string.Empty);
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
    }

    private static string SafeSheetName(string name)
    {
        var cleaned = new string((name ?? string.Empty).Where(ch => ch is not ('[' or ']' or ':' or '*' or '?' or '/' or '\\')).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "Export";
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }
}
