using System.Text;
using System.Text.Json;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public sealed class CsvBusinessReferenceDataImportParser : IBusinessReferenceDataImportParser
{
    public string ParserKey => "csv-v1";

    public bool Supports(string format, string fileName)
    {
        var normalized = format.Trim().ToLowerInvariant();
        return normalized is "csv" or "text/csv" || fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<BusinessReferenceDataImportParsedRow>> ParseAsync(byte[] content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Strip a UTF-8 BOM if present (Excel-saved CSV files include one).
        var fileContent = (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            ? Encoding.UTF8.GetString(content, 3, content.Length - 3)
            : Encoding.UTF8.GetString(content);

        var lines = fileContent
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<BusinessReferenceDataImportParsedRow>>([]);
        }

        var header = ParseCsvLine(lines[0]);
        var headerMap = header
            .Select((name, index) => new { Name = name.Trim().ToLowerInvariant(), Index = index })
            .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);

        var rows = new List<BusinessReferenceDataImportParsedRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            var cols = ParseCsvLine(lines[i]);
            string? Get(string key) => headerMap.TryGetValue(key, out var idx) && idx < cols.Count ? cols[idx] : null;

            var attributes = ParseAttributes(Get("attributes_json"));
            var isDeprecated = bool.TryParse(Get("is_deprecated"), out var dep) && dep;
            var sortOrder = int.TryParse(Get("sort_order"), out var sort) ? sort : 0;

            rows.Add(new BusinessReferenceDataImportParsedRow(
                i,
                Get("value_code"),
                Get("display_name"),
                Get("description"),
                Get("parent_value_code"),
                Get("replacement_value_code"),
                isDeprecated,
                sortOrder,
                attributes));
        }

        return Task.FromResult<IReadOnlyList<BusinessReferenceDataImportParsedRow>>(rows);
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

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
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
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString().Trim());
        return result;
    }
}
