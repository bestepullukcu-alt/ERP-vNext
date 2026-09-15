using Diten.BuildingBlocks.BackgroundJobs;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Prometheus;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-406 — a meeting-related dispatch whose 5th retry exhausts its retries (RetryCount reaches
/// EmailDispatchJobArgs.MaxRetryCount) must tell the meeting's ORGANIZER, exactly once, and must never re-fire on
/// a later re-entry over the same terminal row. A non-meeting dispatch permanently failing gets the ops-only
/// log+counter but never an in-app notification. Real MongoDB throughout (this repo's own convention — see
/// EmailDispatchRetrySweepMongoTests, which this test file's shape deliberately mirrors); only the messaging
/// provider, the event bus and the Hangfire scheduler are doubled.
/// </summary>
public sealed class NotificationDispatchPermanentFailureMongoTests : IAsyncLifetime
{
    private const string MeetingTemplateKey = "platform.meetings.invite";
    private const string NonMeetingTemplateKey = "platform.tasks.assigned";
    private const int MaxRetryCount = 5;

    private MongoIntegrationHarness _harness = null!;

    public async Task InitializeAsync()
        => _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Notification, SchemaProfile.Meetings);

    public async Task DisposeAsync()
    {
        var tenant = _harness.TenantId;
        await _harness.Database.GetCollection<NotificationDispatch>(PlatformCollections.NotificationDispatches).DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.Database.GetCollection<NotificationTemplate>(PlatformCollections.NotificationTemplates).DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.Database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings).DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.Database.GetCollection<MeetingAttendee>(PlatformCollections.MeetingAttendees).DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.Database.GetCollection<UserNotification>(PlatformCollections.UserNotifications).DeleteManyAsync(x => x.TenantId == tenant);
        await _harness.DisposeAsync();
    }

    [Fact]
    public async Task Permanent_failure_of_a_meeting_dispatch_notifies_the_organizer_exactly_once_and_marks_the_attendee_undelivered()
    {
        var tenantId = _harness.TenantId;
        var organizerUserId = Guid.NewGuid();
        var attendeeUserId = Guid.NewGuid();

        var dispatches = new NotificationDispatchRepository(_harness.DbContext);
        var templates = new NotificationTemplateRepository(_harness.DbContext);
        var meetings = new MeetingRepository(_harness.DbContext, _harness.TenantContext);
        var attendees = new MeetingAttendeeRepository(_harness.DbContext, _harness.TenantContext);
        var userNotifications = new UserNotificationRepository(_harness.DbContext);

        await templates.CreateAsync(BuildTemplate(tenantId, MeetingTemplateKey));

        var meeting = await meetings.CreateAsync(new Meeting
        {
            TenantId = tenantId,
            Title = "Q3 Board Review",
            MeetingTypeId = Guid.NewGuid(),
            StartAt = DateTimeOffset.UtcNow.AddDays(1),
            EndAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            OrganizerUserId = organizerUserId,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        });
        await attendees.CreateAsync(new MeetingAttendee
        {
            TenantId = tenantId,
            MeetingId = meeting.Id,
            UserId = attendeeUserId
        });

        var provider = new AlwaysFailingProvider();
        var bus = new NoOpEventBus();

        // 1. The first, synchronous send fails — RetryCount stays 0, exactly as QueueEmailNotificationHandler
        //    already documents.
        var queueResult = await new QueueEmailNotificationHandler(
                new FixedSettingsResolver(), templates, new EmailTemplateRenderer(), dispatches,
                new SingleProviderResolver(provider), bus, NullLogger<QueueEmailNotificationHandler>.Instance)
            .Handle(
                new QueueEmailNotificationCommand(
                    tenantId,
                    new QueueEmailNotificationRequest(
                        TemplateKey: MeetingTemplateKey,
                        Locale: "en",
                        Variables: new Dictionary<string, object?> { ["MeetingTitle"] = meeting.Title },
                        To: [new EmailRecipientDto("attendee@example.test", "Ayşe Attendee")],
                        CausationId: meeting.Id,
                        MeetingAttendeeUserId: attendeeUserId),
                    "corr-permanent-failure"),
                CancellationToken.None);
        Assert.False(queueResult.IsSuccessful);
        var dispatchId = Assert.Single(await dispatches.ListByTenantAsync(tenantId)).Id;

        var job = BuildJob(dispatches, templates, provider, meetings, attendees, userNotifications, bus);
        var args = new EmailDispatchJobArgs(tenantId, dispatchId, MaxRetryCount);

        // 2-6. Five more failed attempts (RetryCount 0→1→2→3→4→5). The 5th of THESE (RetryCount reaching
        //      MaxRetryCount) is the permanent one.
        for (var i = 0; i < MaxRetryCount; i++)
        {
            await job.HandleAsync(args, new BackgroundJobContext(TenantId: tenantId), CancellationToken.None);
        }

        var afterPermanent = (await dispatches.GetByIdForTenantAsync(tenantId, dispatchId))!;
        Assert.Equal(MaxRetryCount, afterPermanent.RetryCount);
        Assert.NotNull(afterPermanent.PermanentlyFailedNotifiedAt);

        var notificationsForOrganizer = await userNotifications.ListForUserAsync(tenantId, organizerUserId, 0, 10);
        var one = Assert.Single(notificationsForOrganizer);
        Assert.Equal("platform.meetings.invite-undelivered", one.EventCode);
        Assert.Equal(meeting.Title, one.Title);
        Assert.Equal("Ayşe Attendee", one.Body);
        Assert.Contains(meeting.Id.ToString(), one.TargetUrl);

        var attendeeRow = await attendees.FindAsync(meeting.Id, attendeeUserId);
        Assert.NotNull(attendeeRow!.MailUndeliveredAt);

        // 6th (re-entrant) invocation over the ALREADY-terminal dispatch — Hangfire re-running the same job, or
        // a defensive re-check — must not create a second notification.
        await job.HandleAsync(args, new BackgroundJobContext(TenantId: tenantId), CancellationToken.None);
        var stillOne = await userNotifications.ListForUserAsync(tenantId, organizerUserId, 0, 10);
        Assert.Single(stillOne);
    }

    [Fact]
    public async Task Successful_delivery_never_creates_an_undelivered_notification_or_marks_the_attendee()
    {
        var tenantId = _harness.TenantId;
        var organizerUserId = Guid.NewGuid();
        var attendeeUserId = Guid.NewGuid();

        var dispatches = new NotificationDispatchRepository(_harness.DbContext);
        var templates = new NotificationTemplateRepository(_harness.DbContext);
        var meetings = new MeetingRepository(_harness.DbContext, _harness.TenantContext);
        var attendees = new MeetingAttendeeRepository(_harness.DbContext, _harness.TenantContext);
        var userNotifications = new UserNotificationRepository(_harness.DbContext);

        await templates.CreateAsync(BuildTemplate(tenantId, MeetingTemplateKey));
        var meeting = await meetings.CreateAsync(new Meeting
        {
            TenantId = tenantId,
            Title = "Weekly Sync",
            MeetingTypeId = Guid.NewGuid(),
            StartAt = DateTimeOffset.UtcNow.AddDays(1),
            EndAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            OrganizerUserId = organizerUserId,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        });
        await attendees.CreateAsync(new MeetingAttendee { TenantId = tenantId, MeetingId = meeting.Id, UserId = attendeeUserId });

        var provider = new AlwaysSucceedingProvider();
        var bus = new NoOpEventBus();

        var queueResult = await new QueueEmailNotificationHandler(
                new FixedSettingsResolver(), templates, new EmailTemplateRenderer(), dispatches,
                new SingleProviderResolver(provider), bus, NullLogger<QueueEmailNotificationHandler>.Instance)
            .Handle(
                new QueueEmailNotificationCommand(
                    tenantId,
                    new QueueEmailNotificationRequest(
                        TemplateKey: MeetingTemplateKey,
                        Locale: "en",
                        Variables: new Dictionary<string, object?> { ["MeetingTitle"] = meeting.Title },
                        To: [new EmailRecipientDto("attendee@example.test", "Attendee")],
                        CausationId: meeting.Id,
                        MeetingAttendeeUserId: attendeeUserId),
                    "corr-success"),
                CancellationToken.None);

        Assert.True(queueResult.IsSuccessful);
        var notifications = await userNotifications.ListForUserAsync(tenantId, organizerUserId, 0, 10);
        Assert.Empty(notifications);
        var attendeeRow = await attendees.FindAsync(meeting.Id, attendeeUserId);
        Assert.Null(attendeeRow!.MailUndeliveredAt);
    }

    [Fact]
    public async Task Permanent_failure_of_a_non_meeting_dispatch_increments_the_ops_counter_but_writes_no_notification()
    {
        var tenantId = _harness.TenantId;
        var dispatches = new NotificationDispatchRepository(_harness.DbContext);
        var templates = new NotificationTemplateRepository(_harness.DbContext);
        var meetings = new MeetingRepository(_harness.DbContext, _harness.TenantContext);
        var attendees = new MeetingAttendeeRepository(_harness.DbContext, _harness.TenantContext);
        var userNotifications = new UserNotificationRepository(_harness.DbContext);

        await templates.CreateAsync(BuildTemplate(tenantId, NonMeetingTemplateKey));

        var provider = new AlwaysFailingProvider();
        var bus = new NoOpEventBus();

        var queueResult = await new QueueEmailNotificationHandler(
                new FixedSettingsResolver(), templates, new EmailTemplateRenderer(), dispatches,
                new SingleProviderResolver(provider), bus, NullLogger<QueueEmailNotificationHandler>.Instance)
            .Handle(
                new QueueEmailNotificationCommand(
                    tenantId,
                    new QueueEmailNotificationRequest(
                        TemplateKey: NonMeetingTemplateKey,
                        Locale: "en",
                        Variables: new Dictionary<string, object?> { ["MeetingTitle"] = "n/a" },
                        To: [new EmailRecipientDto("someone@example.test", "Someone")]),
                    "corr-non-meeting"),
                CancellationToken.None);
        Assert.False(queueResult.IsSuccessful);
        var dispatchId = Assert.Single(await dispatches.ListByTenantAsync(tenantId)).Id;

        var counterBefore = PermanentlyFailedCounterValue(NonMeetingTemplateKey, isMeetingRelated: false);

        var job = BuildJob(dispatches, templates, provider, meetings, attendees, userNotifications, bus);
        var args = new EmailDispatchJobArgs(tenantId, dispatchId, MaxRetryCount);
        for (var i = 0; i < MaxRetryCount; i++)
        {
            await job.HandleAsync(args, new BackgroundJobContext(TenantId: tenantId), CancellationToken.None);
        }

        var afterPermanent = (await dispatches.GetByIdForTenantAsync(tenantId, dispatchId))!;
        Assert.NotNull(afterPermanent.PermanentlyFailedNotifiedAt);

        var counterAfter = PermanentlyFailedCounterValue(NonMeetingTemplateKey, isMeetingRelated: false);
        Assert.Equal(counterBefore + 1, counterAfter);

        // Nobody to notify — there is no meeting to attribute this dispatch to.
        var anyNotification = await _harness.Database.GetCollection<UserNotification>(PlatformCollections.UserNotifications)
            .Find(x => x.TenantId == tenantId).AnyAsync();
        Assert.False(anyNotification);
    }

    private static double PermanentlyFailedCounterValue(string templateKey, bool isMeetingRelated)
    {
        // Same registration call the handler itself makes (Metrics.CreateCounter is idempotent by name) —
        // this is how the test observes the ops-only counter without the handler exposing anything test-only.
        var counter = Metrics.CreateCounter(
            "notification_dispatch_permanently_failed",
            "Notification dispatches whose final retry attempt was exhausted with no further retry due.",
            new CounterConfiguration { LabelNames = new[] { "template_key", "is_meeting_related" } });
        return counter.WithLabels(templateKey, isMeetingRelated ? "true" : "false").Value;
    }

    private static NotificationTemplate BuildTemplate(Guid tenantId, string templateKey) => new()
    {
        TenantId = tenantId,
        IsPlatformDefault = false,
        TemplateKey = templateKey,
        Channel = NotificationChannelCode.Email,
        Locale = "en",
        SubjectTemplate = "Subject {{MeetingTitle}}",
        BodyHtmlTemplate = "<p>{{MeetingTitle}}</p>",
        BodyTextTemplate = "{{MeetingTitle}}",
        Status = NotificationTemplateStatus.Active,
        SemanticVersion = "1.0.0",
        Variables = []
    };

    private static EmailDispatchJob BuildJob(
        NotificationDispatchRepository dispatches,
        NotificationTemplateRepository templates,
        IMessagingProvider provider,
        MeetingRepository meetings,
        MeetingAttendeeRepository attendees,
        UserNotificationRepository userNotifications,
        IEventBus bus) =>
        new(
            dispatches, new FixedSettingsResolver(), new SingleProviderResolver(provider),
            new DirectMediator(
                new MarkNotificationDispatchSentHandler(dispatches, bus),
                new MarkNotificationDispatchFailedHandler(
                    dispatches, bus, NullLogger<MarkNotificationDispatchFailedHandler>.Instance,
                    meetings, attendees, userNotifications),
                new CancelNotificationDispatchHandler(dispatches, bus)),
            NullLogger<EmailDispatchJob>.Instance, templates, new EmailTemplateRenderer());

    // ── doubles ──────────────────────────────────────────────────────────────

    private sealed class AlwaysFailingProvider : IMessagingProvider
    {
        public List<MessagingProviderEmailRequest> Requests { get; } = [];
        public MessagingProviderCode ProviderCode => MessagingProviderCode.Fake;

        public Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(MessagingProviderResult.Fail("ProviderConnectivityFailed", "smtp down"));
        }
    }

    private sealed class AlwaysSucceedingProvider : IMessagingProvider
    {
        public List<MessagingProviderEmailRequest> Requests { get; } = [];
        public MessagingProviderCode ProviderCode => MessagingProviderCode.Fake;

        public Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(MessagingProviderResult.Success("sent-ok"));
        }
    }

    private sealed class FixedSettingsResolver : ITenantMessagingSettingsResolver
    {
        public Task<Response<ResolvedMessagingSettingsDto>> ResolveAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Response<ResolvedMessagingSettingsDto>.Success(
                new ResolvedMessagingSettingsDto(
                    Guid.NewGuid(), tenantId, tenantId, false, MessagingProviderCode.Fake.ToString(),
                    "sender@example.com", "Sender", null, true, NotificationFallbackPolicy.UsePlatformDefault.ToString())));
    }

    private sealed class SingleProviderResolver(IMessagingProvider provider) : IMessagingProviderResolver
    {
        public Response<IMessagingProvider> Resolve(MessagingProviderCode providerCode) =>
            providerCode == provider.ProviderCode
                ? Response<IMessagingProvider>.Success(provider)
                : Response<IMessagingProvider>.Fail("Provider unavailable.", 400);
    }

    private sealed class NoOpEventBus : IEventBus
    {
        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
            where TEvent : IIntegrationEvent => PublishAsync(@event, new EventPublishOptions(), ct);

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, EventPublishOptions options, CancellationToken ct = default)
            where TEvent : IIntegrationEvent =>
            Task.FromResult(new EventEnvelope<TEvent>(
                new EventMetadata(
                    options.EventId ?? Guid.NewGuid(), @event.EventName, @event.EventVersion,
                    options.CorrelationId ?? Guid.NewGuid(), options.CausationId, options.TenantId, "test",
                    options.OccurredAtUtc ?? DateTimeOffset.UtcNow),
                @event));
    }

    private sealed class DirectMediator(
        MarkNotificationDispatchSentHandler sent,
        MarkNotificationDispatchFailedHandler failed,
        CancelNotificationDispatchHandler cancel) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            request switch
            {
                MarkNotificationDispatchSentCommand cmd => (TResponse)(object)await sent.Handle(cmd, cancellationToken),
                MarkNotificationDispatchFailedCommand cmd => (TResponse)(object)await failed.Handle(cmd, cancellationToken),
                CancelNotificationDispatchCommand cmd => (TResponse)(object)await cancel.Handle(cmd, cancellationToken),
                _ => throw new NotSupportedException($"DirectMediator does not handle {request.GetType().Name}.")
            };

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
