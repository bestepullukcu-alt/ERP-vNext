namespace Diten.Platform.Application.Features.WorkingCalendarImport;

public sealed class WorkingCalendarImportOptions
{
    public const string SectionName = "WorkingCalendar:HolidayProvider";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "offline-stub";
    public string BaseUrl { get; set; } = string.Empty;
    public List<string> AllowedHosts { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxResponseItems { get; set; } = 400;
    public HolidayAutoFetchScheduleOptions Schedule { get; set; } = new();
}

public sealed class HolidayAutoFetchScheduleOptions
{
    public bool Enabled { get; set; }
    public string CronExpression { get; set; } = "0 1 * * *";
    public List<int> YearOffsets { get; set; } = new() { 0, 1 };
    public int MaxTargetsPerRun { get; set; } = 100;
    public bool IncludeNonPublicTypes { get; set; }
}

public interface IHolidayProvider
{
    string ProviderKey { get; }
    Task<HolidayFetchResult> FetchAsync(string countryCode, int year, CancellationToken ct = default);
}

public sealed record HolidayFetchResult(
    string Outcome,
    IReadOnlyList<ProviderHoliday> Holidays,
    string ProviderKey,
    string Endpoint,
    DateTimeOffset FetchedAt,
    string PayloadHash,
    string? FailureReason = null);

public sealed record ProviderHoliday(
    DateOnly Date,
    string Name,
    string? LocalName,
    IReadOnlyList<string> Types,
    bool IsNationwide,
    IReadOnlyList<string>? Subdivisions,
    string ProviderRef);

public static class HolidayProviderOutcome
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}

public static class WorkingCalendarImportPermissionKeys
{
    public const string Read = "platform.working-calendar.auto-fetch.read";
    public const string Run = "platform.working-calendar.auto-fetch.run";
    public const string Review = "platform.working-calendar.auto-fetch.review";
    public const string Apply = "platform.working-calendar.auto-fetch.apply";
}

public static class WorkingCalendarImportActors
{
    public const string Scheduler = "system:auto-fetch-scheduler";
    public static bool IsSystem(string? actor) => actor?.StartsWith("system:", StringComparison.OrdinalIgnoreCase) == true;
}
