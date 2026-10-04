namespace Diten.MdmService.Api.Configuration;

public sealed class GskuCorrectionWorkflowWorkerOptions
{
    public const string SectionName = "GskuCorrectionWorkflowWorker";
    public bool Enabled { get; set; }
    public int LeaseSeconds { get; set; } = 60;
    public int RetryDelaySeconds { get; set; } = 10;
    public int PollIntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 50;
    // FIX3 item 1 (B13): retries in a row before an operation is handed to manual reconciliation (0 = never), and the
    // ceiling of the doubling retry delay.
    public int QuarantineAfterFailedAttempts { get; set; } = 30;
    public int MaxRetryDelaySeconds { get; set; } = 900;
    // FIX3 S8: how long after a start the engine's "nothing arrived" is not trusted; longer than the start's timeout.
    public int StartSettleSeconds { get; set; } = 120;
    public bool IsValid() => LeaseSeconds is >= 10 and <= 900 && RetryDelaySeconds is >= 1 and <= 3600
        && PollIntervalSeconds is >= 1 and <= 3600 && BatchSize is >= 1 and <= 100
        && QuarantineAfterFailedAttempts is >= 0 and <= 1000
        && MaxRetryDelaySeconds >= RetryDelaySeconds && MaxRetryDelaySeconds <= 86_400
        && StartSettleSeconds is >= 1 and <= 3600;
}
