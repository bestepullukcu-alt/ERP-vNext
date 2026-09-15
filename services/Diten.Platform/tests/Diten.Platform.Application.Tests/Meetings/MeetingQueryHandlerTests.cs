using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Meetings.Queries;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

// MOD-0357 S2, AC4/AC12 — D3 visibility: organizer ∨ attendee ∨ read-all; everyone else gets 404 (never 403 —
// no metadata leak to a caller with no relationship to the record, mirroring the cross-tenant posture).

public sealed class MeetingQueryHandlerTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;

    private sealed class Fixture
    {
        public FakeMeetingRepository Meetings { get; } = new() { Tenant = Tenant };
        public FakeMeetingTypeRepository Types { get; } = new() { Tenant = Tenant };
        public FakeMeetingAttendeeRepository Attendees { get; } = new() { Tenant = Tenant };
        public FakeAgendaItemRepository Agenda { get; } = new() { Tenant = Tenant };
        public FakeRecordLinkRepository Links { get; } = new();

        public Meeting SeedMeeting(Guid organizerId)
        {
            var meeting = new Meeting
            {
                TenantId = Tenant, Title = "Aylık QA Toplantısı", MeetingTypeId = Guid.NewGuid(),
                StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
                OrganizerUserId = organizerId, IdempotencyKey = Guid.NewGuid().ToString()
            };
            Meetings.Seed(meeting);
            return meeting;
        }

        public GetMeetingByIdHandler ByIdHandler(Guid callerId, bool hasReadAll = false) => new(
            Meetings, Types, Attendees, Agenda,
            new FakeCurrentUserContext(callerId),
            new FakeActorPermissionContext(hasReadAll ? [MeetingPermissions.ReadAll] : []));

        public GetMeetingListHandler ListHandler(Guid callerId, bool hasReadAll = false) => new(
            Meetings, Types, Attendees,
            new Diten.Platform.Application.Features.Meetings.RecordLinks.RecordLinkService(
                Links, new FakeTenantContext(Tenant), new FakeCurrentUserContext(callerId)),
            new FakeCurrentUserContext(callerId),
            new FakeActorPermissionContext(hasReadAll ? [MeetingPermissions.ReadAll] : []));
    }

    [Fact]
    public async Task The_organizer_can_view_their_own_meeting()
    {
        var fx = new Fixture();
        var organizer = Guid.NewGuid();
        var meeting = fx.SeedMeeting(organizer);

        var response = await fx.ByIdHandler(organizer).Handle(new GetMeetingByIdQuery(meeting.Id, "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task An_attendee_can_view_the_meeting()
    {
        var fx = new Fixture();
        var organizer = Guid.NewGuid();
        var attendeeUserId = Guid.NewGuid();
        var meeting = fx.SeedMeeting(organizer);
        await fx.Attendees.CreateAsync(new MeetingAttendee { TenantId = Tenant, MeetingId = meeting.Id, UserId = attendeeUserId });

        var response = await fx.ByIdHandler(attendeeUserId).Handle(new GetMeetingByIdQuery(meeting.Id, "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task Read_all_sees_every_meeting_regardless_of_organizer_or_attendee_status()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting(Guid.NewGuid());

        var response = await fx.ByIdHandler(Guid.NewGuid(), hasReadAll: true)
            .Handle(new GetMeetingByIdQuery(meeting.Id, "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task A_caller_with_NO_relationship_to_the_meeting_gets_404_not_403()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting(Guid.NewGuid());

        var response = await fx.ByIdHandler(Guid.NewGuid()).Handle(new GetMeetingByIdQuery(meeting.Id, "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.NotFound, response.ReasonCode);
    }

    [Fact]
    public async Task GetList_hides_a_meeting_the_caller_has_no_relationship_to()
    {
        var fx = new Fixture();
        var visible = fx.SeedMeeting(Guid.NewGuid());
        var caller = visible.OrganizerUserId;
        fx.SeedMeeting(Guid.NewGuid()); // someone else's meeting — the caller is neither organizer nor attendee

        var filter = new GetMeetingListFilter(null, null, null, null, null, null);
        var response = await fx.ListHandler(caller).Handle(new GetMeetingListQuery(filter, "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!.Items);
        Assert.Equal(visible.Id, response.Data.Items[0].Id);
    }

    [Fact]
    public async Task GetList_filtered_to_I_am_attendee_only_excludes_meetings_the_caller_organizes()
    {
        var fx = new Fixture();
        var caller = Guid.NewGuid();
        var organized = fx.SeedMeeting(caller);
        var attended = fx.SeedMeeting(Guid.NewGuid());
        await fx.Attendees.CreateAsync(new MeetingAttendee { TenantId = Tenant, MeetingId = attended.Id, UserId = caller });

        var filter = new GetMeetingListFilter(null, null, null, null, IAmAttendeeOnly: true, null);
        var response = await fx.ListHandler(caller).Handle(new GetMeetingListQuery(filter, "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!.Items);
        Assert.Equal(attended.Id, response.Data.Items[0].Id);
        Assert.DoesNotContain(response.Data.Items, i => i.Id == organized.Id);
    }
}
