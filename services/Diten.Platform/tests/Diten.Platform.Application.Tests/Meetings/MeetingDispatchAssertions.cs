using System.Text;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// BL-406 (CT follow-up F2, 2026-09-15) — <c>MeetingInviteMailer</c> sends ONE dispatch per recipient
/// (<c>To: [recipient]</c> + <c>MeetingAttendeeUserId</c>), never one dispatch whose <c>To</c> carries the whole group:
/// a permanently failed send has to be attributable to the single attendee it was for, and attendees no longer see
/// each other's addresses. The .ics ATTENDEE list inside every copy is unchanged.
///
/// <para>The meeting mail tests used to read "the ONE dispatch of this event" and compare its <c>To</c> list with the
/// participants. That shape is gone, so what those tests were really proving is asserted here against the new one,
/// once, for every caller: the right people — no one missing, no one extra, no one twice — each on their own
/// dispatch, and every copy of one event carrying the same calendar identity.</para>
/// </summary>
internal static class MeetingDispatchAssertions
{
    /// <summary>
    /// Every dispatch of <paramref name="eventCode"/> in <paramref name="requests"/>, after asserting the per-recipient
    /// contract: (a) each has exactly one <c>To</c> recipient and its <c>MeetingAttendeeUserId</c> is that recipient;
    /// (b) no recipient is mailed twice for the event; (c) the recipients across all of them are EXACTLY
    /// <paramref name="expectedUserIds"/>.
    /// </summary>
    public static IReadOnlyList<NotificationEventDispatchRequest> AssertPerRecipient(
        IEnumerable<NotificationEventDispatchRequest> requests,
        string eventCode,
        IReadOnlyCollection<Guid> expectedUserIds,
        Func<Guid, string> emailOf)
    {
        var dispatches = requests.Where(r => r.EventCode == eventCode).ToList();

        foreach (var dispatch in dispatches)
        {
            Assert.True(dispatch.To.Count == 1,
                $"{eventCode}: a dispatch carries {dispatch.To.Count} To recipients ({string.Join(", ", dispatch.To.Select(t => t.Email))}); expected exactly one.");
            Assert.True(dispatch.MeetingAttendeeUserId is not null,
                $"{eventCode}: the dispatch to {dispatch.To[0].Email} carries no MeetingAttendeeUserId.");
            Assert.Equal(emailOf(dispatch.MeetingAttendeeUserId!.Value), dispatch.To[0].Email);
        }

        var emails = dispatches.Select(d => d.To[0].Email).ToList();
        var duplicates = emails
            .GroupBy(email => email, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} x{group.Count()}")
            .ToList();
        Assert.True(duplicates.Count == 0, $"{eventCode}: recipient mailed more than once: {string.Join(", ", duplicates)}.");

        Assert.Equal(
            expectedUserIds.Select(emailOf).Order(StringComparer.Ordinal).ToArray(),
            emails.Order(StringComparer.Ordinal).ToArray());

        return dispatches;
    }

    /// <summary>The value of one .ics property, asserted to be IDENTICAL on every dispatch in
    /// <paramref name="dispatches"/> — every copy of one event is the same calendar entry (UID, METHOD, SEQUENCE…), so
    /// comparing one sample would let a copy that disagrees slip through.</summary>
    public static string SameIcsProperty(IReadOnlyList<NotificationEventDispatchRequest> dispatches, string name)
    {
        Assert.True(dispatches.Count > 0, $"No dispatch to read .ics property {name} from.");
        var values = dispatches.Select(d => Property(IcsOf(d), name)).Distinct(StringComparer.Ordinal).ToList();
        Assert.True(values.Count == 1, $".ics {name} differs across the event's dispatches: {string.Join(", ", values)}.");
        return values[0];
    }

    /// <summary>The attachment text with RFC 5545 folding reversed, the way a calendar client reads it.</summary>
    private static string IcsOf(NotificationEventDispatchRequest request)
        => Encoding.UTF8.GetString(Assert.Single(request.Attachments!).Content).Replace("\r\n ", string.Empty);

    private static string Property(string ics, string name)
        => ics.Split("\r\n").Single(line => line.StartsWith(name + ":", StringComparison.Ordinal))[(name.Length + 1)..];
}
