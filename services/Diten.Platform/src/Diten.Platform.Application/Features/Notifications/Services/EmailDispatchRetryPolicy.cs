namespace Diten.Platform.Application.Features.Notifications.Services;

/// <summary>
/// When a failed e-mail dispatch is next tried. ONE rule for the two places a send can fail: the first, synchronous
/// attempt in QueueEmailNotificationHandler and every retry EmailDispatchJob makes. Until the S10B live pass
/// (2026-09-13) only the job wrote NextRetryAt, while EmailDispatchSweepJob only picks up rows that HAVE one — so a
/// mail whose first attempt failed was never tried again.
/// </summary>
public static class EmailDispatchRetryPolicy
{
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMinutes(1);
    private const int MaxRetryDelayMinutes = 60;

    /// <param name="attempt">The retry about to be scheduled: 1 for the first retry after the first failure.</param>
    public static DateTimeOffset NextRetryAt(int attempt, DateTimeOffset now)
    {
        var clampedAttempt = Math.Max(1, attempt);
        var exponentialMinutes = Math.Min(
            MaxRetryDelayMinutes, (int)BaseRetryDelay.TotalMinutes * (1 << Math.Min(clampedAttempt - 1, 6)));
        return now.AddMinutes(exponentialMinutes);
    }
}
