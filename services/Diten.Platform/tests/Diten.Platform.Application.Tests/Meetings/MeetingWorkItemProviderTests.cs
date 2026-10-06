using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.Providers;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using MediatR;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S5c (WP-MG-MOD0357-S5C-INVITE-CARD-01) — the read half (<see cref="MeetingWorkItemProvider"/>) and
/// write half (<see cref="MeetingWorkItemActionDispatcher"/>) of the Task Center invite card.
///
/// <para><b>K5 (BL-026) is proven here structurally, not by inspecting a flag</b>: the provider's own source
/// query is <c>ListPendingByUserIdAsync</c>, so an attendee who has answered simply stops being returned by the
/// fake repository — the same mechanism the real Mongo repository uses. <see cref="Accepted_invitation_never_appears"/>
/// and <see cref="Declined_invitation_never_appears"/> are the red→green proof: comment out the
/// <c>InvitationResponse == Pending</c> filter in <c>FakeMeetingAttendeeRepository.ListPendingByUserIdAsync</c>
/// (MeetingS2TestDoubles.cs) and both go red.</para>
/// </summary>
public sealed class MeetingWorkItemProviderTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly Guid Organizer = Guid.NewGuid();
    private static readonly Guid Invitee = Guid.NewGuid();

    private sealed class Fixture
    {
        public FakeMeetingRepository Meetings { get; } = new() { Tenant = Tenant };
        public FakeMeetingTypeRepository Types { get; } = new() { Tenant = Tenant };
        public FakeMeetingAttendeeRepository Attendees { get; } = new() { Tenant = Tenant };
        public FakeUserDisplayNameResolver DisplayNames { get; } = new((Organizer, "Ayşe Yılmaz"));

        public MeetingWorkItemProvider Provider()
            => new(Attendees, Meetings, Types, DisplayNames, SlaForTests.Real());

        public MeetingType SeedType()
        {
            var type = new MeetingType { TenantId = Tenant, Name = "MGMT-REVIEW " + Guid.NewGuid() };
            Types.Seed(type);
            return type;
        }

        public Meeting SeedMeeting(
            Guid typeId,
            MeetingLifecycle lifecycle = MeetingLifecycle.Scheduled,
            DateTimeOffset? startAt = null)
        {
            var start = startAt ?? DateTimeOffset.UtcNow.AddDays(1);
            var meeting = new Meeting
            {
                TenantId = Tenant, Title = "Aylık Değerlendirme", MeetingTypeId = typeId,
                StartAt = start, EndAt = start.AddHours(1),
                OrganizerUserId = Organizer, IdempotencyKey = Guid.NewGuid().ToString(),
                Lifecycle = lifecycle
            };
            Meetings.Seed(meeting);
            return meeting;
        }

        public MeetingAttendee SeedAttendee(
            Guid meetingId, Guid userId, InvitationResponse response = InvitationResponse.Pending)
        {
            var attendee = new MeetingAttendee
            {
                TenantId = Tenant, MeetingId = meetingId, UserId = userId,
                InvitationResponse = response, CreatedBy = "test"
            };
            Attendees.Seed(attendee);
            return attendee;
        }

        public WorkItemActor Actor(bool platform = false, params string[] granted)
            => new(Invitee, platform, new HashSet<string>(granted, StringComparer.Ordinal));
    }

    // ── K5 (BL-026) — never enters İşlerim ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pending_invitation_appears_in_the_provider_output()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee);

        var items = await fx.Provider().GetWorkItemsAsync(fx.Actor(platform: true), CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal(WorkItemContract.IntentMeetingInvite, item.WorkIntent);
        Assert.Equal(WorkItemContract.ProviderCodeMeetings, item.Source.ProviderCode);
        Assert.Equal("acceptInvite", item.PrimaryActionCode);
        Assert.Equal(["declineInvite"], item.SecondaryActionCodes);
        Assert.Equal(2, item.Actions.Count);
        Assert.Contains(item.Actions, a => a.Code == "acceptInvite");
        Assert.Contains(item.Actions, a => a.Code == "declineInvite");
        Assert.Equal($"/Meetings/{meeting.Id}", item.Source.DeepLink);
        Assert.True(item.Assignee.IsCurrentUser);
        Assert.False(item.Requester.IsCurrentUser);
    }

    [Fact]
    public async Task Accepted_invitation_never_appears()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee, InvitationResponse.Accepted);

        var items = await fx.Provider().GetWorkItemsAsync(fx.Actor(platform: true), CancellationToken.None);

        Assert.Empty(items);
    }

    [Fact]
    public async Task Declined_invitation_never_appears()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee, InvitationResponse.Declined);

        var items = await fx.Provider().GetWorkItemsAsync(fx.Actor(platform: true), CancellationToken.None);

        Assert.Empty(items);
    }

    /// <summary>
    /// Red→green proof (a) from the pack: the ONLY thing keeping an accepted invite off the board is this
    /// query's own `Pending` filter, exercised end to end through the real <see cref="RespondToInvitationHandler"/>
    /// rather than by seeding the fake with a pre-set status — so this test would also catch the handler
    /// writing the wrong enum value.
    /// </summary>
    [Fact]
    public async Task Responding_Accept_removes_the_item_from_the_next_read()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee);

        var before = await fx.Provider().GetWorkItemsAsync(fx.Actor(platform: true), CancellationToken.None);
        Assert.Single(before);

        var handler = new RespondToInvitationHandler(fx.Meetings, fx.Attendees, new FakeCurrentUserContext(Invitee));
        var response = await handler.Handle(
            new RespondToInvitationCommand(meeting.Id, new RespondToInvitationRequest("Accept"), "corr"), CancellationToken.None);
        Assert.True(response.IsSuccessful);

        var after = await fx.Provider().GetWorkItemsAsync(fx.Actor(platform: true), CancellationToken.None);
        Assert.Empty(after);
    }

    // ── Provider excludes dead invitations even while still Pending ────────────────────────────────────────

    [Fact]
    public async Task A_cancelled_meetings_pending_invitation_is_excluded()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id, MeetingLifecycle.Cancelled);
        fx.SeedAttendee(meeting.Id, Invitee);

        var items = await fx.Provider().GetWorkItemsAsync(fx.Actor(platform: true), CancellationToken.None);

        Assert.Empty(items);
    }

    [Fact]
    public async Task A_meeting_already_in_the_past_is_excluded()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id, startAt: DateTimeOffset.UtcNow.AddDays(-3));
        fx.SeedAttendee(meeting.Id, Invitee);

        var items = await fx.Provider().GetWorkItemsAsync(fx.Actor(platform: true), CancellationToken.None);

        Assert.Empty(items);
    }

    // ── Action gating follows the SAME permission the provider declares ───────────────────────────────────

    [Fact]
    public async Task An_actor_without_the_read_permission_gets_both_actions_disabled_with_a_reason()
    {
        var fx = new Fixture();
        var type = fx.SeedType();
        var meeting = fx.SeedMeeting(type.Id);
        fx.SeedAttendee(meeting.Id, Invitee);

        var items = await fx.Provider().GetWorkItemsAsync(fx.Actor(platform: false), CancellationToken.None);

        var item = Assert.Single(items);
        Assert.All(item.Actions, a =>
        {
            Assert.False(a.Enabled);
            Assert.Equal(WorkAggregationReasonCodes.PermissionDenied, a.DisabledReasonCode);
        });
    }
}

