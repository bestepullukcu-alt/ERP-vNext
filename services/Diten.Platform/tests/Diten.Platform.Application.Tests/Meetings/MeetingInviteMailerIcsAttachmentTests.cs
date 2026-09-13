using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S5b — <c>MeetingInviteMailer</c> attaches a real .ics to all three moments. Uses the SAME
/// <c>SucceedingDispatchAdapter</c>/<c>FakeRecipientResolver</c> harness <c>MeetingInviteMailerNeverThrowsTests</c>
/// already establishes, so this file adds coverage rather than duplicating a second double.
/// </summary>
public sealed class MeetingInviteMailerIcsAttachmentTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly Guid Organizer = Guid.NewGuid();
    private static readonly Guid Invitee = Guid.NewGuid();

    private sealed class SucceedingDispatchAdapter : INotificationEventDispatchAdapter
    {
        public NotificationEventDispatchRequest? LastRequest { get; private set; }

        public Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(
            NotificationEventDispatchRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(Response<NotificationDispatchDto>.Success());
        }
    }

    private sealed class FakeRecipientResolver : ITaskNotificationRecipientResolver
    {
        public Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TaskNotificationRecipient>>(
                userIds.Select(id => new TaskNotificationRecipient(id, $"{id}@example.test", "Someone")).ToList());
    }

    /// <summary>BL-374 (2) — resolves every OTHER attendee normally but returns nothing for the organizer, so
    /// <c>MeetingInviteMailer</c> falls onto its own empty-email fallback record for the organizer specifically.</summary>
    private sealed class UnresolvableOrganizerRecipientResolver : ITaskNotificationRecipientResolver
    {
        public Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TaskNotificationRecipient>>(
                userIds.Where(id => id != Organizer)
                    .Select(id => new TaskNotificationRecipient(id, $"{id}@example.test", "Someone"))
                    .ToList());
    }

    private static MeetingInviteMailer Mailer(INotificationEventDispatchAdapter adapter, ITaskNotificationRecipientResolver? resolver = null)
        => new(adapter, resolver ?? new FakeRecipientResolver(), new FakeTenantContext(Tenant), Options.Create(new AuthServiceOptions()), NullLogger<MeetingInviteMailer>.Instance);

    private static Meeting MakeMeeting(int version = 1) => new()
    {
        Id = Guid.NewGuid(), TenantId = Tenant, Title = "Weekly Quality Review", MeetingTypeId = Guid.NewGuid(),
        StartAt = DateTimeOffset.UtcNow.AddDays(1), EndAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
        OrganizerUserId = Organizer, IdempotencyKey = Guid.NewGuid().ToString(), CreatedBy = "test", Version = version
    };

    private static MeetingAttendee MakeAttendee(Guid meetingId, Guid userId) => new()
    { TenantId = Tenant, MeetingId = meetingId, UserId = userId, CreatedBy = "test" };

    [Fact]
    public async Task SendInviteAsync_attaches_one_ics_file_named_invite_ics_with_the_REQUEST_content_type()
    {
        var meeting = MakeMeeting();
        var adapter = new SucceedingDispatchAdapter();
        var mailer = Mailer(adapter);

        await mailer.SendInviteAsync(meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Invitee)], Organizer);

        var attachment = Assert.Single(adapter.LastRequest!.Attachments!);
        Assert.Equal("invite.ics", attachment.FileName);
        Assert.Equal("text/calendar; charset=utf-8; method=REQUEST", attachment.ContentType);
        Assert.Contains($"UID:{meeting.Id}@diten", System.Text.Encoding.UTF8.GetString(attachment.Content));
        Assert.Contains($"SEQUENCE:{meeting.Version}", System.Text.Encoding.UTF8.GetString(attachment.Content));
    }

    [Fact]
    public async Task SendChangeAsync_also_uses_METHOD_REQUEST_but_the_meetings_own_current_Version()
    {
        var meeting = MakeMeeting(version: 3);
        var adapter = new SucceedingDispatchAdapter();
        var mailer = Mailer(adapter);

        await mailer.SendChangeAsync(meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Invitee)], Organizer);

        var attachment = Assert.Single(adapter.LastRequest!.Attachments!);
        Assert.Equal("text/calendar; charset=utf-8; method=REQUEST", attachment.ContentType);
        Assert.Contains("SEQUENCE:3", System.Text.Encoding.UTF8.GetString(attachment.Content));
    }

    [Fact]
    public async Task SendCancelAsync_uses_METHOD_CANCEL_and_the_SAME_UID_a_prior_invite_would_have_used()
    {
        var meeting = MakeMeeting();
        var adapter = new SucceedingDispatchAdapter();
        var mailer = Mailer(adapter);

        await mailer.SendCancelAsync(meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Invitee)], Organizer);

        var attachment = Assert.Single(adapter.LastRequest!.Attachments!);
        Assert.Equal("text/calendar; charset=utf-8; method=CANCEL", attachment.ContentType);
        var text = System.Text.Encoding.UTF8.GetString(attachment.Content);
        Assert.Contains($"UID:{meeting.Id}@diten", text);
        Assert.Contains("METHOD:CANCEL", text);
        Assert.Contains("STATUS:CANCELLED", text);
    }

    [Fact]
    public async Task The_ics_ORGANIZER_line_carries_the_organizers_resolved_email_not_just_their_name()
    {
        var meeting = MakeMeeting();
        var adapter = new SucceedingDispatchAdapter();
        var mailer = Mailer(adapter);

        await mailer.SendInviteAsync(meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Invitee)], actingUserId: Guid.NewGuid());

        var attachment = Assert.Single(adapter.LastRequest!.Attachments!);
        // The GUID-shaped test e-mail makes this ORGANIZER line long enough to legitimately cross the
        // 75-octet fold boundary on its own (proven separately by MeetingIcsBuilderTests) — unfolded first,
        // the same reason that suite's own ORGANIZER/ATTENDEE assertion does.
        var text = System.Text.Encoding.UTF8.GetString(attachment.Content).Replace("\r\n ", string.Empty);
        Assert.Contains($"mailto:{Organizer}@example.test", text);
    }

    /// <summary>BL-374 (2) — an ORGANIZER-less .ics is worse than none (RFC 5546 needs an ORGANIZER address on
    /// METHOD:REQUEST); the invite BODY must still go (K12) with no attachment at all in that case.</summary>
    [Fact]
    public async Task When_the_organizers_email_cannot_be_resolved_the_ics_is_omitted_but_the_body_still_sends()
    {
        var meeting = MakeMeeting();
        var adapter = new SucceedingDispatchAdapter();
        var mailer = Mailer(adapter, new UnresolvableOrganizerRecipientResolver());

        var result = await mailer.SendInviteAsync(meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Invitee)], actingUserId: Guid.NewGuid());

        Assert.True(result.Sent);
        Assert.Null(adapter.LastRequest!.Attachments);
    }
}
