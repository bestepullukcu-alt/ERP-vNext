using Diten.Platform.Domain.Entities.Notifications;

namespace Diten.Platform.Domain.Repositories;

public enum InternalDispatchKeyOutcome
{
    /// <summary>This call holds the key: it dispatches, then completes or releases it.</summary>
    Claimed,

    /// <summary>An earlier call finished: its answer is <see cref="InternalDispatchKey.ResultJson"/>.</summary>
    Done,

    /// <summary>An earlier call holds the key and has not finished yet.</summary>
    InFlight
}

public sealed record InternalDispatchKeyClaim(InternalDispatchKeyOutcome Outcome, InternalDispatchKey Record);

/// <summary>
/// WP-VW-W5 W5-FIX2 (F-1) — the idempotency keys of the internal event dispatch, in the CURRENT tenant (the dispatch
/// pins it from the body before it asks). The unique (tenant, source module, key) index is the rule: of two concurrent
/// calls exactly one inserts, the other reads.
/// </summary>
public interface IInternalDispatchKeyRepository
{
    /// <summary>Claims <paramref name="key"/> for <paramref name="sourceModule"/>: a new row; or a released one, or one left
    /// pending longer than <paramref name="staleAfter"/>, taken over under a new token. Otherwise says what holds it.</summary>
    Task<InternalDispatchKeyClaim> ClaimAsync(
        string sourceModule, string key, DateTimeOffset now, TimeSpan staleAfter, CancellationToken ct = default);

    Task<InternalDispatchKey?> GetAsync(string sourceModule, string key, CancellationToken ct = default);

    /// <summary>Stores the answer; only the claim that holds the row (<paramref name="claimToken"/>) can.</summary>
    Task<bool> CompleteAsync(Guid id, Guid claimToken, string resultJson, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Gives the key back (nothing was accepted); only the claim that holds the row can.</summary>
    Task<bool> ReleaseAsync(Guid id, Guid claimToken, DateTimeOffset now, CancellationToken ct = default);
}
