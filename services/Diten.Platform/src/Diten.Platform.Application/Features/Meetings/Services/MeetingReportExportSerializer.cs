using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Diten.Platform.Application.Features.Meetings.Services;

/// <summary>
/// S12 (pack §23.7) — the meeting report's three grains as files. <c>WorkReportExportSerializer</c> is the
/// pattern (BOM, invariant-culture numbers, English column headers, formula-injection guard on text) and is
/// followed wherever it applies; the one addition this feature's own owner decision requires
/// (pack §23.13/5) is the disclaimer line at the top of every file.
/// </summary>
public static class MeetingReportExportSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly (string Header, Func<Application.Features.Meetings.MeetingReportMeetingRowDto, object?> Value)[] MeetingColumns =
    [
        ("Id", row => row.Id),
        ("Title", row => row.Title),
        ("MeetingTypeId", row => row.MeetingTypeId),
        ("MeetingTypeName", row => row.MeetingTypeName),
        ("StartAt", row => row.StartAt),
        ("OrganizerUserId", row => row.OrganizerUserId),
        ("AttendeeCount", row => row.AttendeeCount),
        ("RespondedCount", row => row.RespondedCount),
        ("AttendanceRatePercent", row => row.AttendanceRatePercent)
    ];

    private static readonly (string Header, Func<Application.Features.Meetings.MeetingReportDecisionRowDto, object?> Value)[] DecisionColumns =
    [
        ("MeetingId", row => row.MeetingId),
        ("MeetingTitle", row => row.MeetingTitle),
        ("Code", row => row.Code),
        ("Text", row => row.Text),
        ("DecidedByUserId", row => row.DecidedByUserId)
    ];

    private static readonly (string Header, Func<Application.Features.Meetings.MeetingReportActionRowDto, object?> Value)[] ActionColumns =
    [
        ("TaskId", row => row.TaskId),
        ("Title", row => row.Title),
        ("Lifecycle", row => row.Lifecycle),
        ("DueAt", row => row.DueAt),
        ("IsOverdue", row => row.IsOverdue),
        ("AssigneeUserId", row => row.AssigneeUserId),
        ("OriginMeetingId", row => row.OriginMeetingId),
        ("OriginMeetingTitle", row => row.OriginMeetingTitle),
        ("CurrentMeetingId", row => row.CurrentMeetingId),
        ("CurrentMeetingTitle", row => row.CurrentMeetingTitle)
    ];

    public static byte[] ToCsv(IReadOnlyList<Application.Features.Meetings.MeetingReportMeetingRowDto> rows, string disclaimer) =>
        Render(disclaimer, MeetingColumns, rows);

    public static byte[] ToCsv(IReadOnlyList<Application.Features.Meetings.MeetingReportDecisionRowDto> rows, string disclaimer) =>
        Render(disclaimer, DecisionColumns, rows);

    public static byte[] ToCsv(IReadOnlyList<Application.Features.Meetings.MeetingReportActionRowDto> rows, string disclaimer) =>
        Render(disclaimer, ActionColumns, rows);

    /// <summary>
    /// The disclaimer travels as a top-level property, not a row — a JSON array of objects has no header line
    /// to prepend a sentence in front of, and a fake extra element would corrupt every consumer that reads the
    /// array as one shape.
    /// </summary>
    public static byte[] ToJson<T>(IReadOnlyList<T> rows, string disclaimer) =>
        JsonSerializer.SerializeToUtf8Bytes(new { disclaimer, rows }, JsonOptions);

    private static byte[] Render<T>(
        string disclaimer, (string Header, Func<T, object?> Value)[] columns, IReadOnlyList<T> rows)
    {
        var builder = new StringBuilder();
        builder.Append(EscapeCsv(disclaimer)).Append("\r\n\r\n");
        builder.Append(string.Join(',', columns.Select(c => c.Header))).Append("\r\n");

        foreach (var row in rows)
        {
            builder.Append(string.Join(',', columns.Select(c => EscapeCsv(c.Value(row))))).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
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

        // Formula injection guard — the audit serializer's own rule, kept: a title or decision text is typed
        // by a person, and one that begins with one of these becomes a formula the moment a spreadsheet opens
        // the file.
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