/// <summary>
/// The write half. <see cref="Dispatcher_forwards_to_the_same_command_S5_wired"/> and
/// <see cref="A_non_attendees_refusal_survives_the_dispatch_untouched"/> are the AC3 boundary proof: the
/// dispatcher decides nothing about WHO may respond — it only names the action and forwards, exactly like every
/// other <see cref="IWorkItemActionDispatcher"/> in this codebase.
/// </summary>
public sealed class MeetingWorkItemActionDispatcherTests
{
    [Fact]
    public void Supports_exactly_the_two_actions_the_provider_projects()
    {
        var dispatcher = new MeetingWorkItemActionDispatcher(new RecordingMediator());

        Assert.True(dispatcher.CanDispatch("acceptInvite"));
        Assert.True(dispatcher.CanDispatch("declineInvite"));
        Assert.False(dispatcher.CanDispatch("acceptMeeting"));
        Assert.False(dispatcher.CanDispatch(""));
    }

    [Fact]
    public void Required_permission_is_the_same_key_the_provider_declares()
    {
        var dispatcher = new MeetingWorkItemActionDispatcher(new RecordingMediator());
        var provider = new MeetingWorkItemProvider(null!, null!, null!, null!, null!);

        foreach (var code in dispatcher.SupportedActionCodes)
        {
            var key = dispatcher.RequiredPermission(code);
            Assert.False(string.IsNullOrWhiteSpace(key));
            Assert.Contains(key!, provider.RequiredActionPermissions);
        }
    }

