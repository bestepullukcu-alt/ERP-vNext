using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleRegistration;

// SELF-REG Faz 1 FIX B — startup push retry policy (used by DevEnablement ModuleRegistrationHostedService).
public sealed class ModuleRegistrationRetryTests
{
    [Fact]
    public async Task Retries_until_an_attempt_succeeds_then_stops()
    {
        var calls = 0;
        var delays = 0;

        var stopped = await ModuleRegistrationRetry.RunAsync(
            attempt: (attemptNumber, _) => { calls++; return Task.FromResult(attemptNumber >= 2); }, // fail #1, succeed #2
            maxAttempts: 5,
            backoff: attemptNumber => TimeSpan.FromSeconds(attemptNumber),
            delay: (_, _) => { delays++; return Task.CompletedTask; },
            CancellationToken.None);

        Assert.True(stopped);
        Assert.Equal(2, calls);  // stops right after success — no third attempt
        Assert.Equal(1, delays); // one wait, between attempt 1 and 2
    }

    [Fact]
    public async Task Returns_false_after_exhausting_all_attempts()
    {
        var calls = 0;

        var stopped = await ModuleRegistrationRetry.RunAsync(
            attempt: (_, _) => { calls++; return Task.FromResult(false); }, // always transient → retry
            maxAttempts: 3,
            backoff: _ => TimeSpan.Zero,
            delay: (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.False(stopped);
        Assert.Equal(3, calls);
    }
}
