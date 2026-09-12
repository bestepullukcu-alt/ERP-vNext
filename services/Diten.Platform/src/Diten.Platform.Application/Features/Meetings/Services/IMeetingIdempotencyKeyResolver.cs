namespace Diten.Platform.Application.Features.Meetings.Services;

/// <summary>
/// MOD-0357 S2 (pack §8 K11) — computes the SAME key for a resubmitted create, so
/// <c>IMeetingRepository.FindByIdempotencyKeyAsync</c> can answer "this already happened" with the FIRST result
/// rather than a duplicate row. The key is server-computed and never accepted from the client.
/// </summary>
public interface IMeetingIdempotencyKeyResolver
{
    string Resolve(Guid organizerUserId, Guid meetingTypeId, DateTimeOffset startAt, DateTimeOffset endAt, string title);
}
