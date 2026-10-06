using System.Text;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities.Meetings;

namespace Diten.Platform.Application.Features.Meetings.Services;

/// <summary>MOD-0357 S5b — which of the three invite moments an .ics file is being built for.</summary>
public enum MeetingIcsEventType
{
    Invite,
    Change,
    Cancel
}

/// <summary>
/// MOD-0357 S5b — a real RFC 5545 calendar attachment for the invite/change/cancel e-mails, so Outlook/Gmail
/// recognise the message as a meeting invite and the reader can "Accept" it in their own calendar.
///
/// <para><b>Pure on purpose.</b> Input is the meeting, the organizer and attendees (already resolved to real
/// e-mail addresses), and which of the three moments this is; output is one string. No I/O, no clock read
/// beyond an injectable <c>nowUtc</c> — the same posture <c>MeetingSeriesSchedule</c> already takes, and for the
/// identical reason: a pure function's own test can assert the exact text, never a rendered artifact.</para>
///
/// <para><b>UID is the whole point.</b> Every occurrence of the SAME meeting — invite, a later change, the
/// eventual cancel — carries <c>"{meeting.Id}@diten"</c> and nothing else. A calendar client keys "is this an
/// update to something I already have" on UID; anything else here would create a second event in the reader's
/// calendar instead of updating the first.</para>
/// </summary>
public static class MeetingIcsBuilder
{
    public const string FileName = "invite.ics";

    /// <summary>The MIME content-type header, including the METHOD parameter calendar clients read BEFORE
    /// opening the attachment — this is what makes Outlook/Gmail show "Accept/Decline" instead of a generic
    /// file download.</summary>
    public static string ContentType(MeetingIcsEventType eventType) =>
        $"text/calendar; charset=utf-8; method={MethodFor(eventType)}";

    public static string Build(
        Meeting meeting,
        TaskNotificationRecipient organizer,
        IReadOnlyList<TaskNotificationRecipient> attendees,
        MeetingIcsEventType eventType,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        ArgumentNullException.ThrowIfNull(organizer);
        ArgumentNullException.ThrowIfNull(attendees);

        var status = eventType == MeetingIcsEventType.Cancel ? "CANCELLED" : "CONFIRMED";

        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Diten//MOD-0357 Meetings//EN",
            "CALSCALE:GREGORIAN",
            $"METHOD:{MethodFor(eventType)}",
            "BEGIN:VEVENT",
            // UID is identical across invite/change/cancel (same meeting.Id); SEQUENCE is the ONLY thing that
            // tells the reader's calendar "this is an update", per RFC 5545 §3.8.7.4.
            $"UID:{meeting.Id}@diten",
            $"SEQUENCE:{meeting.Version}",
            $"DTSTAMP:{FormatUtc(nowUtc ?? DateTimeOffset.UtcNow)}",
            $"DTSTART:{FormatUtc(meeting.StartAt)}",
            $"DTEND:{FormatUtc(meeting.EndAt)}",
            $"SUMMARY:{EscapeText(meeting.Title)}"
        };

        if (!string.IsNullOrWhiteSpace(meeting.Location))
        {
            lines.Add($"LOCATION:{EscapeText(meeting.Location)}");
        }

        lines.Add($"STATUS:{status}");
        lines.Add(OrganizerLine(organizer));
        lines.AddRange(attendees.Select(a => AttendeeLine(a, eventType)));

        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");

        // RFC 5545 §3.1 — CRLF throughout, a trailing CRLF on the last line too.
        return string.Join("\r\n", lines.Select(Fold)) + "\r\n";
    }

    private static string MethodFor(MeetingIcsEventType eventType) =>
        eventType == MeetingIcsEventType.Cancel ? "CANCEL" : "REQUEST";

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");

    private static string OrganizerLine(TaskNotificationRecipient organizer) =>
        $"ORGANIZER;CN={QuoteParameter(organizer.DisplayName ?? organizer.Email)}:mailto:{organizer.Email}";

    /// <summary>RSVP=TRUE always (pack §"ics sözleşmesi") — the reader is always being asked to respond, even
    /// on a change. PARTSTAT is omitted entirely on CANCEL: there is no "action state" left to declare against
    /// an event that no longer exists.</summary>
    private static string AttendeeLine(TaskNotificationRecipient attendee, MeetingIcsEventType eventType)
    {
        var partstat = eventType == MeetingIcsEventType.Cancel ? string.Empty : ";PARTSTAT=NEEDS-ACTION";
        return $"ATTENDEE;CN={QuoteParameter(attendee.DisplayName ?? attendee.Email)};RSVP=TRUE{partstat}:mailto:{attendee.Email}";
    }

    /// <summary>
    /// RFC 5545 §3.2 — a NAME is a PARAMETER value, not TEXT, so <see cref="EscapeText"/> does not apply to it:
    /// a backslash there is a literal backslash, and an unquoted colon ends the parameter list and turns the rest
    /// of the name into the address. Always DQUOTEd, so "Tufanoğlu, Ali" and "Ops: Lead" survive; the three
    /// characters a quoted value still cannot hold are carried with RFC 6868's caret encoding. (CT 2026-09-13.)
    /// </summary>
    private static string QuoteParameter(string value) =>
        "\"" + value
            .Replace("^", "^^")
            .Replace("\r\n", "^n")
            .Replace("\n", "^n")
            .Replace("\"", "^'") + "\"";

    /// <summary>RFC 5545 §3.3.11 TEXT escaping. Order matters: the backslash itself is escaped FIRST, so
    /// escaping <c>;</c>/<c>,</c> afterward never doubles up on a backslash this method just inserted.</summary>
    private static string EscapeText(string value) => value
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n");

    /// <summary>
    /// RFC 5545 §3.1 line folding: a content line longer than 75 OCTETS is split by inserting CRLF followed by
    /// a single space, and the space itself counts against the next line's own 75-octet budget.
    ///
    /// <para>Byte-counted per character via UTF-8, not per <c>char</c> — an ASCII character is always exactly
    /// one octet, which is the only case this WP's own test list exercises; a genuine UTF-16 surrogate PAIR
    /// (an emoji, say) is not split mid-pair here since <c>char</c> iteration keeps each half together as one
    /// unit, but its own two- or three-octet UTF-8 width is measured on the SECOND half only — an acceptable,
    /// narrow gap for content this module never actually produces (meeting titles/locations, not emoji).</para>
    /// </summary>
    private static string Fold(string line)
    {
        const int maxOctets = 75;
        if (Encoding.UTF8.GetByteCount(line) <= maxOctets)
        {
            return line;
        }

        var result = new StringBuilder();
        var current = new StringBuilder();
        var currentOctets = 0;
        var budget = maxOctets;
        var wroteFirst = false;

        foreach (var ch in line)
        {
            var chOctets = Encoding.UTF8.GetByteCount([ch]);
            if (current.Length > 0 && currentOctets + chOctets > budget)
            {
                if (wroteFirst)
                {
                    result.Append("\r\n ");
                }

                result.Append(current);
                current.Clear();
                currentOctets = 0;
                budget = maxOctets - 1; // every continuation line loses one octet to its own leading space.
                wroteFirst = true;
            }

            current.Append(ch);
            currentOctets += chOctets;
        }

        if (current.Length > 0)
        {
            if (wroteFirst)
            {
                result.Append("\r\n ");
            }

            result.Append(current);
        }

        return result.ToString();
    }
}
