namespace Diten.MdmService.Api.Configuration;

public sealed class GlobalProductRetirementRequestWorkflowWorkerOptions
{
    public const string SectionName = "GlobalProductRetirementRequestWorkflowWorker";
    public bool Enabled { get; set; }
    public int LeaseSeconds { get; set; } = 60;
    public int RetryDelaySeconds { get; set; } = 10;
    public int PollIntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 50;
    // BL-552 (the GSKU FIX3 settings): retries in a row before a cause that is not a service fault is handed to a
    // person (0 = never), the ceiling of the doubling retry delay, and the start's settle window.
    public int QuarantineAfterFailedAttempts { get; set; } = 30;
    public int MaxRetryDelaySeconds { get; set; } = 900;
    public int StartSettleSeconds { get; set; } = 120;
    // A ceiling below the retry delay is raised to the delay — never a failure of every cycle.
    public int EffectiveMaxRetryDelaySeconds => Math.Max(MaxRetryDelaySeconds, RetryDelaySeconds);
    public bool IsValid() => LeaseSeconds is >= 10 and <= 900 && RetryDelaySeconds is >= 1 and <= 3600
        && PollIntervalSeconds is >= 1 and <= 3600 && BatchSize is >= 1 and <= 100
        && QuarantineAfterFailedAttempts is >= 0 and <= 1000
        && MaxRetryDelaySeconds is >= 1 and <= 86_400
        && StartSettleSeconds is >= 1 and <= 3600;
}
