using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

// MOD-0357 S2 — handler-level unit tests over the in-memory doubles (MeetingS2TestDoubles.cs). The eligibility
// SEAM itself (GetTaskAssignmentPersonLookupQuery) is doubled by FakeEligibilityMediator here and proven for
// real by MOD-0024's own GetTaskAssignmentPersonLookupHandler test suite — these tests are isolated to
// MOD-0357's own logic, per this WP's own NE #3 ("same seam, no second resolution path").

public sealed class MeetingCommandHandlerTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly Guid Organizer = Guid.NewGuid();

    private sealed class Fixture
    {
        public FakeMeetingRepository Meetings { get; } = new() { Tenant = Tenant };
        public FakeMeetingTypeRepository Types { get; } = new() { Tenant = Tenant };
        public FakeMeetingAttendeeRepository Attendees { get; } = new() { Tenant = Tenant };
        public FakeEligibilityMediator Mediator { get; } = new();
        public FakeTenantContext TenantContext { get; } = new(Tenant);
        public FakeCurrentUserContext CurrentUser { get; } = new(Organizer);
        public IMeetingIdempotencyKeyResolver Idempotency { get; } = new MeetingIdempotencyKeyResolver();
        public FakeMeetingInviteMailer InviteMailer { get; } = new();

        public Fixture()
        {
            Mediator.EligibleUserIds.Add(Organizer);
        }

        public CreateMeetingHandler CreateHandler()
            => new(Meetings, Types, Attendees, TenantContext, CurrentUser, Idempotency, Mediator, InviteMailer);

        public UpdateMeetingHandler UpdateHandler() => new(Meetings, Types, Attendees, CurrentUser, InviteMailer);

        public CancelMeetingHandler CancelHandler() => new(Meetings, Types, Attendees, CurrentUser, InviteMailer);

        public MeetingType SeedType()
        {
            var type = new MeetingType { TenantId = Tenant, Name = "MGMT-REVIEW " + Guid.NewGuid() };
            Types.Seed(type);
            return type;
        }
    }

    private static CreateMeetingRequest Request(Guid typeId, string title = "Aylık QA Toplantısı") => new(
        Title: title,
        MeetingTypeId: typeId,
        StartAt: DateTimeOffset.UtcNow,
        EndAt: DateTimeOffset.UtcNow.AddHours(1),
        Location: null,
        OrganizerUserId: Organizer,
        Description: null,
        FollowUpOfMeetingId: null,
        AttendeeUserIds: null);

    // ── AC1 — idempotent create ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_retried_with_the_SAME_request_returns_the_SAME_meeting_id_and_writes_no_second_row()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var handler = fx.CreateHandler();
        var request = Request(type.Id);

        var first = await handler.Handle(new CreateMeetingCommand(request, "corr"), CancellationToken.None);
        var second = await handler.Handle(new CreateMeetingCommand(request, "corr"), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.Equal(first.Data!.Id, second.Data!.Id);
        Assert.Single(await fx.Meetings.ListAsync());
    }

    [Fact]
    public async Task Create_with_EndAt_not_after_StartAt_is_400_MEETING_END_BEFORE_START()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var handler = fx.CreateHandler();
        var request = Request(type.Id) with { EndAt = DateTimeOffset.UtcNow, StartAt = DateTimeOffset.UtcNow };

        var response = await handler.Handle(new CreateMeetingCommand(request, "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.EndBeforeStart, response.ReasonCode);
    }

    [Fact]
    public async Task Create_against_a_type_that_does_not_exist_is_400_MEETING_TYPE_NOT_FOUND()
    {
        var fx = new Fixture();
        var handler = fx.CreateHandler();

        var response = await handler.Handle(new CreateMeetingCommand(Request(Guid.NewGuid()), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.TypeNotFound, response.ReasonCode);
    }

    // ── AC3 — attendee eligibility, scope-EXEMPT ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_with_an_ineligible_attendee_silently_drops_that_id_and_still_creates_the_meeting()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var eligibleAttendee = Guid.NewGuid();
        fx.Mediator.EligibleUserIds.Add(eligibleAttendee);
        var ineligible = Guid.NewGuid();

        var handler = fx.CreateHandler();
        var request = Request(type.Id) with { AttendeeUserIds = [eligibleAttendee, ineligible] };

        var response = await handler.Handle(new CreateMeetingCommand(request, "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var attendees = await fx.Attendees.ListByMeetingIdAsync(response.Data!.Id);
        // S5 — the organizer's own Accepted row (written at creation) plus the one eligible invitee; the
        // ineligible id never became a row at all.
        Assert.Equal(2, attendees.Count);
        Assert.Contains(attendees, a => a.UserId == eligibleAttendee && a.InvitationResponse == InvitationResponse.Pending);
        Assert.Contains(attendees, a => a.UserId == Organizer && a.InvitationResponse == InvitationResponse.Accepted);
    }

    [Fact]
    public async Task AddMeetingAttendees_reports_duplicate_and_ineligible_separately_and_still_adds_the_rest()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var alreadyThere = Guid.NewGuid();
        var eligibleNew = Guid.NewGuid();
        var ineligible = Guid.NewGuid();
        fx.Mediator.EligibleUserIds.Add(alreadyThere);
        fx.Mediator.EligibleUserIds.Add(eligibleNew);

        var meeting = new Meeting
        {
            TenantId = Tenant, Title = "T", MeetingTypeId = type.Id,
            StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
            OrganizerUserId = Organizer, IdempotencyKey = Guid.NewGuid().ToString()
        };
        fx.Meetings.Seed(meeting);
        await fx.Attendees.CreateAsync(new MeetingAttendee { TenantId = Tenant, MeetingId = meeting.Id, UserId = alreadyThere });

        var handler = new AddMeetingAttendeesHandler(fx.Meetings, fx.Attendees, fx.TenantContext, fx.CurrentUser, fx.Mediator);
        var response = await handler.Handle(
            new AddMeetingAttendeesCommand(meeting.Id, new AddMeetingAttendeesRequest([alreadyThere, eligibleNew, ineligible]), "corr"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!.Added);
        Assert.Equal(eligibleNew, response.Data.Added[0].UserId);
        Assert.Equal(2, response.Data.Skipped.Count);
        Assert.Contains(response.Data.Skipped, s => s.UserId == alreadyThere && s.ReasonCode == MeetingReasonCodes.AttendeeDuplicate);
        Assert.Contains(response.Data.Skipped, s => s.UserId == ineligible && s.ReasonCode == MeetingReasonCodes.AttendeeNotEligible);
    }

    // ── AC5 — cancelled/completed conflict, cancellation reason required ────────────────────────────────────

    [Fact]
    public async Task Cancel_without_a_reason_is_400_MEETING_CANCELLATION_REASON_REQUIRED()
    {
        var fx = new Fixture();
        var meeting = new Meeting
        {
            TenantId = Tenant, Title = "T", MeetingTypeId = Guid.NewGuid(),
            StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
            OrganizerUserId = Organizer, IdempotencyKey = Guid.NewGuid().ToString()
        };
        fx.Meetings.Seed(meeting);

        var handler = fx.CancelHandler();
        var response = await handler.Handle(
            new CancelMeetingCommand(meeting.Id, new CancelMeetingRequest("", meeting.Version), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.CancellationReasonRequired, response.ReasonCode);
    }

    [Fact]
    public async Task Updating_an_ALREADY_cancelled_meeting_is_409_MEETING_CANCELLED()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = new Meeting
        {
            TenantId = Tenant, Title = "T", MeetingTypeId = type.Id,
            StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
            OrganizerUserId = Organizer, IdempotencyKey = Guid.NewGuid().ToString(),
            Lifecycle = MeetingLifecycle.Cancelled
        };
        fx.Meetings.Seed(meeting);

        var handler = fx.UpdateHandler();
        var request = new UpdateMeetingRequest("Yeni", type.Id, meeting.StartAt, meeting.EndAt, null, null, meeting.Version);
        var response = await handler.Handle(new UpdateMeetingCommand(meeting.Id, request, "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.Cancelled, response.ReasonCode);
    }

    // ── AC6 — reassign organizer ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReassignOrganizer_to_an_eligible_user_keeps_the_previous_one_on_the_record()
    {
        var fx = new Fixture();
        var newOrganizer = Guid.NewGuid();
        fx.Mediator.EligibleUserIds.Add(newOrganizer);

        var meeting = new Meeting
        {
            TenantId = Tenant, Title = "T", MeetingTypeId = Guid.NewGuid(),
            StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
            OrganizerUserId = Organizer, IdempotencyKey = Guid.NewGuid().ToString()
        };
        fx.Meetings.Seed(meeting);

        var handler = new ReassignMeetingOrganizerHandler(fx.Meetings, fx.Mediator);
        var response = await handler.Handle(
            new ReassignMeetingOrganizerCommand(meeting.Id, new ReassignMeetingOrganizerRequest(newOrganizer, meeting.Version), "corr"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var reread = await fx.Meetings.GetByIdAsync(meeting.Id);
        Assert.Equal(newOrganizer, reread!.OrganizerUserId);
        Assert.Equal(Organizer, reread.PreviousOrganizerUserId);
    }

    [Fact]
    public async Task ReassignOrganizer_on_a_cancelled_meeting_is_409_MEETING_CANCELLED()
    {
        var fx = new Fixture();
        var meeting = new Meeting
        {
            TenantId = Tenant, Title = "T", MeetingTypeId = Guid.NewGuid(),
            StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
            OrganizerUserId = Organizer, IdempotencyKey = Guid.NewGuid().ToString(),
            Lifecycle = MeetingLifecycle.Cancelled
        };
        fx.Meetings.Seed(meeting);

        var handler = new ReassignMeetingOrganizerHandler(fx.Meetings, fx.Mediator);
        var response = await handler.Handle(
            new ReassignMeetingOrganizerCommand(meeting.Id, new ReassignMeetingOrganizerRequest(Guid.NewGuid(), meeting.Version), "corr"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.Cancelled, response.ReasonCode);
    }
}
