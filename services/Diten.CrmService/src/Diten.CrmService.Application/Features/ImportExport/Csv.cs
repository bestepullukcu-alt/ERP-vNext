using System.Text;

namespace Diten.CrmService.Application.Features.ImportExport;

/// <summary>Minimal RFC-4180 CSV writer (dependency-free). Values are quoted when they contain comma/quote/newline.</summary>
public static class Csv
{
    public static string Field(object? value)
    {
        var s = value?.ToString() ?? string.Empty;
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
        {
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        return s;
    }

    public static string Row(params object?[] fields)
        => string.Join(",", fields.Select(Field));

    public static string Build(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", header.Select(h => (object?)h).Select(Field))).Append('\n');
        foreach (var row in rows)
        {
            sb.Append(string.Join(",", row.Select(Field))).Append('\n');
        }

        return sb.ToString();
    }
}
