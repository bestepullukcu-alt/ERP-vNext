using System.Text;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Meetings.Providers;
using Diten.Platform.Application.Features.Meetings.Queries;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// Go-live, 2026-09-13 — the TWO-USER meeting scenarios the live browser tour cannot walk (it can only act as the
/// organizer). Every write and read here goes through the REAL handlers, the REAL Mongo repositories, the REAL
/// <see cref="MeetingInviteMailer"/> and the REAL <see cref="MeetingIcsBuilder"/>. Only three seams are doubled,
/// and none of them is the thing under test: the eligibility lookup (<see cref="FakeEligibilityMediator"/>), the
/// user-id → e-mail resolver, and the notification dispatch adapter, which RECORDS what the mailer hands over —
/// the recipients and the .ics attachment — instead of rendering and sending it.
///
/// <para>Shared-database pattern (<see cref="MongoIntegrationHarness.CreateAsync"/>): one test database, isolation
/// by a fresh TenantId per test, rows of that tenant deleted on dispose.</para>
/// </summary>
public sealed class MeetingTwoUserFlowMongoTests : IAsyncLifetime
{
    private const string InviteEvent = "platform.meetings.invite";
    private const string ChangeEvent = "platform.meetings.change";
    private const string CancelEvent = "platform.meetings.cancel";

    private readonly Guid _organizer = Guid.NewGuid();
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly Guid _outsider = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();

