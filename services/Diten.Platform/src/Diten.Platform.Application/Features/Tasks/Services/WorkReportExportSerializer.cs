using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// The work report as a file — Dilim 1e. The audit export's serializer (<c>AuditExportSerializer</c>) is the
/// pattern, and it is followed where it applies.
///
/// <para><b>⚠ THE COLUMN NAMES ARE NOT TRANSLATED, and that was measured rather than assumed:</b> the audit CSV
/// writes fixed English identifiers (<c>AuditExportSerializer.CsvHeaders</c>), so this does too. A header is a
/// key a spreadsheet formula refers to; one that changed with the reader's language would break every sheet
/// built on a colleague's download.</para>
///
/// <para><b>Two deliberate differences from the audit serializer, each for a stated reason:</b></para>
/// <list type="bullet">
/// <item><b>A UTF-8 byte-order mark.</b> A task title is typed by a person, in Turkish, Arabic or Chinese, and
/// Excel reads a CSV without a BOM as the machine's legacy code page — "Görev" arrives as mojibake. Audit
/// events carry almost no free text, so the audit file never showed the problem.</item>
/// <item><b>Numbers in the invariant culture.</b> <c>1.5</c> hours written under a Turkish thread culture is
/// <c>1,5</c>, which is a comma inside a comma-separated file.</item>
/// </list>
///
/// <para>Membership flags are written <c>1</c>/<c>0</c>, so summing a column is counting its cell.</para>
/// </summary>
public static class WorkReportExportSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <summary>
    /// Header → value, in one list, so a column can never be added to the header and forgotten in the row (or
    /// the reverse) — the two are the same entry.
    /// </summary>
    private static readonly (string Header, Func<WorkReportExportRow, object?> Value)[] Columns =
    [
        ("Id", row => row.Id),
        ("Title", row => row.Title),
        ("Lifecycle", row => row.Lifecycle),
        ("Priority", row => row.Priority),
        ("TaskTypeCode", row => row.TaskTypeCode),
        ("TaskTypeName", row => row.TaskTypeName),
        ("OrganizationUnitId", row => row.OrganizationUnitId),
        ("OrganizationUnitName", row => row.OrganizationUnitName),
        ("LegalEntityId", row => row.LegalEntityId),
        ("AssigneeUserId", row => row.AssigneeUserId),
        ("CreatedAt", row => row.CreatedAt),
        ("DueAt", row => row.DueAt),
        ("ClosedAt", row => row.ClosedAt),
        ("ClosureReasonCode", row => row.ClosureReasonCode),
        ("EstimateHours", row => row.EstimateHours),
        ("SpentHours", row => row.SpentHours),
        ("ReturnCount", row => row.ReturnCount),
        ("InPeriod", row => row.InPeriod),
        ("Opened", row => row.Opened),
        ("Closed", row => row.Closed),
        ("Completed", row => row.Completed),
        ("Cancelled", row => row.Cancelled),
        ("Unattended", row => row.Unattended),
        ("OnTime", row => row.OnTime),
        ("Late", row => row.Late),
        ("WithoutDueDate", row => row.WithoutDueDate),
        ("AgingUpTo7Days", row => row.AgingUpTo7Days),
        ("AgingFrom8To30Days", row => row.AgingFrom8To30Days),
        ("AgingOlderThan30Days", row => row.AgingOlderThan30Days),
        ("Returned", row => row.Returned)
    ];

    /// <summary>The header line's names, in order — exposed so a test reads the real list, not a copy.</summary>
    public static IReadOnlyList<string> CsvHeaders { get; } = Columns.Select(column => column.Header).ToList();

    public static byte[] ToCsv(IReadOnlyList<WorkReportExportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder();
        builder.Append(string.Join(',', CsvHeaders)).Append("\r\n");

        foreach (var row in rows)
        {
            builder.Append(string.Join(',', Columns.Select(column => EscapeCsv(column.Value(row))))).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    public static byte[] ToJson(IReadOnlyList<WorkReportExportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return JsonSerializer.SerializeToUtf8Bytes(rows, JsonOptions);
    }

    private static string EscapeCsv(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            bool flag => flag ? "1" : "0",
            DateTimeOffset at => at.ToString("O", CultureInfo.InvariantCulture),
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

        /*
         * ⚠ FORMULA INJECTION — the audit serializer's guard, kept. A title is typed by a person, and one that
         * begins with `=` becomes a formula the moment a spreadsheet opens the file. Applied to TEXT only: every
         * other value is written by this server, and prefixing a number would stop it from being one.
         */
        if (value is string && text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
        {
            text = "'" + text;
        }

        if (text.Contains('"', StringComparison.Ordinal)
            || text.Contains(',', StringComparison.Ordinal)
            || text.Contains('\n', StringComparison.Ordinal)
            || text.Contains('\r', StringComparison.Ordinal))
        {
            text = "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return text;
    }
}
