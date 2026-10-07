namespace Diten.MdmService.Api.Configuration;

public sealed class ProductIdentityWorkflowWorkerOptions
{
    public const string SectionName = "ProductIdentityWorkflowWorker";

    public bool Enabled { get; init; }
    public int PollIntervalSeconds { get; init; } = 15;
    public int TenantPageSize { get; init; } = 25;
    public int OperationPageSize { get; init; } = 25;
    public int LeaseSeconds { get; init; } = 60;
    public int RetryDelaySeconds { get; init; } = 30;
    public string LeaseOwner { get; init; } = string.Empty;
    // A record waiting on a fault of the service chain backs off up to this ceiling, and its waiting is logged as an
    // error from this many consecutive attempts on. Read by the worker AND by the interactive paths.
    public int ServiceFaultMaxRetryDelaySeconds { get; init; } = 900;
    public int ServiceFaultErrorAfterAttempts { get; init; } = 5;
    // BL-552: the start's settle window, and the worker retries in a row on an answer about the record before the
    // submit is handed to a person (0 = never).
    public int StartSettleSeconds { get; init; } = 120;
    public int QuarantineAfterFailedAttempts { get; init; } = 30;
    // BL-552 flow 3 FIX3 items 1 and 5: with the worker switched off it still records the stuck gauges — this often
    // (15–3600 s), not at the worker's own poll: a count is read-only, but it is read on every replica.
    public int StuckGaugeIntervalSeconds { get; init; } = 60;

    public void EnsureValidWhenEnabled()
    {
        if (!Enabled)
        {
            return;
        }

        if (PollIntervalSeconds is < 1 or > 3_600
            || TenantPageSize is < 1 or > 100
            || OperationPageSize is < 1 or > 100
            || LeaseSeconds is < 10 or > 900
            || RetryDelaySeconds is < 1 or > 3_600
            || !IsServiceFaultWindowValid(
                RetryDelaySeconds, ServiceFaultMaxRetryDelaySeconds, ServiceFaultErrorAfterAttempts)
            || !IsRecordAnswerWindowValid(StartSettleSeconds, QuarantineAfterFailedAttempts)
            || !IsStuckGaugeIntervalValid(StuckGaugeIntervalSeconds)
            || string.IsNullOrWhiteSpace(LeaseOwner)
            || LeaseOwner.Length > 128
            || !string.Equals(LeaseOwner, LeaseOwner.Trim(), StringComparison.Ordinal)
            || LeaseOwner.Any(char.IsControl))
        {
            throw new InvalidOperationException("PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID");
        }
    }

    // BL-552 flow 3 FIX1 item 3: the settle window and the record-answer limit are checked, never quietly clamped —
    // a window of 0 would trust "nothing arrived" at once, a negative limit would mean "never" (the bounds of the
    // correction and retirement workers).
    public static bool IsRecordAnswerWindowValid(int startSettleSeconds, int quarantineAfterFailedAttempts) =>
        StartSettleWindow.IsValid(startSettleSeconds)
        && quarantineAfterFailedAttempts is >= 0 and <= 1_000;

    // BL-552 flow 3 FIX3 EK: kept for the GP tests that name it; the rule itself (and why 25 min) is StartSettleWindow.
    public const int MaxStartSettleSeconds = StartSettleWindow.MaxSeconds;

    public static bool IsStuckGaugeIntervalValid(int seconds) => seconds is >= 15 and <= 3_600;

    // The ceiling may not be shorter than the ordinary retry delay: a "back-off" that is flat and quicker than the
    // normal poll is not one.
    public static bool IsServiceFaultWindowValid(int retryDelaySeconds, int maxRetryDelaySeconds, int errorAfterAttempts) =>
        maxRetryDelaySeconds is >= 1 and <= 86_400
        && errorAfterAttempts is >= 1 and <= 1_000
        && retryDelaySeconds <= maxRetryDelaySeconds;
}
