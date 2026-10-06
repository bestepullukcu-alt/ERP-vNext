using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Notifications.Validators;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 fix round 5 — the blocker. A background job runs outside any request: NO tenant is resolved. The meeting
/// stores the permanent-failure path writes to (attendee "not delivered" badge, organizer notification) read the
/// AMBIENT tenant and throw when there is none; the effects swallowed that, so a meeting invite closed by the retry
/// window or by its last retry told nobody. Measured here with the REAL meeting / attendee / notification repositories
/// over a REAL MongoDB and an UNRESOLVED TenantContext (not the harness's ready-made one), for two tenants at once.
/// </summary>
public sealed class NotificationDispatchTenantScopeMongoTests : IAsyncLifetime
{
    private const int MaxRetryCount = 3;

    private MongoIntegrationHarness _harness = null!;
    private NotificationDispatchRepository _dispatches = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync("email_shell_tenant_scope", SchemaProfile.Notification, SchemaProfile.Meetings);
        _dispatches = new NotificationDispatchRepository(_harness.DbContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task The_window_close_of_a_meeting_invite_badges_the_attendee_and_tells_the_organizer_each_in_its_own_tenant()
    {
        var ambient = new TenantContext(); // what a Hangfire job has: nothing resolved
        var first = await SeedMeetingInviteAsync(queuedHoursAgo: 30);
        var second = await SeedMeetingInviteAsync(queuedHoursAgo: 30);
        Assert.False(ambient.IsResolved);

        var sweep = TestSweeps.Create(
            _dispatches,
            new NothingScheduled(),
            NullLogger<EmailDispatchSweepJob>.Instance,
            new Pipeline(_harness, ambient),
            Options.Create(new EmailDispatchRetentionOptions { RetryWindowHours = 24 }),
            ambient);

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

        await AssertToldAsync(first);
        await AssertToldAsync(second);
        Assert.False(ambient.IsResolved); // the scope is closed again after each row
    }

    [Fact]
    public async Task The_last_failed_retry_of_a_meeting_invite_badges_the_attendee_and_tells_the_organizer()
    {
        var ambient = new TenantContext();
        var invite = await SeedMeetingInviteAsync(queuedHoursAgo: 1, retryCount: MaxRetryCount - 1);

        var job = new EmailDispatchJob(
            _dispatches,
            new FixedSettings(),
            new OneProvider(),
            new Pipeline(_harness, ambient),
            NullLogger<EmailDispatchJob>.Instance,
            tenantContext: ambient);

        await job.HandleAsync(new EmailDispatchJobArgs(invite.TenantId, invite.DispatchId, MaxRetryCount), new BackgroundJobContext(), CancellationToken.None);

        var stored = (await _dispatches.GetByIdForTenantAsync(invite.TenantId, invite.DispatchId))!;
        Assert.NotNull(stored.PermanentlyFailedNotifiedAt);
        await AssertToldAsync(invite);
    }

    private sealed record Invite(Guid TenantId, Guid DispatchId, Guid MeetingId, Guid OrganizerId, Guid AttendeeId);

    private async Task<Invite> SeedMeetingInviteAsync(int queuedHoursAgo, int retryCount = 1)
    {
        var tenantId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();
        var attendeeId = Guid.NewGuid();
        var seeding = new TenantContext();
        Meeting meeting;
        using (TenantScope.Begin(seeding, tenantId))
        {
            meeting = await new MeetingRepository(_harness.DbContext, seeding).CreateAsync(new Meeting
            {
                TenantId = tenantId,
                Title = "Quarterly review",
                MeetingTypeId = Guid.NewGuid(),
                StartAt = DateTimeOffset.UtcNow.AddDays(1),
                EndAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                OrganizerUserId = organizerId,
                IdempotencyKey = Guid.NewGuid().ToString("N")
            });
            await new MeetingAttendeeRepository(_harness.DbContext, seeding).CreateAsync(new MeetingAttendee
            {
                TenantId = tenantId,
                MeetingId = meeting.Id,
                UserId = attendeeId
            });
        }

        var dispatch = new NotificationDispatch
        {
            TenantId = tenantId,
            TemplateKey = "platform.meetings.invite",
            Locale = "en",
            Channel = NotificationChannelCode.Email,
            ProviderCode = MessagingProviderCode.Fake,
            Status = NotificationDispatchStatus.Failed,
            To = [new EmailRecipient { Email = "attendee@example.test", DisplayName = "Attendee" }],
            Subject = "Invitation",
            VariablesJson = "{\"MeetingTitle\":\"Quarterly review\"}",
            QueuedAt = DateTimeOffset.UtcNow.AddHours(-queuedHoursAgo),
            RetryCount = retryCount,
            NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            ErrorCode = "SMTP_TIMEOUT",
            CausationId = meeting.Id,
            MeetingAttendeeUserId = attendeeId
        };
        await _dispatches.CreateAsync(dispatch);
        return new Invite(tenantId, dispatch.Id, meeting.Id, organizerId, attendeeId);
    }

    private async Task AssertToldAsync(Invite invite)
    {
        var reading = new TenantContext();
        using (TenantScope.Begin(reading, invite.TenantId))
        {
            var attendee = await new MeetingAttendeeRepository(_harness.DbContext, reading).FindAsync(invite.MeetingId, invite.AttendeeId);
            Assert.NotNull(attendee!.MailUndeliveredAt);
        }

        var notices = await new UserNotificationRepository(_harness.DbContext).ListForUserAsync(invite.TenantId, invite.OrganizerId, 0, 10);
        var notice = Assert.Single(notices);
        Assert.Equal(invite.TenantId, notice.TenantId);
    }

    /// <summary>
    /// The two transition commands as production runs them: the production validator in the production
    /// ValidationBehavior, then the real handler over REAL repositories bound to the job's ambient tenant context.
    /// </summary>
    private sealed class Pipeline(MongoIntegrationHarness harness, ITenantContext ambient) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var dispatches = new NotificationDispatchRepository(harness.DbContext);
            object response = request switch
            {
                MarkNotificationDispatchFailedCommand failed => await new ValidationBehavior<MarkNotificationDispatchFailedCommand, Response<NotificationDispatchDto>>(
                        [new MarkNotificationDispatchFailedValidator()])
                    .Handle(failed, () => new MarkNotificationDispatchFailedHandler(
                            dispatches,
                            new NotificationsSmtpIntegrationTests.RecordingEventBus(),
                            NullLogger<MarkNotificationDispatchFailedHandler>.Instance,
                            new MeetingRepository(harness.DbContext, ambient),
                            new MeetingAttendeeRepository(harness.DbContext, ambient),
                            new UserNotificationRepository(harness.DbContext))
                        .Handle(failed, cancellationToken), cancellationToken),
                _ => throw new NotSupportedException(request.GetType().Name)
            };
            return (TResponse)response;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class FixedSettings : ITenantMessagingSettingsResolver
    {
        public Task<Response<ResolvedMessagingSettingsDto>> ResolveAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Response<ResolvedMessagingSettingsDto>.Success(
                new ResolvedMessagingSettingsDto(
                    Guid.NewGuid(), tenantId, tenantId, false, MessagingProviderCode.Fake.ToString(),
                    "sender@example.test", "Sender", null, true, NotificationFallbackPolicy.UsePlatformDefault.ToString())));
    }

    /// <summary>A provider that is always down.</summary>
    private sealed class OneProvider : IMessagingProviderResolver, IMessagingProvider
    {
        public MessagingProviderCode ProviderCode => MessagingProviderCode.Fake;

        public Response<IMessagingProvider> Resolve(MessagingProviderCode providerCode) => Response<IMessagingProvider>.Success(this);

        public Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default) =>
            Task.FromResult(MessagingProviderResult.Fail("SMTP_TIMEOUT", "smtp down"));
    }

    private sealed class NothingScheduled : IBackgroundJobScheduler
    {
        public Task<string> EnqueueAsync<TArgs, THandler>(TArgs args, BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : IBackgroundJobHandler<TArgs> => Task.FromResult("job");

        public Task<string> ScheduleAsync<TArgs, THandler>(TArgs args, DateTimeOffset enqueueAtUtc, BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : IBackgroundJobHandler<TArgs> => Task.FromResult("job");

        public Task RegisterRecurringAsync(RecurringJobRegistration registration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
