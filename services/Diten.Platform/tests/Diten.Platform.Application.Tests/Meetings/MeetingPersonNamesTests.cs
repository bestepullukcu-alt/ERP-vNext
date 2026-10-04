using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>ATT-FIX1 — the one name-resolution step every Meetings read uses.</summary>
public sealed class MeetingPersonNamesTests
{
    [Fact]
    public async Task An_empty_id_is_never_asked_for_and_nothing_to_ask_asks_nothing()
    {
        var names = new CountingNames();
        var someone = Guid.NewGuid();

        await MeetingPersonNames.ResolveAsync(names, [Guid.Empty], CancellationToken.None);
        Assert.Empty(names.Asked);

        await MeetingPersonNames.ResolveAsync(names, [Guid.Empty, someone, someone], CancellationToken.None);
        Assert.Equal([someone], Assert.Single(names.Asked));
    }

    [Fact]
    public async Task A_resolver_that_hangs_and_ignores_its_token_still_costs_at_most_the_timeout()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var resolved = await MeetingPersonNames.ResolveAsync(new HangingNames(), [Guid.NewGuid()], CancellationToken.None);
        clock.Stop();

        Assert.Empty(resolved);
        Assert.True(clock.Elapsed < MeetingPersonNames.Timeout + TimeSpan.FromSeconds(1), $"took {clock.Elapsed}");
    }

    private sealed class CountingNames : IUserDisplayNameResolver
    {
        public List<IReadOnlyCollection<Guid>> Asked { get; } = [];

        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        {
            Asked.Add(userIds.ToList());
            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(userIds.ToDictionary(id => id, _ => "Ad"));
        }
    }

    private sealed class HangingNames : IUserDisplayNameResolver
    {
        public async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), CancellationToken.None);   // ignores its token on purpose
            return new Dictionary<Guid, string>();
        }
    }
}
