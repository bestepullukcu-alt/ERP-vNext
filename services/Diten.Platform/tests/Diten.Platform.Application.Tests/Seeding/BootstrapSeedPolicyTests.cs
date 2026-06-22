using Diten.Platform.Infrastructure.Persistence.Configurations;
using Xunit;

namespace Diten.Platform.Application.Tests.Seeding;

// UI #2c — one-time bootstrap seed marker decision rule (Domain + Service share it).
public sealed class BootstrapSeedPolicyTests
{
    [Theory]
    [InlineData(true, false, SeedDecision.Skip)]          // marker present → never re-seed (even if now empty)
    [InlineData(true, true, SeedDecision.Skip)]           // marker present → never re-seed
    [InlineData(false, false, SeedDecision.SeedAndMark)]  // no marker + empty → fresh install: seed then mark
    [InlineData(false, true, SeedDecision.MarkOnly)]      // no marker + curated data → preserve, just mark
    public void Decide_covers_three_bootstrap_states(bool markerExists, bool hasLiveRecords, SeedDecision expected)
    {
        Assert.Equal(expected, BootstrapSeedPolicy.Decide(markerExists, hasLiveRecords));
    }
}
