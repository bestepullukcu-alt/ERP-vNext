namespace Diten.BuildingBlocks.BackgroundJobs;

public sealed class BackgroundJobSchedulerOptions
{
    public const string SectionName = "BackgroundJobs";

    public bool Enabled { get; set; }

    public string ServiceName { get; set; } = "Diten.Platform";

    public bool DashboardEnabled { get; set; }

    public string DashboardPath { get; set; } = "/hangfire";

    public bool DashboardAllowAnonymousInDevelopment { get; set; }

    public string StorageDatabaseName { get; set; } = "diten_background_jobs";

    public int DefaultRetryAttempts { get; set; } = 5;

    public bool RegisterStandardJobs { get; set; } = true;

    public Dictionary<string, bool> EnabledJobs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
