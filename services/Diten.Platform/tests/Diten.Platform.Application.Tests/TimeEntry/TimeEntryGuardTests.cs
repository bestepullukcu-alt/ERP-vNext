using System.Reflection;
using System.Text.RegularExpressions;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 — the module's architectural promises, measured on the PRODUCTION sources and assembly:
/// <list type="bullet">
/// <item>T-01 (D1, ADR-004): outside <c>Adapters/</c>, nothing under <c>Features/TimeEntry</c> names a MOD-0024,
/// MOD-0357 or MOD-0288 repository, entity, feature namespace or collection. That is what keeps extraction a data move.</item>
/// <item>T-22 (Z-4, pack §8.7): no response DTO carries a ratio, a percentage or a score.</item>
/// <item>T-08 (D5): ISO weeks with week-year rules.</item>
/// </list>
/// </summary>
public sealed class TimeEntryGuardTests
{
    /// <summary>Anything that names another module's storage or model. Each entry is matched as a whole word.</summary>
    private static readonly string[] ForeignModuleTokens =
    [
        "Features.Tasks", "Features.Meetings", "Features.TenantOrganization", "Features.Organization",
        "Entities.Tasks", "Entities.Meetings", "Entities.Organization",
        "ITaskItemRepository", "ITaskTransitionRepository", "ITaskSeatDirectory",
        "IMeetingRepository", "IMeetingAttendeeRepository", "IMeetingMinutesVersionRepository",
        "IPositionRepository", "IPositionAssignmentRepository", "IOrganizationUnitRepository",
        "TaskItem", "Meeting", "Position", "PositionAssignment", "OrganizationUnit",
        "task_items", "task_transitions", "meeting_meetings", "meeting_attendees", "positions", "position_assignments",
        "organization_units"
    ];

    private static readonly Regex Foreign = new(
        @"(?<!\w)(" + string.Join("|", ForeignModuleTokens.Select(Regex.Escape)) + @")(?![\w])",
        RegexOptions.Compiled);

    [Fact]
    public void Outside_Adapters_the_module_names_no_other_modules_repository_entity_or_collection()
    {
        var hits = FeatureFiles()
            .Where(file => !IsAdapter(file))
            .SelectMany(file => Code(file).Split('\n')
                .Select((line, index) => (file, line, index))
                .Where(x => Foreign.IsMatch(x.line)))
            .Select(x => $"{Path.GetRelativePath(FeatureRoot(), x.file)}:{x.index + 1}: {x.line.Trim()}")
            .ToList();

        Assert.True(hits.Count == 0,
            "Features/TimeEntry reaches another module directly — go through ITimeEntryOrgGateway / ITimeEntryTaskGateway:\n"
            + string.Join("\n", hits));
    }

    [Fact]
    public void The_scan_is_not_vacuous_the_adapters_themselves_are_caught_by_it()
    {
        var adapters = FeatureFiles().Where(IsAdapter).ToList();

        Assert.NotEmpty(adapters);
        Assert.All(adapters, file => Assert.Matches(Foreign, Code(file)));
        Assert.True(FeatureFiles().Count(file => !IsAdapter(file)) > 30, "The scanned set looks wrong — did the folder move?");
    }

    [Theory]
    [InlineData("var t = new TaskItem();")]
    [InlineData("using Diten.Platform.Domain.Entities.Organization;")]
    [InlineData("IPositionRepository positions")]
    [InlineData("db.GetCollection<X>(\"task_items\")")]
    public void The_pattern_catches_each_shape_it_claims_to(string line) => Assert.Matches(Foreign, line);

    [Theory]
    [InlineData("TimesheetWeek week")]
    [InlineData("TimeEntryPosition position")]
    [InlineData("ITimeEntryTaskGateway tasks")]
    public void The_pattern_leaves_the_modules_own_names_alone(string line) => Assert.DoesNotMatch(Foreign, line);

