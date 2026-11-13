using System.Text;
using ClosedXML.Excel;
using Diten.CrmService.Application.Features.Commercial.Pricing;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.2 ①: an xlsx (first worksheet) or csv (; or , separated, UTF-8 / UTF-16 BOM honoured)
/// price file → text rows. The first row is the header; the mapped columns are found by their header text (case- and
/// space-insensitive). Empty rows are skipped; the file is never stored.
/// </summary>
public sealed class PriceListFileParser : IPriceListFileParser
{
    public PriceListParseResult Parse(string fileName, byte[] content, PriceListFileColumns columns)
    {
        try
        {
            var table = Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ? Xlsx(content) : Csv(content);
            if (table.Count == 0)
            {
                return PriceListParseResult.Fail("The file has no header row.");
            }

            var header = table[0].Select(Key).ToList();
            int? Find(string? name)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                var index = header.IndexOf(Key(name));
                return index < 0 ? -1 : index;
            }

            var map = new[] { Find(columns.Gtin), Find(columns.SkuCode), Find(columns.DepotPrice), Find(columns.PharmacistPurchasePrice),
                Find(columns.PublicPrice), Find(columns.VatRate) };
            var names = new[] { columns.Gtin, columns.SkuCode, columns.DepotPrice, columns.PharmacistPurchasePrice, columns.PublicPrice, columns.VatRate };
            for (var i = 0; i < map.Length; i++)
            {
                if (map[i] == -1)
                {
                    return PriceListParseResult.Fail($"The column \"{names[i]}\" is not in the header row.");
                }
            }

            string? Cell(IReadOnlyList<string> row, int? index) => index is { } i && i >= 0 && i < row.Count && !string.IsNullOrWhiteSpace(row[i]) ? row[i].Trim() : null;
            var rows = new List<ParsedPriceRow>();
            for (var r = 1; r < table.Count; r++)
            {
                var row = table[r];
                if (row.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                rows.Add(new ParsedPriceRow(r + 1, Cell(row, map[0]), Cell(row, map[1]), Cell(row, map[2]), Cell(row, map[3]), Cell(row, map[4]),
                    Cell(row, map[5])));
                if (rows.Count > PricingLimits.ImportMaxRows)
                {
                    break; // the handler refuses more than the limit; no need to read the rest
                }
            }

            return new PriceListParseResult(true, null, rows);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return PriceListParseResult.Fail("The file cannot be read as " + Path.GetExtension(fileName) + ".");
        }
    }

    private static string Key(string? value) => new string((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

    private static List<IReadOnlyList<string>> Xlsx(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed();
        var table = new List<IReadOnlyList<string>>();
        if (used is null)
        {
            return table;
        }

        var lastColumn = used.LastColumn().ColumnNumber();
        foreach (var row in used.Rows())
        {
            var cells = new List<string>(lastColumn);
            for (var c = 1; c <= lastColumn; c++)
            {
                var cell = row.Cell(c);
                cells.Add(cell.Value.IsNumber
                    ? cell.Value.GetNumber().ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : cell.GetFormattedString());
            }

            table.Add(cells);
        }

        return table;
    }

    private static List<IReadOnlyList<string>> Csv(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var separator = lines.Length > 0 && lines[0].Count(c => c == ';') > lines[0].Count(c => c == ',') ? ';' : ',';
        var table = new List<IReadOnlyList<string>>();
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            table.Add(Split(line, separator));
        }

        return table;
    }

    /// <summary>One CSV line with quoted fields ("a;b" stays one field, "" is a quote).</summary>
    private static List<string> Split(string line, char separator)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == separator)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}

/// <summary>WP-VW-W6 W6-BE-b — <c>Commercial:Pricing:Workflow:TemplateCode</c> (default COM-PRICING-PUBLISH; W6-CFG creates
/// it live) and <c>…:ReconcileAfterSeconds</c>.</summary>
public sealed class ConfigurationPricingApprovalSettings : IPricingApprovalSettings
{
    public ConfigurationPricingApprovalSettings(IConfiguration configuration)
    {
        TemplateCode = string.IsNullOrWhiteSpace(configuration["Commercial:Pricing:Workflow:TemplateCode"])
            ? PricingApprovalDefaults.TemplateCode
            : configuration["Commercial:Pricing:Workflow:TemplateCode"]!.Trim();
        ReconcileAfterSeconds = int.TryParse(configuration["Commercial:Pricing:Workflow:ReconcileAfterSeconds"], out var seconds) && seconds > 0
            ? seconds
            : PricingApprovalDefaults.ReconcileAfterSeconds;
    }

    public string TemplateCode { get; }
    public int ReconcileAfterSeconds { get; }
}
