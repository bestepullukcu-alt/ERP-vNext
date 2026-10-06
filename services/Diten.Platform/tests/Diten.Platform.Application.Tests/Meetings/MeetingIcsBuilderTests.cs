using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Domain.Entities.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S5b — <see cref="MeetingIcsBuilder"/>, a pure function: input is the meeting + resolved
/// organizer/attendees + which of the three moments this is, output is one string. Every assertion here reads
/// the exact text, never a rendered artifact — the same posture the pack's own "NASIL" instruction demands.
/// </summary>
public sealed class MeetingIcsBuilderTests
{
    private static readonly Guid MeetingId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTimeOffset StartAt = new(2027, 6, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EndAt = new(2027, 6, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NowUtc = new(2027, 6, 1, 12, 34, 56, TimeSpan.Zero);
    private static readonly TaskNotificationRecipient Organizer = new(Guid.NewGuid(), "organizer@example.test", "Ayşe Yılmaz");
    private static readonly TaskNotificationRecipient AttendeeA = new(Guid.NewGuid(), "a@example.test", "Attendee A");
    private static readonly TaskNotificationRecipient AttendeeB = new(Guid.NewGuid(), "b@example.test", "Attendee B");

    private static Meeting MakeMeeting(string title = "Weekly Quality Review", string? location = "Room 2", int version = 1) => new()
    {
        Id = MeetingId, TenantId = Guid.NewGuid(), Title = title, MeetingTypeId = Guid.NewGuid(),
        StartAt = StartAt, EndAt = EndAt, Location = location, OrganizerUserId = Organizer.UserId,
        IdempotencyKey = "key", CreatedBy = "test", Version = version
    };

    private static string Build(MeetingIcsEventType type, Meeting? meeting = null, IReadOnlyList<TaskNotificationRecipient>? attendees = null) =>
        MeetingIcsBuilder.Build(meeting ?? MakeMeeting(), Organizer, attendees ?? [AttendeeA, AttendeeB], type, NowUtc);

    [Fact]
    public void UID_is_identical_across_invite_change_and_cancel()
    {
        var invite = Build(MeetingIcsEventType.Invite);
        var change = Build(MeetingIcsEventType.Change);
        var cancel = Build(MeetingIcsEventType.Cancel);

        var expectedUid = $"UID:{MeetingId}@diten";
        Assert.Contains(expectedUid, invite);
        Assert.Contains(expectedUid, change);
        Assert.Contains(expectedUid, cancel);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void SEQUENCE_follows_the_meetings_own_Version(int version)
    {
        var ics = Build(MeetingIcsEventType.Change, MakeMeeting(version: version));

        Assert.Contains($"SEQUENCE:{version}", ics);
    }

    [Fact]
    public void Invite_and_change_use_METHOD_REQUEST_and_STATUS_CONFIRMED()
    {
        var invite = Build(MeetingIcsEventType.Invite);
        var change = Build(MeetingIcsEventType.Change);

        Assert.Contains("METHOD:REQUEST", invite);
        Assert.Contains("STATUS:CONFIRMED", invite);
        Assert.Contains("METHOD:REQUEST", change);
        Assert.Contains("STATUS:CONFIRMED", change);
    }

    [Fact]
    public void Cancel_uses_METHOD_CANCEL_and_STATUS_CANCELLED()
    {
        var cancel = Build(MeetingIcsEventType.Cancel);

        Assert.Contains("METHOD:CANCEL", cancel);
        Assert.Contains("STATUS:CANCELLED", cancel);
        Assert.DoesNotContain("METHOD:REQUEST", cancel);
        Assert.DoesNotContain("STATUS:CONFIRMED", cancel);
    }

    [Fact]
    public void PARTSTAT_is_present_on_invite_and_change_but_omitted_on_cancel()
    {
        var invite = Build(MeetingIcsEventType.Invite);
        var cancel = Build(MeetingIcsEventType.Cancel);

        Assert.Contains("PARTSTAT=NEEDS-ACTION", invite);
        Assert.DoesNotContain("PARTSTAT", cancel);
        // RSVP=TRUE stays on every moment, cancel included (pack's own explicit contract).
        Assert.Contains("RSVP=TRUE", cancel);
    }

    [Fact]
    public void Timestamps_are_UTC_in_the_yyyyMMddTHHmmssZ_form()
    {
        var ics = Build(MeetingIcsEventType.Invite);

        Assert.Contains("DTSTAMP:20270601T123456Z", ics);
        Assert.Contains("DTSTART:20270615T090000Z", ics);
        Assert.Contains("DTEND:20270615T100000Z", ics);
    }

    [Fact]
    public void ORGANIZER_and_ATTENDEE_lines_carry_the_resolved_mailto_addresses()
    {
        var ics = Build(MeetingIcsEventType.Invite);
        // Unfolded: the ATTENDEE line is long enough (CN + RSVP + PARTSTAT + mailto) to legitimately cross the
        // 75-octet fold boundary on its own — proven separately by the folding tests below. Reversing that
        // fold here is what a real calendar CLIENT does before reading the property value, so asserting on the
        // unfolded text is the correct comparison, not a workaround.
        var unfolded = ics.Replace("\r\n ", string.Empty);

        Assert.Contains($"ORGANIZER;CN=\"Ayşe Yılmaz\":mailto:{Organizer.Email}", unfolded);
        Assert.Contains($"ATTENDEE;CN=\"Attendee A\";RSVP=TRUE;PARTSTAT=NEEDS-ACTION:mailto:{AttendeeA.Email}", unfolded);
        Assert.Contains($"ATTENDEE;CN=\"Attendee B\";RSVP=TRUE;PARTSTAT=NEEDS-ACTION:mailto:{AttendeeB.Email}", unfolded);
    }

    [Fact]
    public void LOCATION_line_is_entirely_absent_when_the_meeting_has_none()
    {
        var ics = Build(MeetingIcsEventType.Invite, MakeMeeting(location: null));

        Assert.DoesNotContain("LOCATION", ics);
    }

    [Fact]
    public void LOCATION_line_is_present_and_escaped_when_the_meeting_has_one()
    {
        var ics = Build(MeetingIcsEventType.Invite, MakeMeeting(location: "Building A, Room 3"));

        Assert.Contains("LOCATION:Building A\\, Room 3", ics);
    }

    [Fact]
    public void A_comma_in_the_title_is_escaped_and_does_not_break_the_calendar()
    {
        var ics = Build(MeetingIcsEventType.Invite, MakeMeeting(title: "Review, Q3 Planning"));

        Assert.Contains("SUMMARY:Review\\, Q3 Planning", ics);
        // The RAW (unescaped) comma must never appear on its own in the SUMMARY line.
        Assert.DoesNotContain("SUMMARY:Review, Q3", ics);
    }

    [Fact]
    public void A_semicolon_and_backslash_in_the_title_are_both_escaped()
    {
        var ics = Build(MeetingIcsEventType.Invite, MakeMeeting(title: "Budget; Review \\ Sign-off"));

        Assert.Contains("SUMMARY:Budget\\; Review \\\\ Sign-off", ics);
    }

    [Fact]
    public void A_long_SUMMARY_line_is_folded_at_75_octets_with_a_leading_space_continuation()
    {
        var longTitle = new string('A', 120);
        var ics = Build(MeetingIcsEventType.Invite, MakeMeeting(title: longTitle));

        var physicalLines = ics.Split("\r\n");
        var summaryLineIndex = Array.FindIndex(physicalLines, l => l.StartsWith("SUMMARY:", StringComparison.Ordinal));
        Assert.True(summaryLineIndex >= 0);

        // The folded continuation is the NEXT physical line, and RFC 5545 requires it to start with a space.
        var continuation = physicalLines[summaryLineIndex + 1];
        Assert.StartsWith(" ", continuation);

        // Every physical line (this one included) stays within the 75-octet budget.
        foreach (var physicalLine in physicalLines)
        {
            Assert.True(System.Text.Encoding.UTF8.GetByteCount(physicalLine) <= 75, $"Line exceeded 75 octets: '{physicalLine}'");
        }

        // Unfolding (removing every CRLF+space) must reproduce the original, unbroken SUMMARY value.
        var unfolded = ics.Replace("\r\n ", string.Empty);
        Assert.Contains($"SUMMARY:{longTitle}", unfolded);
    }

    [Fact]
    public void A_short_SUMMARY_line_is_never_folded()
    {
        var ics = Build(MeetingIcsEventType.Invite, MakeMeeting(title: "Short"));

        Assert.Contains("SUMMARY:Short\r\n", ics);
    }

    [Fact]
    public void The_calendar_ends_with_a_trailing_CRLF_and_uses_CRLF_throughout()
    {
        var ics = Build(MeetingIcsEventType.Invite);

        Assert.EndsWith("\r\n", ics);
        Assert.DoesNotContain("\r\r", ics);
        // No bare LF without a preceding CR anywhere in the document.
        for (var i = 0; i < ics.Length; i++)
        {
            if (ics[i] == '\n')
            {
                Assert.True(i > 0 && ics[i - 1] == '\r', $"Bare LF at index {i}");
            }
        }
    }

    [Fact]
    public void The_calendar_declares_VCALENDAR_and_VEVENT_correctly_paired()
    {
        var ics = Build(MeetingIcsEventType.Invite);

        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("END:VCALENDAR", ics);
        Assert.Contains("BEGIN:VEVENT", ics);
        Assert.Contains("END:VEVENT", ics);
        Assert.True(ics.IndexOf("BEGIN:VCALENDAR", StringComparison.Ordinal) < ics.IndexOf("BEGIN:VEVENT", StringComparison.Ordinal));
        Assert.True(ics.IndexOf("END:VEVENT", StringComparison.Ordinal) < ics.IndexOf("END:VCALENDAR", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(MeetingIcsEventType.Invite, "text/calendar; charset=utf-8; method=REQUEST")]
    [InlineData(MeetingIcsEventType.Change, "text/calendar; charset=utf-8; method=REQUEST")]
    [InlineData(MeetingIcsEventType.Cancel, "text/calendar; charset=utf-8; method=CANCEL")]
    public void ContentType_carries_the_matching_METHOD_parameter(MeetingIcsEventType eventType, string expected)
    {
        Assert.Equal(expected, MeetingIcsBuilder.ContentType(eventType));
    }

    [Fact]
    public void The_file_name_is_always_invite_ics_regardless_of_event_type()
    {
        Assert.Equal("invite.ics", MeetingIcsBuilder.FileName);
    }

    /// <summary>
    /// CT 2026-09-13 — a NAME is a parameter value, not TEXT (RFC 5545 §3.2): backslash escaping does not apply
    /// there, and a comma, semicolon or colon must be carried inside DQUOTEs instead. "Tufanoğlu, Ali" is the
    /// ordinary "Last, First" form; unquoted, a client either shows the backslash or ends the parameter list at
    /// the first colon and reads the rest of the name as the address.
    /// </summary>
    [Fact]
    public void A_display_name_with_a_comma_colon_or_semicolon_is_DQUOTED_not_backslash_escaped()
    {
        var organizer = new TaskNotificationRecipient(Guid.NewGuid(), "organizer@example.test", "Tufanoğlu, Ali");
        var attendee = new TaskNotificationRecipient(Guid.NewGuid(), "ops@example.test", "Ops: Lead; QA");

        var ics = MeetingIcsBuilder.Build(MakeMeeting(), organizer, [attendee], MeetingIcsEventType.Invite, NowUtc)
            .Replace("\r\n ", string.Empty);

        Assert.Contains("ORGANIZER;CN=\"Tufanoğlu, Ali\":mailto:organizer@example.test", ics);
        Assert.Contains("ATTENDEE;CN=\"Ops: Lead; QA\";RSVP=TRUE;PARTSTAT=NEEDS-ACTION:mailto:ops@example.test", ics);
        Assert.DoesNotContain("CN=Tufanoğlu\\,", ics);
    }

    /// <summary>CT 2026-09-13 — a DQUOTE cannot appear inside a quoted parameter value; RFC 6868 carries it as ^'.</summary>
    [Fact]
    public void A_double_quote_inside_a_display_name_is_caret_encoded()
    {
        var attendee = new TaskNotificationRecipient(Guid.NewGuid(), "q@example.test", "Ali \"Kalite\" Tufan");

        var ics = MeetingIcsBuilder.Build(MakeMeeting(), Organizer, [attendee], MeetingIcsEventType.Invite, NowUtc)
            .Replace("\r\n ", string.Empty);

        Assert.Contains("ATTENDEE;CN=\"Ali ^'Kalite^' Tufan\";", ics);
    }

    /// <summary>
    /// CT 2026-09-13 — folding is counted in OCTETS. This company writes Turkish titles and the tenant module ships
    /// in Chinese and Arabic: every physical line must stay within 75 UTF-8 octets, and unfolding must give back the
    /// exact title. The WP's own folding test used ASCII only, where a character and an octet are the same thing.
    /// </summary>
    [Theory]
    [InlineData("Çeyreklik Yönetim Gözden Geçirme Toplantısı · Şube Müdürleri ve Kalite Güvence Ekibi (Ağustos)")]
    [InlineData("季度管理评审会议季度管理评审会议季度管理评审会议季度管理评审会议季度管理评审会议")]
    [InlineData("اجتماع مراجعة الإدارة الفصلي لمديري الفروع وفريق ضمان الجودة في المقر الرئيسي")]
    public void A_multibyte_title_folds_within_75_octets_and_unfolds_to_the_exact_title(string title)
    {
        var ics = Build(MeetingIcsEventType.Invite, MakeMeeting(title: title));

        foreach (var physical in ics.Split("\r\n"))
        {
            Assert.True(System.Text.Encoding.UTF8.GetByteCount(physical) <= 75,
                $"{System.Text.Encoding.UTF8.GetByteCount(physical)} octets: {physical}");
        }

        Assert.Contains($"SUMMARY:{title}\r\n", ics.Replace("\r\n ", string.Empty));
    }
}
