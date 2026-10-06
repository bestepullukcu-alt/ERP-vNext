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
/// K12, against the REAL <c>MeetingInviteMailer</c> (Infrastructure) rather than the fake handler-level double —
/// this is where the "never throws" rule is actually implemented, so this is where its mutation guard lives.
/// </summary>
public sealed class MeetingInviteMailerNeverThrowsTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly Guid Organizer = Guid.NewGuid();
    private static readonly Guid Invitee = Guid.NewGuid();

    private sealed class ThrowingDispatchAdapter : INotificationEventDispatchAdapter
    {
        public Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(
            NotificationEventDispatchRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException("the notification pipeline blew up");
    }

    private sealed class FailingDispatchAdapter : INotificationEventDispatchAdapter
    {
        public Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(
            NotificationEventDispatchRequest request, CancellationToken ct = default)
            => Task.FromResult(Response<NotificationDispatchDto>.Fail("no template", 404, "EVENT_NOT_FOUND"));
    }

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

    private static MeetingInviteMailer Mailer(INotificationEventDispatchAdapter adapter)
        => new(
            adapter,
            new FakeRecipientResolver(),
            new FakeTenantContext(Tenant),
            Options.Create(new AuthServiceOptions()),
            NullLogger<MeetingInviteMailer>.Instance);

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

    [Fact]
    public async Task A_throwing_dispatch_adapter_still_returns_a_result_never_an_exception()
    {
        var meeting = MakeMeeting();
        var mailer = Mailer(new ThrowingDispatchAdapter());

        var result = await mailer.SendInviteAsync(meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Invitee)], Organizer);

        Assert.False(result.Sent);
        Assert.True(result.Failed);
    }

    [Fact]
    public async Task A_dispatch_refusal_is_reported_as_Failed_not_thrown()
    {
        var meeting = MakeMeeting();
        var mailer = Mailer(new FailingDispatchAdapter());

        var result = await mailer.SendChangeAsync(meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Invitee)], Organizer);

        Assert.False(result.Sent);
        Assert.True(result.Failed);
        Assert.Equal("EVENT_NOT_FOUND", result.Reason);
    }

    [Fact]
    public async Task A_successful_dispatch_excludes_the_ACTOR_from_the_recipient_list()
    {
        var meeting = MakeMeeting();
        var adapter = new SucceedingDispatchAdapter();
        var mailer = Mailer(adapter);

        // The organizer is BOTH a recipient row (their own Accepted attendance) AND the actor creating it.
        var result = await mailer.SendInviteAsync(
            meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Organizer), MakeAttendee(meeting.Id, Invitee)], actingUserId: Organizer);

        Assert.True(result.Sent);
        Assert.False(result.Failed);
        Assert.NotNull(adapter.LastRequest);
        Assert.Single(adapter.LastRequest!.To);
        Assert.DoesNotContain(adapter.LastRequest.To, r => r.Email == $"{Organizer}@example.test");
    }

    [Fact]
    public async Task No_recipients_other_than_the_actor_is_reported_as_no_delivery_not_a_failure()
    {
        var meeting = MakeMeeting();
        var mailer = Mailer(new SucceedingDispatchAdapter());

        // Only the organizer's own row — and the organizer IS the actor.
        var result = await mailer.SendInviteAsync(meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Organizer)], actingUserId: Organizer);

        Assert.False(result.Sent);
        Assert.False(result.Failed);
    }

    [Fact]
    public async Task The_absolute_meeting_url_carries_the_configured_frontend_origin()
    {
        var meeting = MakeMeeting();
        var adapter = new SucceedingDispatchAdapter();
        var options = Options.Create(new AuthServiceOptions { FrontendBaseUrl = "https://tenant.example.test" });
        var mailer = new MeetingInviteMailer(
            adapter, new FakeRecipientResolver(), new FakeTenantContext(Tenant), options, NullLogger<MeetingInviteMailer>.Instance);

        await mailer.SendInviteAsync(meeting, "MGMT-REVIEW", [MakeAttendee(meeting.Id, Invitee)], Organizer);

        Assert.Equal($"https://tenant.example.test/Meetings/{meeting.Id}", adapter.LastRequest!.Variables["MeetingUrl"]);
    }
}
