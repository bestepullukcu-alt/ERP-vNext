namespace Diten.Platform.Infrastructure.Services.Audit;

public sealed class AuditOutboxWorkerOptions
{
    public int BatchSize { get; init; } = 25;
    public int MaxAttempts { get; init; } = 5;
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMinutes(15);
    public int MaxLastErrorLength { get; init; } = 500;

    /// <summary>
    /// A message in <c>Processing</c> state older than this threshold is considered
    /// stuck (e.g., worker pod crashed mid-processing) and is eligible for re-claim
    /// by the next worker iteration. Without this guard, stuck Processing messages
    /// would block the corresponding audit event indefinitely.
    /// </summary>
    public TimeSpan ProcessingStaleAfter { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan GetRetryDelay(int failedAttemptCount)
    {
        if (failedAttemptCount <= 0)
        {
            return InitialRetryDelay;
        }

        var multiplier = Math.Pow(2, Math.Min(failedAttemptCount - 1, 8));
        var delay = TimeSpan.FromTicks((long)(InitialRetryDelay.Ticks * multiplier));
        return delay > MaxRetryDelay ? MaxRetryDelay : delay;
    }
}