    private MongoIntegrationHarness _harness = null!;
    private MeetingRepository _meetings = null!;
    private MeetingTypeRepository _types = null!;
    private MeetingAttendeeRepository _attendees = null!;
    private AgendaItemRepository _agenda = null!;
    private readonly RecordingDispatchAdapter _dispatches = new();
    private readonly FakeEligibilityMediator _eligibility = new();

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        _meetings = new MeetingRepository(_harness.DbContext, _harness.TenantContext);
        _types = new MeetingTypeRepository(_harness.DbContext, _harness.TenantContext);
        _attendees = new MeetingAttendeeRepository(_harness.DbContext, _harness.TenantContext);
        _agenda = new AgendaItemRepository(_harness.DbContext, _harness.TenantContext);
        _eligibility.EligibleUserIds.UnionWith([_organizer, _alice, _bob]);
    }

    public async Task DisposeAsync()
    {
        var tenants = new[] { _harness.TenantId, _otherTenantId };
        await _harness.Database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings)
            .DeleteManyAsync(Builders<Meeting>.Filter.In(x => x.TenantId, tenants));
        await _harness.Database.GetCollection<MeetingAttendee>(PlatformCollections.MeetingAttendees)
            .DeleteManyAsync(Builders<MeetingAttendee>.Filter.In(x => x.TenantId, tenants));
        await _harness.Database.GetCollection<MeetingType>(PlatformCollections.MeetingTypes)
            .DeleteManyAsync(Builders<MeetingType>.Filter.In(x => x.TenantId, tenants));
        await _harness.DisposeAsync();
    }

    // ── A — a participant answers; the organizer's view shows it; nobody else can answer ─────────────────────

    [Fact]
    public async Task A_participant_accepts_another_declines_and_the_organizers_meeting_view_shows_both_answers()
    {
        var meeting = await OrganizerCreatesMeetingAsync();
        var before = await OrganizerViewAsync(meeting.Id);
        Assert.Equal(InvitationResponse.Pending, ResponseOf(before, _alice));
        Assert.Equal(InvitationResponse.Pending, ResponseOf(before, _bob));

        var accept = await RespondAsync(_alice, meeting.Id, "Accept");
        var decline = await RespondAsync(_bob, meeting.Id, "Decline");

        Assert.Equal(200, accept.StatusCode);
        Assert.Equal(200, decline.StatusCode);
        var after = await OrganizerViewAsync(meeting.Id);
        Assert.Equal(InvitationResponse.Accepted, ResponseOf(after, _alice));
        Assert.Equal(InvitationResponse.Declined, ResponseOf(after, _bob));
        Assert.Equal(InvitationResponse.Accepted, ResponseOf(after, _organizer));
    }

    [Fact]
    public async Task A_user_who_is_not_on_the_meeting_cannot_answer_its_invitation_and_no_answer_changes()
    {
        var meeting = await OrganizerCreatesMeetingAsync();
        await RespondAsync(_alice, meeting.Id, "Accept");
        await RespondAsync(_bob, meeting.Id, "Decline");

        var refused = await RespondAsync(_outsider, meeting.Id, "Accept");

        Assert.False(refused.IsSuccessful);
        Assert.Equal(404, refused.StatusCode);
        Assert.Equal(MeetingReasonCodes.AttendeeNotFound, refused.ReasonCode);
        var view = await OrganizerViewAsync(meeting.Id);
        Assert.Equal(InvitationResponse.Accepted, ResponseOf(view, _alice));
        Assert.Equal(InvitationResponse.Declined, ResponseOf(view, _bob));
        Assert.DoesNotContain(view.Attendees, a => a.UserId == _outsider);
        Assert.Equal(3, view.Attendees.Count);
    }

    [Fact]
    public async Task Another_tenant_cannot_answer_this_tenants_invitation_even_with_the_invitees_own_user_id()
    {
        var meeting = await OrganizerCreatesMeetingAsync();

        Response<NoContent> refused;
        _harness.TenantContext.SetTenant(_otherTenantId);
        try
        {
            refused = await RespondAsync(_alice, meeting.Id, "Accept");
        }
        finally
        {
            _harness.TenantContext.SetTenant(_harness.TenantId);
        }

        Assert.False(refused.IsSuccessful);
        Assert.Equal(404, refused.StatusCode);
        Assert.Equal(InvitationResponse.Pending, ResponseOf(await OrganizerViewAsync(meeting.Id), _alice));
    }

    // ── B — the .ics each participant receives across invite → change → cancel ─────────────────────────────────

    [Fact]
    public async Task Moving_the_meeting_after_invites_sends_every_participant_a_REQUEST_with_the_same_UID_and_a_higher_SEQUENCE()
    {
        var meeting = await OrganizerCreatesMeetingAsync();
        var invite = Assert.Single(_dispatches.Requests, r => r.EventCode == InviteEvent);
        var newStart = meeting.StartAt.AddHours(2);

        var update = await new UpdateMeetingHandler(
                _meetings, _types, _attendees, new FakeCurrentUserContext(_organizer), Mailer())
            .Handle(
                new UpdateMeetingCommand(
                    meeting.Id,
                    new UpdateMeetingRequest(meeting.Title, meeting.MeetingTypeId, newStart, newStart.AddHours(1),
                        meeting.Location, meeting.Description, meeting.Version),
                    "corr"),
                CancellationToken.None);

        Assert.True(update.IsSuccessful);
        var change = Assert.Single(_dispatches.Requests, r => r.EventCode == ChangeEvent);
        Assert.Equal(ParticipantEmails(), RecipientEmails(change));
        Assert.Equal(ParticipantEmails(), RecipientEmails(invite));

        var inviteIcs = IcsOf(invite);
        var changeIcs = IcsOf(change);
        Assert.Equal("REQUEST", Property(changeIcs, "METHOD"));
        Assert.Equal($"{meeting.Id}@diten", Property(inviteIcs, "UID"));
        Assert.Equal(Property(inviteIcs, "UID"), Property(changeIcs, "UID"));
        Assert.True(
            int.Parse(Property(changeIcs, "SEQUENCE")) > int.Parse(Property(inviteIcs, "SEQUENCE")),
            $"change SEQUENCE {Property(changeIcs, "SEQUENCE")} must be higher than invite SEQUENCE {Property(inviteIcs, "SEQUENCE")}");
        Assert.Equal(newStart.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'"), Property(changeIcs, "DTSTART"));
    }

    [Fact]
    public async Task Cancelling_after_invites_sends_every_participant_a_CANCEL_with_the_same_UID_and_STATUS_CANCELLED()
    {
        var meeting = await OrganizerCreatesMeetingAsync();
        var invite = Assert.Single(_dispatches.Requests, r => r.EventCode == InviteEvent);

        var cancel = await new CancelMeetingHandler(
                _meetings, _types, _attendees, new FakeCurrentUserContext(_organizer), Mailer())
            .Handle(
                new CancelMeetingCommand(meeting.Id, new CancelMeetingRequest("Not needed", meeting.Version), "corr"),
                CancellationToken.None);

        Assert.True(cancel.IsSuccessful);
        var cancellation = Assert.Single(_dispatches.Requests, r => r.EventCode == CancelEvent);
        Assert.Equal(ParticipantEmails(), RecipientEmails(cancellation));
        Assert.Equal("text/calendar; charset=utf-8; method=CANCEL", Assert.Single(cancellation.Attachments!).ContentType);

        var inviteIcs = IcsOf(invite);
        var cancelIcs = IcsOf(cancellation);
        Assert.Equal("CANCEL", Property(cancelIcs, "METHOD"));
        Assert.Equal("CANCELLED", Property(cancelIcs, "STATUS"));
        Assert.Equal(Property(inviteIcs, "UID"), Property(cancelIcs, "UID"));
        Assert.True(int.Parse(Property(cancelIcs, "SEQUENCE")) > int.Parse(Property(inviteIcs, "SEQUENCE")));
    }

    // ── D — the invitation reaches the invitee's Task Center, never the organizer's ───────────────────────────

    [Fact]
    public async Task The_invitee_gets_an_invitation_work_item_for_the_meeting_but_the_organizer_who_created_it_gets_none()
    {
        var meeting = await OrganizerCreatesMeetingAsync();
        var provider = new MeetingWorkItemProvider(
            _attendees, _meetings, _types, new FakeUserDisplayNameResolver((_organizer, "Ayşe Yılmaz")), SlaForTests.Real());

        var aliceItems = await provider.GetWorkItemsAsync(Actor(_alice), CancellationToken.None);
        var organizerItems = await provider.GetWorkItemsAsync(Actor(_organizer), CancellationToken.None);

        var item = Assert.Single(aliceItems);
        Assert.Equal(meeting.Id.ToString(), item.Id);
        Assert.Equal(WorkItemContract.IntentMeetingInvite, item.WorkIntent);
        Assert.Equal(["acceptInvite", "declineInvite"], item.Actions.Select(a => a.Code).ToArray());
        Assert.All(item.Actions, a => Assert.True(a.Enabled));
        Assert.Equal(_organizer.ToString(), item.Requester!.Id);
        Assert.Empty(organizerItems);
    }

    // ── harness ───────────────────────────────────────────────────────────────────────────────────────────────

    private static WorkItemActor Actor(Guid userId)
        => new(userId, IsPlatformActor: false, new HashSet<string>([MeetingPermissions.Read], StringComparer.Ordinal));

    private MeetingInviteMailer Mailer() => new(
        _dispatches, new EmailPerUserResolver(), _harness.TenantContext,
        Options.Create(new AuthServiceOptions()), NullLogger<MeetingInviteMailer>.Instance);

    private async Task<MeetingDto> OrganizerCreatesMeetingAsync()
    {
        var type = await _types.CreateAsync(new MeetingType
        {
            TenantId = _harness.TenantId, Name = "MGMT-REVIEW " + Guid.NewGuid(), CreatedBy = "test"
        });
        var start = new DateTimeOffset(DateTimeOffset.UtcNow.AddDays(3).UtcDateTime.Date.AddHours(9), TimeSpan.Zero);

        var handler = new CreateMeetingHandler(
            _meetings, _types, _attendees, _harness.TenantContext, new FakeCurrentUserContext(_organizer),
            new MeetingIdempotencyKeyResolver(), _eligibility, Mailer());
        var response = await handler.Handle(
            new CreateMeetingCommand(
                new CreateMeetingRequest(
                    "Yönetim Gözden Geçirme " + Guid.NewGuid().ToString("N")[..6], type.Id, start, start.AddHours(1),
                    "Room 2", _organizer, null, null, [_alice, _bob]),
                "corr"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful, string.Join(" | ", response.Errors));
        Assert.Equal(201, response.StatusCode);
        return response.Data!;
    }

    private Task<Response<NoContent>> RespondAsync(Guid actingUserId, Guid meetingId, string answer)
        => new RespondToInvitationHandler(_meetings, _attendees, new FakeCurrentUserContext(actingUserId))
            .Handle(new RespondToInvitationCommand(meetingId, new RespondToInvitationRequest(answer), "corr"), CancellationToken.None);

    private async Task<MeetingDto> OrganizerViewAsync(Guid meetingId)
    {
        var response = await new GetMeetingByIdHandler(
                _meetings, _types, _attendees, _agenda, new FakeCurrentUserContext(_organizer), new FakeActorPermissionContext())
            .Handle(new GetMeetingByIdQuery(meetingId, "corr"), CancellationToken.None);
        Assert.True(response.IsSuccessful, $"organizer view refused: {response.StatusCode} {response.ReasonCode}");
        return response.Data!;
    }

    private static InvitationResponse ResponseOf(MeetingDto meeting, Guid userId)
        => meeting.Attendees.Single(a => a.UserId == userId).InvitationResponse;

    private string[] ParticipantEmails() => new[] { EmailOf(_alice), EmailOf(_bob) }.Order(StringComparer.Ordinal).ToArray();

    private static string[] RecipientEmails(NotificationEventDispatchRequest request)
        => request.To.Select(r => r.Email).Order(StringComparer.Ordinal).ToArray();

    private static string EmailOf(Guid userId) => $"{userId:N}@example.test";

    /// <summary>The attachment text with RFC 5545 folding reversed, the way a calendar client reads it.</summary>
    private static string IcsOf(NotificationEventDispatchRequest request)
        => Encoding.UTF8.GetString(Assert.Single(request.Attachments!).Content).Replace("\r\n ", string.Empty);

    private static string Property(string ics, string name)
        => ics.Split("\r\n").Single(line => line.StartsWith(name + ":", StringComparison.Ordinal))[(name.Length + 1)..];

    /// <summary>Records what the mailer hands to the notification pipeline — the recipients and the .ics.</summary>
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

    private sealed class EmailPerUserResolver : ITaskNotificationRecipientResolver
    {
        public Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TaskNotificationRecipient>>(
                userIds.Select(id => new TaskNotificationRecipient(id, EmailOf(id), "User " + id.ToString("N")[..4])).ToList());
    }
}
