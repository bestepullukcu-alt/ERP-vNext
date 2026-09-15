namespace Diten.Platform.Application.Features.Meetings.Services;

/// <summary>
/// MOD-0357 S2 (pack §8 K11) — computes the SAME key for a resubmitted create, so
/// <c>IMeetingRepository.FindByIdempotencyKeyAsync</c> can answer "this already happened" with the FIRST result
/// rather than a duplicate row. The key is server-computed and never accepted from the client.
/// </summary>
public interface IMeetingIdempotencyKeyResolver
{
    string Resolve(Guid organizerUserId, Guid meetingTypeId, DateTimeOffset startAt, DateTimeOffset endAt, string title);

    /// <summary>
    /// MOD-0357 S4 (pack §8.5, K11) — the meeting→task bridge's own key. Unlike <see cref="Resolve"/> (server-
    /// derived from business fields alone, so a genuine double-click hashes to the same key with no client
    /// input), this combines the caller-supplied <paramref name="clientIdempotencyKey"/> with the meeting and
    /// actor: the bridge command has no small set of business fields whose repetition alone implies a retry
    /// (two DIFFERENT tasks opened from the same meeting by the same actor are not the same request), so the
    /// caller states its own retry intent, the same REST idempotency-key convention as everywhere else on the
    /// web — this only makes that key resolvable per (meeting, actor) rather than trusting it tenant-wide.
    /// </summary>
    string ResolveForTaskBridge(Guid meetingId, Guid actorUserId, string clientIdempotencyKey);

    /// <summary>
    /// MOD-0357 S7 (pack §8 K11, K6) — the continuation-meeting bridge's own key, same shape as
    /// <see cref="ResolveForTaskBridge"/> and for the same reason: "schedule a follow-up of THIS meeting" has
    /// no small set of business fields whose repetition alone implies a retry (two DIFFERENT continuations of
    /// the same meeting are not the same request), so the caller states its own retry intent.
    /// </summary>
    string ResolveForFollowUp(Guid meetingId, Guid actorUserId, string clientIdempotencyKey);
}