    [Theory]
    [InlineData("acceptInvite", "Accept")]
    [InlineData("declineInvite", "Decline")]
    public async Task Dispatcher_forwards_to_the_same_command_S5_wired(string actionCode, string expectedResponse)
    {
        var mediator = new RecordingMediator();
        var dispatcher = new MeetingWorkItemActionDispatcher(mediator);
        var itemId = Guid.NewGuid();

        var result = await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(
            itemId, actionCode, new WorkItemActionPayloadDto(), PlatformActor(), "corr"));

        Assert.True(result.IsSuccessful);
        var command = Assert.IsType<RespondToInvitationCommand>(Assert.Single(mediator.Sent));
        Assert.Equal(itemId, command.MeetingId);
        Assert.Equal(expectedResponse, command.Request.Response);
    }

    [Fact]
    public async Task An_action_the_dispatcher_does_not_own_is_refused_by_code_and_sends_nothing()
    {
        var mediator = new RecordingMediator();
        var dispatcher = new MeetingWorkItemActionDispatcher(mediator);

        var result = await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(
            Guid.NewGuid(), "acceptMeeting", new WorkItemActionPayloadDto(), PlatformActor(), "corr"));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(WorkItemActionReasonCodes.ActionUnknown, result.ReasonCode);
        Assert.Empty(mediator.Sent);
    }

    /// <summary>
    /// The exact refusal §13:497 documents ("404, not 403: a caller with no row here never learns the meeting
    /// exists") must reach the caller UNCHANGED — the same guarantee <c>A_modules_refusal_code_survives_the_dispatch</c>
    /// proves for MOD-0024 in WorkItemActionDispatchTests.cs.
    /// </summary>
    [Fact]
    public async Task A_non_attendees_refusal_survives_the_dispatch_untouched()
    {
        var mediator = new RecordingMediator(
            Diten.Platform.Application.Common.Response<Diten.Platform.Application.Common.NoContent>.Fail(
                "This meeting has no invitation for you.", 404, MeetingReasonCodes.AttendeeNotFound, "corr"));
        var dispatcher = new MeetingWorkItemActionDispatcher(mediator);

        var result = await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(
            Guid.NewGuid(), "acceptInvite", new WorkItemActionPayloadDto(), PlatformActor(), "corr"));

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(MeetingReasonCodes.AttendeeNotFound, result.ReasonCode);
    }

    private static WorkItemActor PlatformActor() => new(Guid.NewGuid(), IsPlatformActor: true, new HashSet<string>());

    /// <summary>Same shape as WorkItemActionDispatchTests.RecordingMediator: proves ROUTING, not module behaviour.</summary>
    private sealed class RecordingMediator(object? answer = null) : IMediator
    {
        private readonly object? _answer = answer;

        public List<object> Sent { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            Sent.Add(request);
            if (_answer is TResponse typed)
            {
                return Task.FromResult(typed);
            }

            var successful = typeof(TResponse)
                .GetMethod("Success", [typeof(int), typeof(string)])!
                .Invoke(null, [200, "corr"]);
            return Task.FromResult((TResponse)successful!);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
