namespace Diten.MdmService.Api.Configuration;

public sealed class LskuRetirementRequestWorkflowWorkerOptions
{
    public const string SectionName = "LskuRetirementRequestWorkflowWorker";
    public bool Enabled { get; set; }
    public int LeaseSeconds { get; set; } = 60;
    public int RetryDelaySeconds { get; set; } = 10;
    public int PollIntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 50;
    // LSKU FIX1 B2 (the GSKU retirement request's options): retries in a row before a request is stuck, the back-off
    // ceiling, and how long after a start the engine's "nothing arrived" is not trusted.
    public int QuarantineAfterFailedAttempts { get; set; } = 30;
    public int MaxRetryDelaySeconds { get; set; } = 900;
    public int StartSettleSeconds { get; set; } = 120;
    public int EffectiveMaxRetryDelaySeconds => Math.Max(MaxRetryDelaySeconds, RetryDelaySeconds);
    public bool IsValid() => LeaseSeconds is >= 10 and <= 900 && RetryDelaySeconds is >= 1 and <= 3600
        && PollIntervalSeconds is >= 1 and <= 3600 && BatchSize is >= 1 and <= 100
        && QuarantineAfterFailedAttempts is >= 0 and <= 1000
        && MaxRetryDelaySeconds is >= 1 and <= 86_400
        && StartSettleSeconds is >= 1 and <= 3600;
}
