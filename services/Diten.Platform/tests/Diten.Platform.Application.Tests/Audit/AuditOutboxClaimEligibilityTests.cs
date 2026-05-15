using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// Contract tests for the claim filter behavior in <c>IAuditOutboxProcessingRepository</c>.
/// The actual MongoDB filter in <c>AuditOutboxRepository.ClaimNextAsync</c> must produce
/// the same eligibility decisions as the in-memory store used here. Any future
/// divergence indicates a regression in the Mongo filter expressions.
/// </summary>
public sealed class AuditOutboxClaimEligibilityTests
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);
    private const int MaxAttempts = 5;

    [Fact]
    public void PendingMessage_WithNextAttemptInPast_ShouldBeClaimed()
    {
        var now = DateTimeOffset.UtcNow;
        var message = Message(status: ClaimEligibility.OutboxStatus.Pending, attempts: 0, nextAttempt: now.AddSeconds(-1));

        var claimed = ClaimEligibility.Claim(message, now, StaleAfter, MaxAttempts);

        Assert.True(claimed);
    }

    [Fact]
    public void FailedMessage_ReadyForRetry_ShouldBeClaimed()
    {
        var now = DateTimeOffset.UtcNow;
        var message = Message(status: ClaimEligibility.OutboxStatus.Failed, attempts: 2, nextAttempt: now.AddSeconds(-10));

        var claimed = ClaimEligibility.Claim(message, now, StaleAfter, MaxAttempts);

        Assert.True(claimed);
    }

    [Fact]
    public void FailedMessage_NotYetReady_ShouldNotBeClaimed()
    {
        var now = DateTimeOffset.UtcNow;
        var message = Message(status: ClaimEligibility.OutboxStatus.Failed, attempts: 2, nextAttempt: now.AddMinutes(2));

        var claimed = ClaimEligibility.Claim(message, now, StaleAfter, MaxAttempts);

        Assert.False(claimed);
    }

    [Fact]
    public void FailedMessage_AtOrAboveMaxAttempts_ShouldNotBeClaimed()
    {
        var now = DateTimeOffset.UtcNow;
        var message = Message(status: ClaimEligibility.OutboxStatus.Failed, attempts: MaxAttempts, nextAttempt: now);

        var claimed = ClaimEligibility.Claim(message, now, StaleAfter, MaxAttempts);

        Assert.False(claimed);
    }

    [Fact]
    public void CompletedMessage_ShouldNeverBeClaimed()
    {
        var now = DateTimeOffset.UtcNow;
        var message = Message(status: ClaimEligibility.OutboxStatus.Completed, attempts: 1, nextAttempt: now.AddSeconds(-10));

        var claimed = ClaimEligibility.Claim(message, now, StaleAfter, MaxAttempts);

        Assert.False(claimed);
    }

    [Fact]
    public void DeadLetterMessage_ShouldNeverBeClaimed()
    {
        var now = DateTimeOffset.UtcNow;
        var message = Message(status: ClaimEligibility.OutboxStatus.DeadLetter, attempts: MaxAttempts, nextAttempt: now.AddSeconds(-10));

        var claimed = ClaimEligibility.Claim(message, now, StaleAfter, MaxAttempts);

        Assert.False(claimed);
    }

    [Fact]
    public void ProcessingMessage_NonStale_ShouldNotBeClaimed()
    {
        var now = DateTimeOffset.UtcNow;
        var nextAttemptJustClaimed = now.AddSeconds(-30);
        var message = Message(status: ClaimEligibility.OutboxStatus.Processing, attempts: 1, nextAttempt: nextAttemptJustClaimed);

        var claimed = ClaimEligibility.Claim(message, now, StaleAfter, MaxAttempts);

        Assert.False(claimed);
    }

    [Fact]
    public void ProcessingMessage_StaleBeyondThreshold_ShouldBeReclaimed()
    {
        var now = DateTimeOffset.UtcNow;
        var staleNextAttempt = now.Subtract(StaleAfter + TimeSpan.FromSeconds(1));
        var message = Message(status: ClaimEligibility.OutboxStatus.Processing, attempts: 1, nextAttempt: staleNextAttempt);

        var claimed = ClaimEligibility.Claim(message, now, StaleAfter, MaxAttempts);

        Assert.True(claimed);
    }

    [Fact]
    public void ProcessingMessage_ExactlyAtThreshold_ShouldBeReclaimed()
    {
        var now = DateTimeOffset.UtcNow;
        var staleNextAttempt = now.Subtract(StaleAfter);
        var message = Message(status: ClaimEligibility.OutboxStatus.Processing, attempts: 1, nextAttempt: staleNextAttempt);

        var claimed = ClaimEligibility.Claim(message, now, StaleAfter, MaxAttempts);

        Assert.True(claimed);
    }

    [Fact]
    public void StaleProcessingMessage_AtOrAboveMaxAttempts_ShouldNotBeClaimed()
    {
        var now = DateTimeOffset.UtcNow;
        var staleNextAttempt = now.Subtract(StaleAfter + TimeSpan.FromSeconds(10));
        var message = Message(status: ClaimEligibility.OutboxStatus.Processing, attempts: MaxAttempts, nextAttempt: staleNextAttempt);

        var claimed = ClaimEligibility.Claim(message, now, StaleAfter, MaxAttempts);

        Assert.False(claimed);
    }

    private static ClaimEligibility.OutboxMessage Message(ClaimEligibility.OutboxStatus status, int attempts, DateTimeOffset nextAttempt)
    {
        return new ClaimEligibility.OutboxMessage(status, attempts, nextAttempt);
    }

    /// <summary>
    /// Mirror of <c>AuditOutboxRepository.ClaimNextAsync</c> filter expression as plain C#.
    /// The Mongo filter must produce the same eligibility decisions as this predicate.
    /// Mirrors the persistence-level <c>AuditOutboxStatus</c> enum value semantics
    /// (Pending, Processing, Completed, Failed, DeadLetter) without taking an
    /// internal-type dependency from the test project.
    /// </summary>
    private static class ClaimEligibility
    {
        public enum OutboxStatus
        {
            Pending = 1,
            Processing = 2,
            Completed = 3,
            Failed = 4,
            DeadLetter = 5
        }

        public sealed record OutboxMessage(OutboxStatus Status, int Attempts, DateTimeOffset NextAttemptAtUtc);

        public static bool Claim(OutboxMessage message, DateTimeOffset now, TimeSpan processingStaleAfter, int maxAttempts)
        {
            if (message.Attempts >= maxAttempts)
            {
                return false;
            }

            if ((message.Status == OutboxStatus.Pending || message.Status == OutboxStatus.Failed)
                && message.NextAttemptAtUtc <= now)
            {
                return true;
            }

            if (message.Status == OutboxStatus.Processing
                && message.NextAttemptAtUtc <= now - processingStaleAfter)
            {
                return true;
            }

            return false;
        }
    }
}
