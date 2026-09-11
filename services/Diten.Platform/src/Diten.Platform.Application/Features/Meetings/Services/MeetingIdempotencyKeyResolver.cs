using System.Security.Cryptography;
using System.Text;

namespace Diten.Platform.Application.Features.Meetings.Services;

/// <inheritdoc cref="IMeetingIdempotencyKeyResolver"/>
public sealed class MeetingIdempotencyKeyResolver : IMeetingIdempotencyKeyResolver
{
    public string Resolve(
        Guid organizerUserId, Guid meetingTypeId, DateTimeOffset startAt, DateTimeOffset endAt, string title)
    {
        // Deterministic: the SAME five values always hash to the SAME key, so a retried request (double-click,
        // client retry) resolves to the SAME meeting rather than a second one (K11). Title is included
        // case/whitespace-sensitively on purpose — a title that genuinely changed between two clicks is a
        // different request, not a retry of the same one.
        var input = string.Join(
            '|',
            organizerUserId.ToString("N"),
            meetingTypeId.ToString("N"),
            startAt.ToUniversalTime().ToString("O"),
            endAt.ToUniversalTime().ToString("O"),
            title.Trim());

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string ResolveForTaskBridge(Guid meetingId, Guid actorUserId, string clientIdempotencyKey)
    {
        var input = string.Join(
            '|',
            "bridge",
            meetingId.ToString("N"),
            actorUserId.ToString("N"),
            clientIdempotencyKey.Trim());

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
