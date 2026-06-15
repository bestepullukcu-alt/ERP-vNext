using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

/// <summary>
/// Reads an .xlsx workbook (Office Open XML) without any third-party dependency
/// by treating the file as a zip archive (System.IO.Compression) and parsing the
/// worksheet/shared-strings XML. The first row is the header; columns are matched
/// by name, mirroring the CSV parser (value_code, display_name, description,
/// parent_value_code, replacement_value_code, is_deprecated, sort_order,
/// attributes_json).
/// </summary>
public sealed class XlsxBusinessReferenceDataImportParser : IBusinessReferenceDataImportParser
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public string ParserKey => "xlsx-v1";

    public bool Supports(string format, string fileName)
    {
        var normalized = format.Trim().ToLowerInvariant();
        return normalized is "xlsx" or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            || fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<BusinessReferenceDataImportParsedRow>> ParseAsync(byte[] content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var stream = new MemoryStream(content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var sharedStrings = ReadSharedStrings(archive);
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? archive.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
                && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        if (sheetEntry is null)
        {
            throw new InvalidOperationException("invalid_import_payload_xlsx_sheet_missing");
        }

        XDocument sheet;
        using (var sheetStream = sheetEntry.Open())
        {
            sheet = XDocument.Load(sheetStream);
        }

        var rows = sheet.Root?.Element(Main + "sheetData")?.Elements(Main + "row").ToList()
            ?? new List<XElement>();

        var result = new List<BusinessReferenceDataImportParsedRow>();
        Dictionary<string, int>? headerMap = null;
        var dataRowNumber = 0;

        foreach (var row in rows)
        {
            var cells = new Dictionary<int, string>();
            foreach (var c in row.Elements(Main + "c"))
            {
                var reference = (string?)c.Attribute("r") ?? string.Empty;
                cells[ColumnIndex(reference)] = ReadCellValue(c, sharedStrings);
            }

            if (headerMap is null)
            {
                headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in cells)
                {
                    var name = kv.Value.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(name) && !headerMap.ContainsKey(name))
                    {
                        headerMap[name] = kv.Key;
                    }
                }

                continue;
            }

            if (cells.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string? Get(string key) => headerMap.TryGetValue(key, out var idx) && cells.TryGetValue(idx, out var val) ? val : null;

            dataRowNumber++;
            var attributes = ParseAttributes(Get("attributes_json"));
            var isDeprecated = bool.TryParse(Get("is_deprecated"), out var dep) && dep;
            var sortOrder = int.TryParse(Get("sort_order"), out var sort) ? sort : 0;

            result.Add(new BusinessReferenceDataImportParsedRow(
                dataRowNumber,
                Get("value_code"),
                Get("display_name"),
                Get("description"),
                Get("parent_value_code"),
                Get("replacement_value_code"),
                isDeprecated,
                sortOrder,
                attributes));
        }

        return Task.FromResult<IReadOnlyList<BusinessReferenceDataImportParsedRow>>(result);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return Array.Empty<string>();
        }

        XDocument doc;
        using (var s = entry.Open())
        {
            doc = XDocument.Load(s);
        }

        var list = new List<string>();
        foreach (var si in doc.Root?.Elements(Main + "si") ?? Enumerable.Empty<XElement>())
        {
            // <si> is either a single <t> or several <r><t> runs.
            list.Add(string.Concat(si.Descendants(Main + "t").Select(t => t.Value)));
        }

        return list;
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
        {
            return string.Concat(cell.Element(Main + "is")?.Descendants(Main + "t").Select(t => t.Value)
                ?? Enumerable.Empty<string>());
        }

        var v = cell.Element(Main + "v")?.Value;
        if (string.IsNullOrEmpty(v))
        {
            return string.Empty;
        }

        if (type == "s" && int.TryParse(v, out var idx) && idx >= 0 && idx < sharedStrings.Count)
        {
            return sharedStrings[idx];
        }

        if (type == "b")
        {
            return v == "1" ? "true" : "false";
        }

        return v;
    }

    private static int ColumnIndex(string cellReference)
    {
        var index = 0;
        foreach (var ch in cellReference)
        {
            if (ch is >= 'A' and <= 'Z')
            {
                index = (index * 26) + (ch - 'A' + 1);
            }
            else if (ch is >= 'a' and <= 'z')
            {
                index = (index * 26) + (ch - 'a' + 1);
            }
            else
            {
                break;
            }
        }

        return index - 1;
    }

    private static Dictionary<string, string>? ParseAttributes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in document.RootElement.EnumerateObject())
            {
                result[p.Name] = p.Value.ValueKind == JsonValueKind.String
                    ? p.Value.GetString() ?? string.Empty
                    : p.Value.ToString();
            }

            return result;
        }
        catch
        {
            return null;
        }
    }
}
