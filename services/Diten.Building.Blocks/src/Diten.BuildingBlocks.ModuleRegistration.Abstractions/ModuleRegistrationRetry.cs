namespace Diten.BuildingBlocks.ModuleRegistration.Abstractions;

/// <summary>
/// Tiny retry policy for the startup self-registration push. Pure (delay is injected) so it is unit-testable without
/// real waits. Runs <paramref name="attempt"/> up to <paramref name="maxAttempts"/> times; an attempt returns
/// <c>true</c> to STOP (success or a non-retryable failure) and <c>false</c> to RETRY (transient: connection refused / 5xx).
/// </summary>
public static class ModuleRegistrationRetry
{
    /// <returns><c>true</c> if some attempt stopped the loop; <c>false</c> if all attempts were exhausted.</returns>
    public static async Task<bool> RunAsync(
        Func<int, CancellationToken, Task<bool>> attempt,
        int maxAttempts,
        Func<int, TimeSpan> backoff,
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken ct)
    {
        for (var attemptNumber = 1; attemptNumber <= maxAttempts; attemptNumber++)
        {
            ct.ThrowIfCancellationRequested();

            if (await attempt(attemptNumber, ct))
            {
                return true;
            }

            if (attemptNumber < maxAttempts)
            {
                await delay(backoff(attemptNumber), ct);
            }
        }

        return false;
    }
}
