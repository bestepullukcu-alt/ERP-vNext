using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S5 — K5 (Accept/Decline, idempotent, 404 for a non-attendee) and K12 (an invite/change/cancel
/// e-mail failure never fails the write it rides on). <see cref="FakeMeetingInviteMailer"/> (MeetingS2TestDoubles.cs)
/// stands in for <see cref="IMeetingInviteMailer"/> — the mailer's OWN K12 try/catch is proven separately, by
/// <see cref="MeetingInviteMailerNeverThrowsTests"/>, against a REAL <c>MeetingInviteMailer</c>.
/// </summary>
public sealed class MeetingInvitationTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly Guid Organizer = Guid.NewGuid();
    private static readonly Guid Invitee = Guid.NewGuid();

    private sealed class Fixture
    {
        public FakeMeetingRepository Meetings { get; } = new() { Tenant = Tenant };
        public FakeMeetingTypeRepository Types { get; } = new() { Tenant = Tenant };
        public FakeMeetingAttendeeRepository Attendees { get; } = new() { Tenant = Tenant };
        public FakeMeetingInviteMailer InviteMailer { get; } = new();

        public RespondToInvitationHandler RespondHandler(Guid actingUserId)
            => new(Meetings, Attendees, new FakeCurrentUserContext(actingUserId));

        public UpdateMeetingHandler UpdateHandler()
            => new(Meetings, Types, Attendees, new FakeCurrentUserContext(Organizer), InviteMailer);

        public CancelMeetingHandler CancelHandler()
            => new(Meetings, Types, Attendees, new FakeCurrentUserContext(Organizer), InviteMailer);

        public MeetingType SeedType()
        {
            var type = new MeetingType { TenantId = Tenant, Name = "MGMT-REVIEW " + Guid.NewGuid() };
            Types.Seed(type);
            return type;
        }

        public Meeting SeedMeeting(Guid typeId, MeetingLifecycle lifecycle = MeetingLifecycle.Scheduled)
        {
            var meeting = new Meeting
            {
                TenantId = Tenant, Title = "T", MeetingTypeId = typeId,
                StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
                OrganizerUserId = Organizer, IdempotencyKey = Guid.NewGuid().ToString(),
                Lifecycle = lifecycle
            };
            Meetings.Seed(meeting);
            return meeting;
        }

        public MeetingAttendee SeedAttendee(Guid meetingId, Guid userId, InvitationResponse response = InvitationResponse.Pending)
        {
            var attendee = new MeetingAttendee
            {
                TenantId = Tenant, MeetingId = meetingId, UserId = userId,
                InvitationResponse = response, CreatedBy = "test"
            };
            Attendees.Seed(attendee);
            return attendee;
        }
    }

    // ── K5 — respond ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_records_Accepted_and_returns_200()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee);

        var response = await fx.RespondHandler(Invitee).Handle(
            new RespondToInvitationCommand(meeting.Id, new RespondToInvitationRequest("Accept"), "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(200, response.StatusCode);
        var stored = await fx.Attendees.FindAsync(meeting.Id, Invitee);
        Assert.Equal(InvitationResponse.Accepted, stored!.InvitationResponse);
    }

    [Fact]
    public async Task Decline_records_Declined()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee);

        var response = await fx.RespondHandler(Invitee).Handle(
            new RespondToInvitationCommand(meeting.Id, new RespondToInvitationRequest("Decline"), "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var stored = await fx.Attendees.FindAsync(meeting.Id, Invitee);
        Assert.Equal(InvitationResponse.Declined, stored!.InvitationResponse);
    }

    [Fact]
    public async Task A_caller_with_no_attendee_row_gets_404_never_a_hint_the_meeting_exists()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        // Nobody seeded as an attendee — the caller was never invited.

        var response = await fx.RespondHandler(Invitee).Handle(
            new RespondToInvitationCommand(meeting.Id, new RespondToInvitationRequest("Accept"), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.AttendeeNotFound, response.ReasonCode);
    }

    [Fact]
    public async Task An_unknown_meeting_is_404()
    {
        var fx = new Fixture();

        var response = await fx.RespondHandler(Invitee).Handle(
            new RespondToInvitationCommand(Guid.NewGuid(), new RespondToInvitationRequest("Accept"), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Responding_to_a_CANCELLED_meeting_is_409()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id, MeetingLifecycle.Cancelled);
        fx.SeedAttendee(meeting.Id, Invitee);

        var response = await fx.RespondHandler(Invitee).Handle(
            new RespondToInvitationCommand(meeting.Id, new RespondToInvitationRequest("Accept"), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.Cancelled, response.ReasonCode);
    }

    [Fact]
    public async Task Responding_to_a_COMPLETED_meeting_is_409()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id, MeetingLifecycle.Completed);
        fx.SeedAttendee(meeting.Id, Invitee);

        var response = await fx.RespondHandler(Invitee).Handle(
            new RespondToInvitationCommand(meeting.Id, new RespondToInvitationRequest("Accept"), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.Completed, response.ReasonCode);
    }

    [Theory]
    [InlineData("Maybe")]
    [InlineData("accept")] // K5 — compared case-sensitively; the wire literal is "Accept", not "accept".
    [InlineData("")]
    public async Task A_response_that_is_not_exactly_Accept_or_Decline_is_400(string value)
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee);

        var response = await fx.RespondHandler(Invitee).Handle(
            new RespondToInvitationCommand(meeting.Id, new RespondToInvitationRequest(value), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.InvitationResponseInvalid, response.ReasonCode);
    }

    [Fact]
    public async Task The_SAME_response_resubmitted_is_still_200_idempotent()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee);
        var handler = fx.RespondHandler(Invitee);
        var request = new RespondToInvitationRequest("Accept");

        var first = await handler.Handle(new RespondToInvitationCommand(meeting.Id, request, "corr"), CancellationToken.None);
        var second = await handler.Handle(new RespondToInvitationCommand(meeting.Id, request, "corr"), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(InvitationResponse.Accepted, (await fx.Attendees.FindAsync(meeting.Id, Invitee))!.InvitationResponse);
    }

    [Fact]
    public async Task Switching_from_Accept_to_Decline_overwrites_the_earlier_response()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee);
        var handler = fx.RespondHandler(Invitee);

        await handler.Handle(new RespondToInvitationCommand(meeting.Id, new RespondToInvitationRequest("Accept"), "corr"), CancellationToken.None);
        await handler.Handle(new RespondToInvitationCommand(meeting.Id, new RespondToInvitationRequest("Decline"), "corr"), CancellationToken.None);

        Assert.Equal(InvitationResponse.Declined, (await fx.Attendees.FindAsync(meeting.Id, Invitee))!.InvitationResponse);
    }

    // ── K7/S5 — the change e-mail only for a schedule-visible edit ─────────────────────────────────────────────

    [Fact]
    public async Task Updating_StartAt_sends_a_change_email()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee);

        var request = new UpdateMeetingRequest(
            "T", type.Id, meeting.StartAt.AddHours(1), meeting.EndAt.AddHours(1), null, null, meeting.Version);
        var response = await fx.UpdateHandler().Handle(new UpdateMeetingCommand(meeting.Id, request, "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(fx.InviteMailer.Calls, c => c.Kind == "change");
    }

    [Fact]
    public async Task Updating_ONLY_the_title_sends_no_change_email()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee);

        var request = new UpdateMeetingRequest(
            "Yeni Başlık", type.Id, meeting.StartAt, meeting.EndAt, meeting.Location, "Yeni açıklama", meeting.Version);
        var response = await fx.UpdateHandler().Handle(new UpdateMeetingCommand(meeting.Id, request, "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(fx.InviteMailer.Calls);
    }

    [Fact]
    public async Task Updating_the_LOCATION_alone_also_counts_as_a_schedule_change()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee);

        var request = new UpdateMeetingRequest(
            "T", type.Id, meeting.StartAt, meeting.EndAt, "New Room", null, meeting.Version);
        await fx.UpdateHandler().Handle(new UpdateMeetingCommand(meeting.Id, request, "corr"), CancellationToken.None);

        Assert.Single(fx.InviteMailer.Calls, c => c.Kind == "change");
    }

    // ── K7/S5 — cancel always notifies ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelling_always_sends_a_cancel_email()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee);

        var response = await fx.CancelHandler().Handle(
            new CancelMeetingCommand(meeting.Id, new CancelMeetingRequest("Not needed", meeting.Version), "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(fx.InviteMailer.Calls, c => c.Kind == "cancel");
    }

    // ── K12 — a failed/absent delivery is reported, never silently absorbed ────────────────────────────────────

    [Fact]
    public async Task A_failed_invite_delivery_still_returns_201_with_the_failure_named()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        fx.InviteMailer.NextResult = new MeetingInviteDeliveryResult(Sent: false, Failed: true, Reason: "DISPATCH_FAILED");

        var meetings = new FakeMeetingRepository { Tenant = Tenant };
        var attendees = new FakeMeetingAttendeeRepository { Tenant = Tenant };
        var mediator = new FakeEligibilityMediator();
        mediator.EligibleUserIds.Add(Organizer);
        var handler = new CreateMeetingHandler(
            meetings, fx.Types, attendees, new FakeTenantContext(Tenant), new FakeCurrentUserContext(Organizer),
            new MeetingIdempotencyKeyResolver(), mediator, fx.InviteMailer);

        var request = new CreateMeetingRequest(
            "T", type.Id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, Organizer, null, null, null);
        var response = await handler.Handle(new CreateMeetingCommand(request, "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.NotNull(response.Data!.InviteDelivery);
        Assert.False(response.Data.InviteDelivery!.Sent);
        Assert.True(response.Data.InviteDelivery.Failed);
        Assert.Equal("DISPATCH_FAILED", response.Data.InviteDelivery.Reason);
    }
}
