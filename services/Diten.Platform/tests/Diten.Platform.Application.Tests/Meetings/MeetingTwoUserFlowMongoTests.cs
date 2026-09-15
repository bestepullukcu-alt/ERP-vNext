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
using static Diten.Platform.Application.Tests.Meetings.MeetingDispatchAssertions;

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
    private const string RemovedEvent = "platform.meetings.removed";
    private const string OrganizerAddedEvent = "platform.meetings.organizer-added";
    private const string OrganizerUpdatedEvent = "platform.meetings.organizer-updated";
    private const string OrganizerCancelledEvent = "platform.meetings.organizer-cancelled";

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

    // BL-406 (CT F2, 2026-09-15) — the mailer sends ONE dispatch per recipient, so each test below asserts the
    // per-recipient contract through MeetingDispatchAssertions: exactly the expected people, each on their own
    // dispatch, nobody twice, and every copy of one event carrying the same UID/METHOD/SEQUENCE.

    [Fact]
    public async Task Moving_the_meeting_after_invites_sends_every_participant_a_REQUEST_with_the_same_UID_and_a_higher_SEQUENCE()
    {
        var meeting = await OrganizerCreatesMeetingAsync();
        var invites = AssertPerRecipient(_dispatches.Requests, InviteEvent, [_alice, _bob], EmailOf);
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
        var changes = AssertPerRecipient(_dispatches.Requests, ChangeEvent, [_alice, _bob], EmailOf);
        // The organizer moved it themselves: no mail about their own action, under ANY event code.
        Assert.DoesNotContain(_dispatches.Requests, r => r.EventCode == OrganizerUpdatedEvent);
        Assert.DoesNotContain(_dispatches.Requests, r => r.To.Any(t => t.Email == EmailOf(_organizer)));

        var inviteUid = SameIcsProperty(invites, "UID");
        var inviteSequence = SameIcsProperty(invites, "SEQUENCE");
        var changeSequence = SameIcsProperty(changes, "SEQUENCE");
        Assert.Equal("REQUEST", SameIcsProperty(changes, "METHOD"));
        Assert.Equal($"{meeting.Id}@diten", inviteUid);
        Assert.Equal(inviteUid, SameIcsProperty(changes, "UID"));
        Assert.True(
            int.Parse(changeSequence) > int.Parse(inviteSequence),
            $"change SEQUENCE {changeSequence} must be higher than invite SEQUENCE {inviteSequence}");
        Assert.Equal(newStart.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'"), SameIcsProperty(changes, "DTSTART"));
    }

    [Fact]
    public async Task Cancelling_after_invites_sends_every_participant_a_CANCEL_with_the_same_UID_and_STATUS_CANCELLED()
    {
        var meeting = await OrganizerCreatesMeetingAsync();
        var invites = AssertPerRecipient(_dispatches.Requests, InviteEvent, [_alice, _bob], EmailOf);

        var cancel = await new CancelMeetingHandler(
                _meetings, _types, _attendees, new FakeCurrentUserContext(_organizer), Mailer())
            .Handle(
                new CancelMeetingCommand(meeting.Id, new CancelMeetingRequest("Not needed", meeting.Version), "corr"),
                CancellationToken.None);

        Assert.True(cancel.IsSuccessful);
        var cancellations = AssertPerRecipient(_dispatches.Requests, CancelEvent, [_alice, _bob], EmailOf);
        // The organizer cancelled it themselves: no mail about their own action, under ANY event code.
        Assert.DoesNotContain(_dispatches.Requests, r => r.EventCode == OrganizerCancelledEvent);
        Assert.DoesNotContain(_dispatches.Requests, r => r.To.Any(t => t.Email == EmailOf(_organizer)));
        Assert.All(cancellations, c =>
            Assert.Equal("text/calendar; charset=utf-8; method=CANCEL", Assert.Single(c.Attachments!).ContentType));

        Assert.Equal("CANCEL", SameIcsProperty(cancellations, "METHOD"));
        Assert.Equal("CANCELLED", SameIcsProperty(cancellations, "STATUS"));
        Assert.Equal(SameIcsProperty(invites, "UID"), SameIcsProperty(cancellations, "UID"));
        Assert.True(int.Parse(SameIcsProperty(cancellations, "SEQUENCE")) > int.Parse(SameIcsProperty(invites, "SEQUENCE")));
    }

    // ── C — BL-386: the removed attendee's own calendar entry is withdrawn, nobody else's is touched ──────────

    [Fact]
    public async Task Removing_Bob_sends_ONLY_Bob_a_removed_CANCEL_with_the_invites_own_UID_and_a_higher_SEQUENCE()
    {
        var meeting = await OrganizerCreatesMeetingAsync();
        var invites = AssertPerRecipient(_dispatches.Requests, InviteEvent, [_alice, _bob], EmailOf);

        var remove = await RemoveAttendeeAsync(_organizer, meeting.Id, _bob);

        Assert.True(remove.IsSuccessful, string.Join(" | ", remove.Errors));
        var removed = AssertPerRecipient(_dispatches.Requests, RemovedEvent, [_bob], EmailOf);

        Assert.Equal("CANCEL", SameIcsProperty(removed, "METHOD"));
        Assert.Equal("CANCELLED", SameIcsProperty(removed, "STATUS"));
        Assert.Equal(SameIcsProperty(invites, "UID"), SameIcsProperty(removed, "UID"));
        Assert.True(int.Parse(SameIcsProperty(removed, "SEQUENCE")) > int.Parse(SameIcsProperty(invites, "SEQUENCE")));

        var view = await OrganizerViewAsync(meeting.Id);
        Assert.DoesNotContain(view.Attendees, a => a.UserId == _bob);
        Assert.DoesNotContain(_dispatches.Requests, r => r.EventCode == RemovedEvent && RecipientEmails(r).Contains(EmailOf(_alice)));
        // The organizer removed Bob themselves: no mail to the organizer about it, under ANY event code.
        Assert.DoesNotContain(_dispatches.Requests, r => r.To.Any(t => t.Email == EmailOf(_organizer)));
    }

    [Fact]
    public async Task The_organizers_own_attendee_row_cannot_be_removed()
    {
        var meeting = await OrganizerCreatesMeetingAsync();

        var remove = await RemoveAttendeeAsync(_alice, meeting.Id, _organizer);

        Assert.False(remove.IsSuccessful);
        Assert.Equal(409, remove.StatusCode);
        Assert.Equal(MeetingReasonCodes.AttendeeIsOrganizer, remove.ReasonCode);
        Assert.DoesNotContain(_dispatches.Requests, r => r.EventCode == RemovedEvent);
        Assert.Contains((await OrganizerViewAsync(meeting.Id)).Attendees, a => a.UserId == _organizer);
    }

    [Fact]
    public async Task Removing_an_attendee_from_a_cancelled_meeting_is_refused_and_sends_no_removed_mail()
    {
        var meeting = await OrganizerCreatesMeetingAsync();
        var cancel = await new CancelMeetingHandler(
                _meetings, _types, _attendees, new FakeCurrentUserContext(_organizer), Mailer())
            .Handle(new CancelMeetingCommand(meeting.Id, new CancelMeetingRequest("Not needed", meeting.Version), "corr"),
                CancellationToken.None);
        Assert.True(cancel.IsSuccessful);
        _dispatches.Requests.Clear();

        var remove = await RemoveAttendeeAsync(_organizer, meeting.Id, _bob);

        Assert.False(remove.IsSuccessful);
        Assert.Equal(409, remove.StatusCode);
        Assert.Equal(MeetingReasonCodes.Cancelled, remove.ReasonCode);
        Assert.Empty(_dispatches.Requests);
    }

    [Fact]
    public async Task An_attendee_who_removes_themselves_gets_no_removed_mail_about_their_own_action()
    {
        var meeting = await OrganizerCreatesMeetingAsync();

        var remove = await RemoveAttendeeAsync(_bob, meeting.Id, _bob);

        Assert.True(remove.IsSuccessful, string.Join(" | ", remove.Errors));
        Assert.DoesNotContain(_dispatches.Requests, r => r.EventCode == RemovedEvent);
        Assert.DoesNotContain((await OrganizerViewAsync(meeting.Id)).Attendees, a => a.UserId == _bob);
    }

    private Task<Response<NoContent>> RemoveAttendeeAsync(Guid actingUserId, Guid meetingId, Guid removedUserId)
        => new RemoveMeetingAttendeeHandler(_meetings, _types, _attendees, new FakeCurrentUserContext(actingUserId), Mailer())
            .Handle(new RemoveMeetingAttendeeCommand(meetingId, removedUserId, "corr"), CancellationToken.None);

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

    // ── E — BL-387 (owner, 2026-09-14): the organizer's own "your meeting" mail, never the plain invite copy,
    // whenever someone else performed the action — and the ONLY moment a reassignment mails anyone at all ────────

    [Fact]
    public async Task A_time_change_made_by_someone_other_than_the_organizer_mails_the_organizer_the_organizer_variant_and_the_attendee_the_normal_one()
    {
        var meeting = await OrganizerCreatesMeetingAsync();
        var newStart = meeting.StartAt.AddHours(2);

        // Bob — an ordinary attendee, not the organizer — moves the meeting.
        var update = await new UpdateMeetingHandler(
                _meetings, _types, _attendees, new FakeCurrentUserContext(_bob), Mailer())
            .Handle(
                new UpdateMeetingCommand(
                    meeting.Id,
                    new UpdateMeetingRequest(meeting.Title, meeting.MeetingTypeId, newStart, newStart.AddHours(1),
                        meeting.Location, meeting.Description, meeting.Version),
                    "corr"),
                CancellationToken.None);

        Assert.True(update.IsSuccessful, string.Join(" | ", update.Errors));
        // Bob is the actor, excluded from both groups; Alice gets the ordinary change mail.
        var change = Assert.Single(_dispatches.Requests, r => r.EventCode == ChangeEvent);
        Assert.Equal([EmailOf(_alice)], RecipientEmails(change));
        // The organizer did not make this change, so they get their OWN wording, not the attendee copy.
        var organizerUpdated = Assert.Single(_dispatches.Requests, r => r.EventCode == OrganizerUpdatedEvent);
        Assert.Equal([EmailOf(_organizer)], RecipientEmails(organizerUpdated));
        // Same meeting, same .ics identity — only the template differs.
        Assert.Equal(Property(IcsOf(change), "UID"), Property(IcsOf(organizerUpdated), "UID"));
        Assert.Equal(Property(IcsOf(change), "SEQUENCE"), Property(IcsOf(organizerUpdated), "SEQUENCE"));
    }

    [Fact]
    public async Task A_cancellation_made_by_someone_other_than_the_organizer_mails_the_organizer_the_organizer_variant()
    {
        var meeting = await OrganizerCreatesMeetingAsync();

        var cancel = await new CancelMeetingHandler(
                _meetings, _types, _attendees, new FakeCurrentUserContext(_bob), Mailer())
            .Handle(new CancelMeetingCommand(meeting.Id, new CancelMeetingRequest("Not needed", meeting.Version), "corr"),
                CancellationToken.None);

        Assert.True(cancel.IsSuccessful, string.Join(" | ", cancel.Errors));
        var cancellation = Assert.Single(_dispatches.Requests, r => r.EventCode == CancelEvent);
        Assert.Equal([EmailOf(_alice)], RecipientEmails(cancellation));
        var organizerCancelled = Assert.Single(_dispatches.Requests, r => r.EventCode == OrganizerCancelledEvent);
        Assert.Equal([EmailOf(_organizer)], RecipientEmails(organizerCancelled));
    }

    [Fact]
    public async Task Reassigning_the_organizer_to_someone_other_than_the_actor_mails_only_the_new_organizer_an_added_to_calendar_mail()
    {
        var meeting = await OrganizerCreatesMeetingAsync();

        // Bob reassigns the meeting to Alice.
        var reassign = await ReassignAsync(_bob, meeting.Id, _alice, meeting.Version);

        Assert.True(reassign.IsSuccessful, string.Join(" | ", reassign.Errors));
        // Nobody else is mailed by a reassignment on its own — not the previous organizer, not Bob, not a
        // change/cancel event of any kind.
        var organizerAdded = Assert.Single(_dispatches.Requests, r => r.EventCode == OrganizerAddedEvent);
        Assert.Equal([EmailOf(_alice)], RecipientEmails(organizerAdded));
        Assert.DoesNotContain(_dispatches.Requests, r => r.EventCode is ChangeEvent or CancelEvent or OrganizerUpdatedEvent or OrganizerCancelledEvent);
        var ics = IcsOf(organizerAdded);
        Assert.Equal($"{meeting.Id}@diten", Property(ics, "UID"));
        Assert.Equal("REQUEST", Property(ics, "METHOD"));
    }

    [Fact]
    public async Task Reassigning_the_organizer_to_the_actors_own_user_id_sends_no_mail()
    {
        var meeting = await OrganizerCreatesMeetingAsync();

        // Bob reassigns the meeting to HIMSELF.
        var reassign = await ReassignAsync(_bob, meeting.Id, _bob, meeting.Version);

        Assert.True(reassign.IsSuccessful, string.Join(" | ", reassign.Errors));
        Assert.DoesNotContain(_dispatches.Requests, r => r.EventCode is OrganizerAddedEvent or ChangeEvent or CancelEvent);
    }

    /// <summary>The rule reads the CURRENT organizer, not whoever created the meeting: after a reassignment a
    /// later change mails the NEW organizer their own variant and the PREVIOUS one the ordinary attendee copy —
    /// the previous organizer's own attendee row was never touched (YAPMA), only what the mailer reads changed.</summary>
    [Fact]
    public async Task After_the_organizer_is_reassigned_a_time_change_mails_the_new_organizer_the_organizer_variant_and_the_previous_one_the_normal_one()
    {
        var meeting = await OrganizerCreatesMeetingAsync();
        var reassign = await ReassignAsync(_organizer, meeting.Id, _alice, meeting.Version);
        Assert.True(reassign.IsSuccessful, string.Join(" | ", reassign.Errors));
        _dispatches.Requests.Clear();

        // Bob (still an ordinary attendee) moves the meeting.
        var current = (await _meetings.GetByIdAsync(meeting.Id, CancellationToken.None))!;
        var newStart = current.StartAt.AddHours(2);
        var update = await new UpdateMeetingHandler(
                _meetings, _types, _attendees, new FakeCurrentUserContext(_bob), Mailer())
            .Handle(
                new UpdateMeetingCommand(
                    meeting.Id,
                    new UpdateMeetingRequest(current.Title, current.MeetingTypeId, newStart, newStart.AddHours(1),
                        current.Location, current.Description, current.Version),
                    "corr"),
                CancellationToken.None);

        Assert.True(update.IsSuccessful, string.Join(" | ", update.Errors));
        // Alice (the NEW organizer) gets her own variant — she did not make this change.
        var organizerUpdated = Assert.Single(_dispatches.Requests, r => r.EventCode == OrganizerUpdatedEvent);
        Assert.Equal([EmailOf(_alice)], RecipientEmails(organizerUpdated));
        // The PREVIOUS organizer is an ordinary attendee now — the normal change mail, alongside anyone else.
        var change = Assert.Single(_dispatches.Requests, r => r.EventCode == ChangeEvent);
        Assert.Equal([EmailOf(_organizer)], RecipientEmails(change));
    }

    private Task<Response<NoContent>> ReassignAsync(Guid actingUserId, Guid meetingId, Guid newOrganizerUserId, int expectedVersion)
        => new ReassignMeetingOrganizerHandler(_meetings, _types, new FakeCurrentUserContext(actingUserId), _eligibility, Mailer())
            .Handle(
                new ReassignMeetingOrganizerCommand(meetingId, new ReassignMeetingOrganizerRequest(newOrganizerUserId, expectedVersion), "corr"),
                CancellationToken.None);

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