    // ── T-22 ────────────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly string[] ScoreWords = ["Percent", "Ratio", "Rate", "Score", "Utilization", "Utilisation",
        "Efficiency", "Productivity", "Rank", "Achievement"];

    [Fact]
    public void No_response_DTO_field_is_or_hides_a_ratio_percentage_or_score()
    {
        var dtos = typeof(TimeEntryPermissions).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Diten.Platform.Application.Features.TimeEntry", StringComparison.Ordinal) == true
                        && t.Name.EndsWith("Dto", StringComparison.Ordinal))
            .ToList();
        Assert.True(dtos.Count >= 8, $"Expected the module's DTOs, found {dtos.Count}.");

        var offenders = dtos
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => (t, p)))
            .Where(x => Words(x.p.Name).Any(word => ScoreWords.Contains(word, StringComparer.OrdinalIgnoreCase))
                        || x.p.PropertyType == typeof(double) || x.p.PropertyType == typeof(decimal)
                        || x.p.PropertyType == typeof(float))
            .Select(x => $"{x.t.Name}.{x.p.Name}")
            .ToList();

        Assert.True(offenders.Count == 0, "Time entry exposes durations only, never a ratio or score: " + string.Join(", ", offenders));
    }

    /// <summary>PascalCase words — "DurationMinutes" is Duration + Minutes, not a hidden "ratio".</summary>
    private static IEnumerable<string> Words(string name) => Regex.Split(name, "(?<=[a-z0-9])(?=[A-Z])");

    [Fact]
    public void The_score_scan_is_not_vacuous()
    {
        Assert.Contains("Percent", Words("TargetPercent"));
        Assert.DoesNotContain("Ratio", Words("DurationMinutes"));
    }

    // ── T-08 — ISO week-year ────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2027-01-01", "2026-W53")]   // a Friday: week 53 of the PREVIOUS year
    [InlineData("2026-12-28", "2026-W53")]
    [InlineData("2024-12-30", "2025-W01")]   // a Monday: week 1 of the NEXT year
    [InlineData("2026-10-05", "2026-W41")]
    [InlineData("2026-10-25", "2026-W43")]   // the Berlin DST Sunday
    public void A_local_date_lands_in_its_ISO_week(string date, string weekKey)
        => Assert.Equal(weekKey, WeekCalendar.KeyOf(DateOnly.Parse(date)));

    [Fact]
    public void Week_keys_parse_to_their_Monday_and_impossible_weeks_do_not_parse()
    {
        Assert.True(WeekCalendar.TryParse("2026-W53", out var w53));
        Assert.Equal(new DateOnly(2026, 12, 28), w53);
        Assert.True(WeekCalendar.TryParse("2026-W01", out var w1));
        Assert.Equal(new DateOnly(2025, 12, 29), w1);
        Assert.False(WeekCalendar.TryParse("2027-W53", out _));
        Assert.False(WeekCalendar.TryParse("2026-W00", out _));
        Assert.False(WeekCalendar.TryParse("2026W40", out _));
        Assert.False(WeekCalendar.TryParse(null, out _));
    }

    [Fact]
    public void The_edit_window_is_this_week_and_the_four_before_it()
    {
        var today = new DateOnly(2026, 10, 7); // Wednesday of W41
        Assert.True(WeekCalendar.IsInsideEditWindow(new DateOnly(2026, 10, 5), today));
        Assert.True(WeekCalendar.IsInsideEditWindow(new DateOnly(2026, 9, 7), today));   // W37
        Assert.False(WeekCalendar.IsInsideEditWindow(new DateOnly(2026, 8, 31), today)); // W36
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static string FeatureRoot()
        => Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "TimeEntry");

    private static IReadOnlyList<string> FeatureFiles()
        => Directory.EnumerateFiles(FeatureRoot(), "*.cs", SearchOption.AllDirectories).ToList();

    private static bool IsAdapter(string file)
        => Path.GetRelativePath(FeatureRoot(), file).StartsWith("Adapters" + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>Comments may name other modules (they explain the boundary); only code counts.</summary>
    private static string Code(string file)
    {
        var source = File.ReadAllText(file);
        var noBlocks = Regex.Replace(source, @"/\*.*?\*/", m => new string('\n', m.Value.Count(c => c == '\n')), RegexOptions.Singleline);
        return Regex.Replace(noBlocks, @"(?m)^\s*///.*$|(?<![:""])//.*$", string.Empty);
    }
}
