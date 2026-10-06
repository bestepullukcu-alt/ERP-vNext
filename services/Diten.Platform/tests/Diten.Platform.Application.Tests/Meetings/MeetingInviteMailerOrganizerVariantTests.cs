using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
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
/// WP-MG-MOD0357-BL387-ORGANIZER-CALENDAR-MAIL-01 — the organizer/others split inside
/// <see cref="MeetingInviteMailer"/>'s private <c>SendAsync</c>, isolated from Mongo. The recipient rule stays in
/// ONE place (the mailer); these tests prove that place, not any handler.
/// </summary>
public sealed class MeetingInviteMailerOrganizerVariantTests
{
    private const string InviteEvent = "platform.meetings.invite";
    private const string ChangeEvent = "platform.meetings.change";
    private const string CancelEvent = "platform.meetings.cancel";
    private const string OrganizerAddedEvent = "platform.meetings.organizer-added";
    private const string OrganizerUpdatedEvent = "platform.meetings.organizer-updated";
    private const string OrganizerCancelledEvent = "platform.meetings.organizer-cancelled";

    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly Guid Organizer = Guid.NewGuid();
    private static readonly Guid Invitee = Guid.NewGuid();
    private static readonly Guid ThirdParty = Guid.NewGuid();

    private sealed class RecordingDispatchAdapter : INotificationEventDispatchAdapter
    {
        public List<NotificationEventDispatchRequest> Requests { get; } = [];

        public Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(
            NotificationEventDispatchRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
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

    private static MeetingInviteMailer Mailer(INotificationEventDispatchAdapter adapter)
        => new(adapter, new FakeRecipientResolver(), new FakeTenantContext(Tenant), Options.Create(new AuthServiceOptions()), NullLogger<MeetingInviteMailer>.Instance);

    private static Meeting MakeMeeting() => new()
    {
        TenantId = Tenant, Title = "T", MeetingTypeId = Guid.NewGuid(),
        StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
        OrganizerUserId = Organizer, IdempotencyKey = Guid.NewGuid().ToString(), CreatedBy = "test"
    };

    private static MeetingAttendee MakeAttendee(Guid meetingId, Guid userId) => new()
    {
        TenantId = Tenant, MeetingId = meetingId, UserId = userId, CreatedBy = "test"
    };

    [Theory]
    [InlineData("invite", InviteEvent, OrganizerAddedEvent)]
    [InlineData("change", ChangeEvent, OrganizerUpdatedEvent)]
    [InlineData("cancel", CancelEvent, OrganizerCancelledEvent)]
    public async Task When_a_third_party_acts_the_organizer_gets_their_own_variant_and_everyone_else_gets_the_ordinary_one(
        string moment, string ordinaryEvent, string organizerEvent)
    {
        var meeting = MakeMeeting();
        var adapter = new RecordingDispatchAdapter();
        var mailer = Mailer(adapter);
        IReadOnlyList<MeetingAttendee> rows = [MakeAttendee(meeting.Id, Organizer), MakeAttendee(meeting.Id, Invitee)];

        var result = moment switch
        {
            "invite" => await mailer.SendInviteAsync(meeting, "MGMT-REVIEW", rows, ThirdParty),
            "change" => await mailer.SendChangeAsync(meeting, "MGMT-REVIEW", rows, ThirdParty),
            _ => await mailer.SendCancelAsync(meeting, "MGMT-REVIEW", rows, ThirdParty)
        };

        Assert.True(result.Sent);
        Assert.False(result.Failed);

        var ordinary = Assert.Single(adapter.Requests, r => r.EventCode == ordinaryEvent);
        Assert.Equal([$"{Invitee}@example.test"], ordinary.To.Select(r => r.Email).ToArray());

        var organizer = Assert.Single(adapter.Requests, r => r.EventCode == organizerEvent);
        Assert.Equal([$"{Organizer}@example.test"], organizer.To.Select(r => r.Email).ToArray());

        // Same meeting, same .ics identity carried into BOTH dispatches — only the template differs.
        Assert.Equal(2, adapter.Requests.Count);
        var ordinaryIcs = System.Text.Encoding.UTF8.GetString(Assert.Single(ordinary.Attachments!).Content);
        var organizerIcs = System.Text.Encoding.UTF8.GetString(Assert.Single(organizer.Attachments!).Content);
        Assert.Equal(ordinaryIcs, organizerIcs);
    }

    [Fact]
    public async Task When_the_organizer_acts_themselves_they_get_neither_variant_and_only_others_are_mailed()
    {
        var meeting = MakeMeeting();
        var adapter = new RecordingDispatchAdapter();
        var mailer = Mailer(adapter);

        var result = await mailer.SendInviteAsync(
            meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Organizer), MakeAttendee(meeting.Id, Invitee)], actingUserId: Organizer);

