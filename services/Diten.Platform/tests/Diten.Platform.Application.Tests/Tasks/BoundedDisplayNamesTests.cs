using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>ATT-FIX2 — the one bounded way every read and write asks for names.</summary>
public sealed class BoundedDisplayNamesTests
{
    [Fact]
    public async Task The_resolver_is_handed_a_token_that_the_bound_cancels()
    {
        var resolver = new TokenWatchingNames();

        var names = await BoundedDisplayNames.ResolveAsync(resolver, [Guid.NewGuid()], CancellationToken.None);

        Assert.Empty(names);
        // The bound answers at its timeout without waiting for the resolver (names are decoration on an answer that must
        // still come); the resolver sees the cancellation on its own continuation, which a loaded runner can schedule
        // after the answer (main CI 2026-11-05, 3870486d6: the flag was read before it was set). So wait for it —
        // bounded: a resolver whose token is never cancelled still fails here, after 10 s.
        var seen = await Task.WhenAny(resolver.Cancelled, Task.Delay(TimeSpan.FromSeconds(10))) == resolver.Cancelled;
        Assert.True(seen && resolver.TokenWasCancelled,
            "the resolver's token was never cancelled: an HTTP call would run its full 100 s");
    }

    [Fact]
    public async Task A_checked_call_that_times_out_is_incomplete_never_complete_with_gaps()
    {
        var result = await BoundedDisplayNames.ResolveCheckedAsync(new TokenWatchingNames(), [Guid.NewGuid()], CancellationToken.None);

        Assert.False(result.Complete);
        Assert.Empty(result.Names);
    }

    [Fact]
    public async Task Nothing_to_ask_asks_nothing_and_is_complete()
    {
        var resolver = new TokenWatchingNames();

        var result = await BoundedDisplayNames.ResolveCheckedAsync(resolver, [Guid.Empty], CancellationToken.None);

        Assert.True(result.Complete);
        Assert.False(resolver.Asked);
    }

    /// <summary>Honours its token: waits until it is cancelled (or five minutes).</summary>
    private sealed class TokenWatchingNames : IUserDisplayNameResolver, IUserDisplayNameChecker
    {
        private readonly TaskCompletionSource cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool TokenWasCancelled { get; private set; }
        public bool Asked { get; private set; }

        /// <summary>Completes when the resolver has seen its token cancelled.</summary>
        public Task Cancelled => cancelled.Task;

        public async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        {
            await WaitAsync(ct);
            return new Dictionary<Guid, string>();
        }

        public async Task<DisplayNameResolution> ResolveCheckedAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        {
            await WaitAsync(ct);
            return new DisplayNameResolution(new Dictionary<Guid, string>(), true);
        }

        private async Task WaitAsync(CancellationToken ct)
        {
            Asked = true;
            try { await Task.Delay(TimeSpan.FromMinutes(5), ct); }
            catch (OperationCanceledException) { TokenWasCancelled = true; cancelled.TrySetResult(); throw; }
        }
    }
}
