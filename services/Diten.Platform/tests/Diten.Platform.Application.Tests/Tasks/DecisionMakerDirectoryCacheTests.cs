using Diten.Platform.Application.Features.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>BL-512 FIX1 — the decision-makers directory cache: per tenant, short-lived, bounded.</summary>
public sealed class DecisionMakerDirectoryCacheTests
{
    private static readonly IReadOnlyList<AssignablePersonDto> Rows =
        [new AssignablePersonDto(Guid.NewGuid(), "Ayşe", Guid.NewGuid(), "P", "Pozisyon", Guid.NewGuid(), "U", "Birim", Guid.NewGuid())];

    [Fact]
    public void One_tenants_directory_is_never_served_to_another()
    {
        var cache = new DecisionMakerDirectoryCache(new ManualClock());
        var a = Guid.NewGuid();
        cache.Set(a, Rows);

        Assert.True(cache.TryGet(a, out var mine));
        Assert.Same(Rows, mine);
        Assert.False(cache.TryGet(Guid.NewGuid(), out _));
    }

    [Fact]
    public void An_entry_lives_at_most_sixty_seconds()
    {
        var clock = new ManualClock();
        var cache = new DecisionMakerDirectoryCache(clock);
        var a = Guid.NewGuid();
        cache.Set(a, Rows);

        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.True(cache.TryGet(a, out _));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(cache.TryGet(a, out _));
    }

    [Fact]
    public void It_never_holds_more_than_its_bound_and_no_tenant_is_kept()
    {
        var clock = new ManualClock();
        var cache = new DecisionMakerDirectoryCache(clock);
        for (var i = 0; i < DecisionMakerDirectoryCache.MaximumTenants + 50; i++)
        {
            cache.Set(Guid.NewGuid(), Rows);
            clock.Advance(TimeSpan.FromMilliseconds(1));
        }

        Assert.Equal(DecisionMakerDirectoryCache.MaximumTenants, cache.Count);
        cache.Set(Guid.Empty, Rows);
        Assert.False(cache.TryGet(Guid.Empty, out _));
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
