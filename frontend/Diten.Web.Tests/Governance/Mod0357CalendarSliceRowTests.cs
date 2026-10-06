using Xunit;

namespace Diten.Web.Tests.Governance;

/// <summary>
/// WP-UI-CALENDAR-VIEW-01 (F) — MOD-0357's S3b row says what was actually decided and built: a SHARED calendar
/// component (month/week/day) with drag-to-plan in the Task Center and meetings read-only, the Meetings page and
/// the invitation cards in 2c. The old row promised a read-only month grid reusing <c>renderCalendar</c>, which no
/// longer exists; a pack that still said so would send the next slice after code that was deleted.
/// </summary>
public sealed class Mod0357CalendarSliceRowTests
{
    private static string PackRow()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "execution", "domains")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var pack = Path.Combine(dir!.FullName, "execution", "domains", "management-governance", "module-packs",
            "MOD-0357-management-review-cadence.md");
        var row = File.ReadAllLines(pack).SingleOrDefault(line => line.StartsWith("| S3b |", StringComparison.Ordinal));
        Assert.False(row is null, "MOD-0357 has no S3b row");
        return row!;
    }

    [Fact]
    public void The_S3b_row_names_the_shared_component_and_its_three_views()
    {
        var row = PackRow();

        Assert.Contains("Ortak takvim bileşeni (ay/hafta/gün)", row, StringComparison.Ordinal);
        Assert.Contains("Görev Merkezi sürükle-planla", row, StringComparison.Ordinal);
        Assert.Contains("toplantılar salt okunur", row, StringComparison.Ordinal);
        Assert.Contains("davet kartları 2c", row, StringComparison.Ordinal);
        Assert.Contains("shared/diten-calendar.js", row, StringComparison.Ordinal);
    }

    [Fact]
    public void The_S3b_row_no_longer_promises_the_deleted_month_grid()
    {
        var row = PackRow();

        Assert.DoesNotContain("reuses the Task Center's `renderCalendar` once extracted", row, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"^\| S3b \| Read-only month calendar", row);
    }
}
