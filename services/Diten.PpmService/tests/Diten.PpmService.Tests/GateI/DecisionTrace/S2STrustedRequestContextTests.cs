using Diten.PpmService.Application.GateI;
using Xunit;

namespace Diten.PpmService.Tests.GateI.DecisionTrace;

public sealed class S2STrustedRequestContextTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validate_rejects_expired_context()
    {
        var context = ValidContext(Now.AddMinutes(-2), Now.AddMinutes(-1));

        Assert.Throws<ArgumentException>(() => context.Validate(new FixedTimeProvider(Now)));
    }

    [Fact]
    public void Validate_rejects_context_that_is_not_yet_valid()
    {
        var context = ValidContext(Now.AddMinutes(1), Now.AddMinutes(2));

        Assert.Throws<ArgumentException>(() => context.Validate(new FixedTimeProvider(Now)));
    }

    [Fact]
    public void Validate_accepts_context_inside_freshness_window()
    {
        var context = ValidContext(Now.AddMinutes(-1), Now.AddMinutes(1));

        Assert.Same(context, context.Validate(new FixedTimeProvider(Now)));
    }

    private static S2STrustedRequestContext ValidContext(
        DateTimeOffset notBeforeUtc,
        DateTimeOffset expiresAtUtc) =>
        new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "ppm-client", "issuer", "audience", "service", "scope", "operation",
            ["ppm.investment-cases.update"], new string('a', 64), 1, 1, 1,
            notBeforeUtc.AddSeconds(-1), notBeforeUtc, expiresAtUtc);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