        Assert.True(result.Sent);
        var request = Assert.Single(adapter.Requests);
        Assert.Equal(InviteEvent, request.EventCode);
        Assert.Equal([$"{Invitee}@example.test"], request.To.Select(r => r.Email).ToArray());
        Assert.DoesNotContain(adapter.Requests, r => r.EventCode == OrganizerAddedEvent);
    }

    /// <summary>BL-373 — the sweep's own signature: no actor at all (<see cref="Guid.Empty"/>), so the actor rule
    /// excludes nobody, and the organizer must still land in their OWN variant, never the plain invite.</summary>
    [Fact]
    public async Task When_the_acting_user_is_empty_the_series_sweeps_own_signature_the_organizer_still_gets_their_own_variant()
    {
        var meeting = MakeMeeting();
        var adapter = new RecordingDispatchAdapter();
        var mailer = Mailer(adapter);

        var result = await mailer.SendInviteAsync(
            meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Organizer), MakeAttendee(meeting.Id, Invitee)], actingUserId: Guid.Empty);

        Assert.True(result.Sent);
        var organizerAdded = Assert.Single(adapter.Requests, r => r.EventCode == OrganizerAddedEvent);
        Assert.Equal([$"{Organizer}@example.test"], organizerAdded.To.Select(r => r.Email).ToArray());
        var invite = Assert.Single(adapter.Requests, r => r.EventCode == InviteEvent);
        Assert.Equal([$"{Invitee}@example.test"], invite.To.Select(r => r.Email).ToArray());
    }

    [Fact]
    public async Task When_only_the_organizer_is_a_recipient_and_a_third_party_acted_exactly_one_dispatch_goes_out()
    {
        var meeting = MakeMeeting();
        var adapter = new RecordingDispatchAdapter();
        var mailer = Mailer(adapter);

        var result = await mailer.SendInviteAsync(
            meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Organizer)], actingUserId: ThirdParty);

        Assert.True(result.Sent);
        var request = Assert.Single(adapter.Requests);
        Assert.Equal(OrganizerAddedEvent, request.EventCode);
        Assert.Equal([$"{Organizer}@example.test"], request.To.Select(r => r.Email).ToArray());
    }

    /// <summary>One group's dispatch failure must not cost the OTHER group its mail — the stronger form of K12
    /// this WP's own split requires (a single shared dispatch call could no longer make this true).</summary>
    [Fact]
    public async Task A_failure_dispatching_to_the_organizer_does_not_prevent_the_others_group_from_being_mailed()
    {
        var meeting = MakeMeeting();
        var adapter = new SelectivelyFailingDispatchAdapter(failEventCode: OrganizerAddedEvent);
        var mailer = Mailer(adapter);

        var result = await mailer.SendInviteAsync(
            meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Organizer), MakeAttendee(meeting.Id, Invitee)], actingUserId: ThirdParty);

        // Partial delivery: the failing group makes the combined result Failed, but the successful group still Sent.
        Assert.True(result.Sent);
        Assert.True(result.Failed);
        Assert.Contains(adapter.Requests, r => r.EventCode == InviteEvent);
        Assert.Contains(adapter.Requests, r => r.EventCode == OrganizerAddedEvent);
    }

    private sealed class SelectivelyFailingDispatchAdapter(string failEventCode) : INotificationEventDispatchAdapter
    {
        public List<NotificationEventDispatchRequest> Requests { get; } = [];

        public Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(
            NotificationEventDispatchRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(request.EventCode == failEventCode
                ? Response<NotificationDispatchDto>.Fail("boom", 500, "DISPATCH_FAILED")
                : Response<NotificationDispatchDto>.Success());
        }
    }
}
