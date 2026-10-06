using Diten.BuildingBlocks.BackgroundJobs;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>BL-454 — WP-EMAIL-SHELL-01 slice 2, stage A: the hardening deferred at slice 1's acceptance.</summary>
public sealed partial class EmailShellDispatchTests
{
    // ---------------------------------------------------------------- 2. the scope comes back when the body throws

    [Fact]
    public void A_tenant_scope_restores_the_previous_tenant_when_its_body_throws()
    {
        var context = new TenantContext();
        var outer = Guid.NewGuid();
        context.SetTenant(outer);

        Action body = () =>
        {
            using (TenantScope.Begin(context, Guid.NewGuid()))
            {
                throw new InvalidOperationException("the body failed");
            }
        };

        Assert.Throws<InvalidOperationException>(body);

        Assert.True(context.IsResolved);
        Assert.Equal(outer, context.TenantId);
    }

    [Fact]
    public async Task A_close_that_throws_in_one_tenant_leaves_the_next_row_in_its_own_tenant_and_no_tenant_behind()
    {
        var rig = new Rig();
        var first = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 31);
        var second = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 30);
        second.TenantId = Guid.NewGuid();
        var ambient = new TenantContext();
        var mediator = new TenantRecordingMediator(ambient, new ValidatingMediator(rig.Dispatches), throwFor: first.Id);
        var sweep = new EmailDispatchSweepJob(rig.Dispatches, new RecordingScheduler(), NullLogger<EmailDispatchSweepJob>.Instance,
            mediator, Options.Create(new EmailDispatchRetentionOptions { RetryWindowHours = 24 }), ambient);

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

        Assert.Equal([first.TenantId, second.TenantId], mediator.TenantsSeen);
        Assert.Null(first.PermanentlyFailedNotifiedAt);   // its close threw: still waiting, picked up next minute
        Assert.NotNull(second.PermanentlyFailedNotifiedAt);
        Assert.False(ambient.IsResolved);
    }

    // ---------------------------------------------------------------- 3. both close attempts refused

    [Fact]
    public async Task When_the_close_and_its_plain_retry_are_both_refused_the_row_waits_and_the_log_says_so()
    {
        var rig = new Rig();
        var row = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 30);
        var logger = new LinesLogger<EmailDispatchSweepJob>();
        var sweep = new EmailDispatchSweepJob(rig.Dispatches, new RecordingScheduler(), logger, new AlwaysRefusing(),
            Options.Create(new EmailDispatchRetentionOptions { RetryWindowHours = 24 }));

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

        Assert.Equal(NotificationDispatchStatus.Failed, row.Status);
        Assert.Null(row.PermanentlyFailedNotifiedAt);
        Assert.Contains("kept", row.VariablesJson);
        Assert.Contains(logger.Lines, line => line.Contains("expiry_refused", StringComparison.Ordinal));
        Assert.Contains(logger.Lines, line => line.Contains("expiry_failed", StringComparison.Ordinal) && line.Contains("ValidationException", StringComparison.Ordinal));
        Assert.Contains(logger.Lines, line => line.Contains("Closed=0", StringComparison.Ordinal) && line.Contains("NotClosed=1", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- 5. Google API keys and PEM private keys

    private static readonly string TestOnlyGoogleKey = "AI" + "za" + "TESTONLYtestonlyTESTONLYtestonly123";
    private static readonly string TestOnlyPem =
        "-----BEGIN " + "PRIVATE KEY-----\nTEST-ONLY-NOT-A-KEY-line-one\nTEST-ONLY-NOT-A-KEY-line-two\n-----END " + "PRIVATE KEY-----";

    [Fact]
    public void A_google_api_key_shape_and_a_pem_private_key_block_are_secrets_and_the_word_alone_is_not()
    {
        Assert.Equal(39, TestOnlyGoogleKey.Length);
        Assert.True(NotificationSecrets.IsSecretText(TestOnlyGoogleKey));
        Assert.True(NotificationSecrets.IsSecretText("key: " + TestOnlyGoogleKey + ";"));
        Assert.True(NotificationSecrets.IsSecretText(TestOnlyPem));
        Assert.True(NotificationSecrets.IsSecretText("-----BEGIN " + "RSA PRIVATE KEY-----\nTEST-ONLY"));

        Assert.False(NotificationSecrets.IsSecretText("AI" + "za is a name, not a key"));
        Assert.False(NotificationSecrets.IsSecretText("AI" + "zaShortValue"));
        Assert.False(NotificationSecrets.IsSecretText("-----BEGIN CERTIFICATE----- is public"));
    }

    [Theory]
    [InlineData("google")]
    [InlineData("pem")]
    public async Task A_key_in_a_variable_is_kept_out_of_the_stored_variables_subject_and_preview(string kind)
    {
        var secret = kind == "google" ? TestOnlyGoogleKey : TestOnlyPem;
        var rig = new Rig();
        rig.AddTemplate("en", "<p>Note: {{Note}}</p>", "Note: {{Note}}", subject: "Note {{Note}}");
        rig.Transport.SendThrow = new InvalidOperationException("smtp down"); // keep the variables to look at

        await rig.QueueAsync("en", new() { ["Note"] = secret });

        var dispatch = Assert.Single(rig.Dispatches.Items);
        var fragment = kind == "google" ? "TESTONLYtestonly" : "TEST-ONLY-NOT-A-KEY";
        Assert.DoesNotContain(fragment, dispatch.VariablesJson);
        Assert.DoesNotContain(fragment, dispatch.Subject);
        Assert.DoesNotContain(fragment, dispatch.BodyHtmlPreview);
        Assert.DoesNotContain(fragment, dispatch.BodyTextPreview);
    }

    // ---------------------------------------------------------------- 6. effects lost to a failed publish are re-driven

    [Fact]
    public async Task Effects_a_failed_publish_skipped_are_applied_once_by_the_next_sweep()
    {
        var rig = new Rig();
        var meetings = new MeetingDoubles();
        var key = UniqueKey("platform.meetings");
        var row = MeetingRow(rig, meetings, hoursAgo: 2, key);
        var before = Counter(key, meeting: true);
        var handler = new MarkNotificationDispatchFailedHandler(
            rig.Dispatches, new ThrowingEventBus(), null, meetings.Meetings, meetings.Attendees, meetings.Notifications);

        // The last failure: written, then the publish throws — the effects never ran.
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new MarkNotificationDispatchFailedCommand(
            rig.TenantId, row.Id, "SMTP_TIMEOUT", "SMTP_TIMEOUT", RetryCount: 5, IsPermanentFailure: true), CancellationToken.None));

        Assert.Equal(NotificationDispatchStatus.Failed, row.Status);
        Assert.True(NotificationDispatch.IsPermanentFailurePending(row));
        Assert.Empty(meetings.Undelivered);
        Assert.Equal(before, Counter(key, meeting: true));

        var sweep = new EmailDispatchSweepJob(rig.Dispatches, new RecordingScheduler(), NullLogger<EmailDispatchSweepJob>.Instance,
            new ValidatingMediator(rig.Dispatches, meetings), Options.Create(new EmailDispatchRetentionOptions { RetryWindowHours = 24 }),
            new TenantContext(), meetings.Effects());

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);
        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

        Assert.Equal([row.MeetingAttendeeUserId!.Value], meetings.Undelivered); // once, not twice
        Assert.Single(meetings.OrganizerNotices);
        Assert.Equal(before + 1, Counter(key, meeting: true));
        Assert.False(NotificationDispatch.IsPermanentFailurePending(row));
        Assert.NotNull(row.PermanentlyFailedNotifiedAt);
    }

    [Fact]
    public async Task A_pending_row_is_already_permanent_for_the_retry_and_the_window_queries()
    {
        var rig = new Rig();
        var template = rig.AddTemplate("en", "<p>x</p>", "x");
        var row = rig.AddFailedDispatch("{}", template.Id);
        row.PermanentlyFailedNotifiedAt = NotificationDispatch.PermanentFailurePending;
        row.QueuedAt = DateTimeOffset.UtcNow.AddHours(-30);

        Assert.Empty(await rig.Dispatches.FindRetryWindowExpiredAsync(DateTimeOffset.UtcNow.AddHours(-24), 50));
        await rig.Job().HandleAsync(new EmailDispatchJobArgs(rig.TenantId, row.Id), new BackgroundJobContext(), CancellationToken.None);
        Assert.Null(rig.Transport.LastSentMessage);
    }

    // ---------------------------------------------------------------- doubles

    /// <summary>Records the ambient tenant each close is sent under; throws for one row, as a store failure would.</summary>
    private sealed class TenantRecordingMediator(ITenantContext ambient, IMediator inner, Guid throwFor) : IMediator
    {
        public List<Guid> TenantsSeen { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            TenantsSeen.Add(ambient.TenantId); // throws if no tenant is resolved: that is the point
            if (request is MarkNotificationDispatchFailedCommand failed && failed.DispatchId == throwFor)
            {
                throw new InvalidOperationException("store unavailable");
            }

            return inner.Send(request, cancellationToken);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }

    /// <summary>Every close is refused the way the validator refuses one.</summary>
    private sealed class AlwaysRefusing : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new FluentValidation.ValidationException(
                [new FluentValidation.Results.ValidationFailure("ErrorMessage", "ErrorMessage must be redacted.")]);

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class ThrowingEventBus : IEventBus
    {
        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
            where TEvent : IIntegrationEvent => throw new InvalidOperationException("broker unavailable");

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, EventPublishOptions options, CancellationToken ct = default)
            where TEvent : IIntegrationEvent => throw new InvalidOperationException("broker unavailable");
    }
}
