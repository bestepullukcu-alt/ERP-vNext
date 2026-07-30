namespace Diten.Platform.Infrastructure.Eventing;

internal static class PpmAuditRetryPolicy
{
    internal const int RetryCount = 4;
    internal static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan MaximumDelay = TimeSpan.FromMinutes(5);
    internal const double MaximumJitterRatio = 0.20d;

    internal static TimeSpan[] CreateDelays(Func<double>? nextJitter = null)
    {
        nextJitter ??= Random.Shared.NextDouble;
        var delays = new TimeSpan[RetryCount];
        delays[0] = FirstDelay;
        for (var retry = 1; retry < RetryCount; retry++)
        {
            var exponentialSeconds = FirstDelay.TotalSeconds * Math.Pow(2, retry);
            var jitter = Math.Clamp(nextJitter(), 0d, 1d)
                         * exponentialSeconds
                         * MaximumJitterRatio;
            delays[retry] = TimeSpan.FromSeconds(
                Math.Min(exponentialSeconds + jitter, MaximumDelay.TotalSeconds));
        }

        return delays;
    }
}
