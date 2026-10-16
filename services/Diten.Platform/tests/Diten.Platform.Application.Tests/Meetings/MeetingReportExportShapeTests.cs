using System.Text;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Services;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// BL-531 — the report SCREEN's rows now carry the organizer's name; the EXPORT is built from the same row type
/// without names and must come out exactly as before: no new JSON property, no new CSV column.
/// </summary>
public sealed class MeetingReportExportShapeTests
{
    private static readonly MeetingReportMeetingRowDto Row = new(
        Guid.NewGuid(), "Kalite", Guid.NewGuid(), "Tip", DateTimeOffset.UtcNow, Guid.NewGuid(), 2, 1, 50);

    [Fact]
    public void The_json_export_has_no_organizer_name_property()
    {
        var json = Encoding.UTF8.GetString(MeetingReportExportSerializer.ToJson([Row], "d"));

        Assert.DoesNotContain("organizerDisplayName", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("organizerUserId", json, StringComparison.Ordinal);
    }

    [Fact]
    public void The_csv_export_keeps_its_own_columns()
    {
        var csv = Encoding.UTF8.GetString(MeetingReportExportSerializer.ToCsv([Row with { OrganizerDisplayName = "Ad" }], "d"));

        Assert.DoesNotContain("OrganizerDisplayName", csv, StringComparison.Ordinal);
        Assert.DoesNotContain(",Ad,", csv, StringComparison.Ordinal);
    }
}
