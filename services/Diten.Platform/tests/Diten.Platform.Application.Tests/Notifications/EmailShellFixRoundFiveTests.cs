using Diten.BuildingBlocks.BackgroundJobs;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Notifications.Validators;
using Diten.Platform.Contracts.Events.Notifications;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>BL-454 — fix round 5 (the last) of WP-EMAIL-SHELL-01, by the prompt's item numbers.</summary>
public sealed partial class EmailShellDispatchTests
{
    // ---------------------------------------------------------------- 2. only "code" is excused by a word before it

    [Theory]
    [InlineData("EmployeePin")] [InlineData("CustomerOtp")] [InlineData("AccountKey")] [InlineData("AccountSession")]
    [InlineData("SitePass")] [InlineData("WeekKey")] [InlineData("ProjectKey")] [InlineData("BranchNonce")]
    public void A_short_secret_word_after_an_erp_word_is_still_a_secret(string name) =>
        Assert.True(NotificationSecrets.IsSecretName(name), name);

    [Theory]
    [InlineData("CustomerCode")] [InlineData("EmployeeCode")] [InlineData("SortKey")] [InlineData("ForeignKey")]
    [InlineData("LookupKey")] [InlineData("KeyResult")]
    public void An_erp_code_and_a_data_key_stay_plain(string name) =>
        Assert.False(NotificationSecrets.IsSecretName(name), name);

    // ---------------------------------------------------------------- 4. a refused close is answered once, plainly

    [Fact]
    public async Task A_close_the_validator_refuses_is_retried_once_with_the_plain_message_and_the_refusal_is_logged()
    {
        var rig = new Rig();
        var row = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 30);
        row.ErrorCode = "SMTP_TIMEOUT";
        var logger = new LinesLogger<EmailDispatchSweepJob>();
        var mediator = new RefusingOnce(new ValidatingMediator(rig.Dispatches));
        var sweep = TestSweeps.Create(rig.Dispatches, new RecordingScheduler(), logger, mediator,
            Options.Create(new EmailDispatchRetentionOptions { RetryWindowHours = 24 }));

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

        Assert.Equal("RetryWindowExpired:None", row.ErrorMessage);
        Assert.NotNull(row.PermanentlyFailedNotifiedAt);
        Assert.Contains(logger.Lines, line => line.Contains("expiry_refused", StringComparison.Ordinal) && line.Contains("ErrorMessage", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_very_long_stored_error_code_is_cut_and_the_close_passes_the_real_validator()
    {
        var rig = new Rig();
        var row = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 30);
        row.ErrorCode = new string('A', 3000);
        var mediator = new ValidatingMediator(rig.Dispatches);

        await Sweep(rig, mediator).HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

        Assert.Empty(mediator.Refusals);
        Assert.Equal("RetryWindowExpired:" + new string('A', EmailDispatchSweepJob.MaxKeptErrorCodeLength), row.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_failed_retry_whose_provider_message_is_blank_records_the_code(string blank)
    {
        var rig = new Rig();
        var template = rig.AddTemplate("en", "<p>{{TaskTitle}}</p>", "{{TaskTitle}}");
        var row = rig.AddFailedDispatch("{\"TaskTitle\":\"Batch\"}", template.Id);
        var mediator = new ValidatingMediator(rig.Dispatches);
        var job = new EmailDispatchJob(
            rig.Dispatches, new TenantMessagingSettingsResolver(rig.Settings),
            new NotificationsSmtpIntegrationTests.TestProviderResolver(new BlankFailure(blank)), mediator,
            NullLogger<EmailDispatchJob>.Instance, rig.Templates, new EmailTemplateRenderer(), rig.Composer);

        await job.HandleAsync(new EmailDispatchJobArgs(rig.TenantId, row.Id), new BackgroundJobContext(), CancellationToken.None);

        Assert.Empty(mediator.Refusals);
        Assert.Equal(EmailDispatchJob.ProviderRejectedMessage, row.ErrorMessage);
        Assert.Equal(EmailDispatchJob.ProviderRejectedMessage, row.ErrorCode);
    }

    // ---------------------------------------------------------------- 5. the two remaining recipient rules carry the code

    [Theory]
    [InlineData("no-recipient")]
    [InlineData("long-name")]
    public async Task No_recipient_or_an_overlong_recipient_name_is_refused_with_the_recipient_code(string kind)
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>x</p>", "x");
        IReadOnlyList<EmailRecipientDto> to = kind == "no-recipient"
            ? []
            : [new EmailRecipientDto("user@example.com", new string('n', 161))];
        var command = new QueueEmailNotificationCommand(
            rig.TenantId, new QueueEmailNotificationRequest(TemplateKey, "en", new Dictionary<string, object?>(), to), "corr");

        var refusal = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => Pipeline(rig, command));

        Assert.True(QueueEmailNotificationValidator.IsRecipientRefusal(refusal));
        Assert.All(refusal.Errors, error => Assert.Equal(QueueEmailNotificationHandler.ReasonRecipientInvalid, error.ErrorCode));
    }

    // ---------------------------------------------------------------- 6. long values and long tokens

    [Fact]
    public void A_value_longer_than_the_judged_limit_is_a_secret_without_being_searched()
    {
        var prose = string.Concat(Enumerable.Repeat("plain words ", NotificationSecrets.MaxJudgedLength / 12 + 10));
        Assert.True(prose.Length > NotificationSecrets.MaxJudgedLength);
        Assert.True(NotificationSecrets.IsSecretText(prose));
        Assert.False(NotificationSecrets.IsSecretText("plain words plain words"));
    }

    [Fact]
    public void A_token_whose_payload_is_longer_than_any_bound_is_still_a_token()
    {
        var token = "ey" + "J" + new string('h', 40) + "." + new string('p', 20_000) + "." + new string('s', 40);
        Assert.True(NotificationSecrets.IsSecretText(token));
        Assert.True(NotificationSecrets.IsSecretText("ey" + "J" + new string('h', 5000)));
    }

    [Fact]
    public void A_name_longer_than_the_judged_limit_is_treated_as_a_secret() =>
        Assert.True(NotificationSecrets.IsSecretName(new string('a', NotificationSecrets.MaxJudgedLength + 1)));

    // ---------------------------------------------------------------- 9. rules that had no test

    [Theory]
    [InlineData(null, "RetryWindowExpired:None")]
    [InlineData("smtp password rejected", "RetryWindowExpired:Redacted")]
    public async Task The_closing_message_branches_pass_the_real_validator(string? lastError, string expected)
    {
        var rig = new Rig();
        var row = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 30);
        row.ErrorCode = lastError;
        var mediator = new ValidatingMediator(rig.Dispatches);

        await Sweep(rig, mediator).HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

        Assert.Empty(mediator.Refusals);
        Assert.Equal(expected, row.ErrorMessage);
    }

    [Fact]
    public async Task A_conditional_write_that_matches_nothing_answers_409_publishes_nothing_and_tells_nobody()
    {
        var rig = new Rig();
        var meetings = new MeetingDoubles();
        var row = MeetingRow(rig, meetings, hoursAgo: 30, UniqueKey("platform.meetings"));
        var bus = new CountingEventBus();
        var handler = new MarkNotificationDispatchFailedHandler(
            new WritesNothing(rig.Dispatches), bus, null, meetings.Meetings, meetings.Attendees, meetings.Notifications);

        var response = await handler.Handle(new MarkNotificationDispatchFailedCommand(
            rig.TenantId, row.Id, EmailDispatchSweepJob.RetryWindowExpiredCode, EmailDispatchSweepJob.ClosingMessage(null),
            IsPermanentFailure: true, ExpectedVersion: row.Version, ExpectedStatus: row.Status), CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MarkNotificationDispatchFailedHandler.ReasonDispatchChanged, response.ReasonCode);
        Assert.Equal(0, bus.Published);
        Assert.Empty(meetings.Undelivered);
        Assert.Empty(meetings.OrganizerNotices);
    }

    [Fact]
    public async Task A_mail_that_is_not_a_meeting_invite_badges_nobody_even_with_attendee_ids()
    {
        var meetings = new MeetingDoubles();
        var dispatch = new NotificationDispatch
        {
            TenantId = Guid.NewGuid(),
            TemplateKey = UniqueKey("platform.tasks"),
            CausationId = meetings.MeetingId,
            MeetingAttendeeUserId = Guid.NewGuid(),
            To = [new EmailRecipient { Email = "user@example.com" }]
        };

        await meetings.Effects().ApplyAsync(dispatch, silent: false, CancellationToken.None);

        Assert.Empty(meetings.Undelivered);
        Assert.Empty(meetings.OrganizerNotices);
    }

    [Theory]
    [InlineData(0, 2, 0.5)]          // configured 0 → one hour
    [InlineData(100_000, 721, 700)]  // configured far too long → thirty days
    public async Task The_sweep_closes_by_the_bounded_window(int configuredHours, int closedHoursAgo, double openHoursAgo)
    {
        var rig = new Rig();
        var closed = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: closedHoursAgo);
        var open = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 0);
        open.QueuedAt = DateTimeOffset.UtcNow.AddHours(-openHoursAgo);
        var sweep = TestSweeps.Create(rig.Dispatches, new RecordingScheduler(), NullLogger<EmailDispatchSweepJob>.Instance,
            new ValidatingMediator(rig.Dispatches), Options.Create(new EmailDispatchRetentionOptions { RetryWindowHours = configuredHours }));

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

        Assert.NotNull(closed.PermanentlyFailedNotifiedAt);
        Assert.Null(open.PermanentlyFailedNotifiedAt);
    }

    // ---------------------------------------------------------------- 8. the guards cover everything they claim to

    [Fact]
    public void No_test_under_notifications_carries_a_key_shaped_literal_in_any_known_format()
    {
        var shapes = new System.Text.RegularExpressions.Regex(
            "(?<![A-Za-z0-9])(?:sk_" + "live_|sk_" + "test_|gh" + "p_[A-Za-z0-9]{8}|xo" + "xb-|AK" + "IA[0-9A-Z]{16}"
            + "|ey" + "J[A-Za-z0-9_-]{8,}\\.ey" + "J|sk-" + "[A-Za-z0-9-]{12,}|S" + "G\\.[A-Za-z0-9_-]{8,}\\.|AI" + "za[0-9A-Za-z_-]{20,}"
            + "|-----BEGIN [A-Z ]*PRIV" + "ATE KEY-----)");
        var folder = Path.Combine(RepositoryRoot(), "services/Diten.Platform/tests/Diten.Platform.Application.Tests/Notifications");
        var files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
        var offenders = files.Where(path => shapes.IsMatch(File.ReadAllText(path))).Select(Path.GetFileName).ToList();

        Assert.True(files.Length > 10);
        Assert.Empty(offenders);
    }

    [Fact]
    public void Exactly_the_known_classes_send_the_internal_key_and_each_one_uses_the_no_redirect_client()
    {
        var root = RepositoryRoot();
        var platform = Path.Combine(root, "services/Diten.Platform/src/Diten.Platform.Infrastructure");
        var web = Path.Combine(root, "frontend/Diten.Web/Services");
        var platformHelper = File.ReadAllText(Path.Combine(platform, "Services/InternalHttpClients.cs"));
        var webHelper = File.ReadAllText(Path.Combine(web, "InternalHttpClients.cs"));

        var platformSenders = Senders(platform, text => text.Contains("AuthServiceOptions", StringComparison.Ordinal));
        var webSenders = Senders(web, _ => true);

        Assert.Equal(
            new[]
            {
                "AdminUserInvitationService", "AuthPermissionModulesClient", "AuthServiceApprovalRoleDirectory",
                "AuthServiceTenantActivationNotifier", "AuthTaskNotificationRecipientClient", "AuthTenantUserCountClient",
                "AuthUserDisplayNameClient", "CatalogPermissionSyncService", "PlatformAdministratorProvisioningService"
            },
            platformSenders.Keys.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(new[] { "BrandingGateway", "TenantSlugResolver", "TenantStatusGateway" }, webSenders.Keys.OrderBy(x => x, StringComparer.Ordinal));

        foreach (var (name, text) in platformSenders)
        {
            AssertNoRedirectClient(name, text, platformHelper);
        }

        foreach (var (name, text) in webSenders)
        {
            Assert.DoesNotContain("CreateClient(", text);
            Assert.Contains($", {name}>", webHelper);
        }
    }

    private static Dictionary<string, string> Senders(string folder, Func<string, bool> also) =>
        Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => !path.EndsWith("InternalHttpClients.cs", StringComparison.Ordinal))
            .Select(path => (Name: Path.GetFileNameWithoutExtension(path), Text: File.ReadAllText(path)))
            .Where(file => file.Text.Contains("X-Internal-Api-Key", StringComparison.Ordinal) && also(file.Text))
            .ToDictionary(file => file.Name, file => file.Text);

    private static void AssertNoRedirectClient(string name, string text, string helper)
    {
        var calls = System.Text.RegularExpressions.Regex.Matches(text, @"CreateClient\(([^)]*)\)");
        foreach (System.Text.RegularExpressions.Match call in calls)
        {
            // A sender may keep its OWN named client (the display-name client's short timeout) only when that client is
            // registered without redirects through the internal-client helper.
            var ownNoRedirectClient = call.Groups[1].Value == "HttpClientName"
                                      && helper.Contains($"{name}.AddAuthDisplayNameHttpClient(services)", StringComparison.Ordinal)
                                      && text.Contains("WithoutRedirects()", StringComparison.Ordinal);
            Assert.True(
                call.Groups[1].Value is "InternalHttpClients.AuthInternal" or "AuthInternalClientName" || ownNoRedirectClient,
                $"{name} asks the factory for '{call.Groups[1].Value}', not the internal client.");
        }

        if (calls.Count == 0)
        {
            Assert.Contains($", {name}>", helper);
        }
    }

    // ---------------------------------------------------------------- doubles

    /// <summary>The first close is refused the way the validator refuses one; the second goes through.</summary>
    private sealed class RefusingOnce(IMediator inner) : IMediator
    {
        private bool _refused;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (!_refused && request is MarkNotificationDispatchFailedCommand)
            {
                _refused = true;
                throw new FluentValidation.ValidationException(
                    [new FluentValidation.Results.ValidationFailure("ErrorMessage", "ErrorMessage must be redacted.")]);
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

    /// <summary>A provider that fails with a blank message.</summary>
    private sealed class BlankFailure(string message) : IMessagingProvider
    {
        public MessagingProviderCode ProviderCode => MessagingProviderCode.Smtp;

        public Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default) =>
            Task.FromResult(MessagingProviderResult.Fail(message, message));
    }

    /// <summary>The rig's store, except that a conditional write always loses (someone wrote in between).</summary>
    private sealed class WritesNothing(INotificationDispatchRepository inner) : INotificationDispatchRepository
    {
        public Task<NotificationDispatch> CreateAsync(NotificationDispatch dispatch, CancellationToken ct = default) => inner.CreateAsync(dispatch, ct);
        public Task<NotificationDispatch?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
            inner.GetByIdForTenantAsync(tenantId, id, ct);
        public Task<IReadOnlyList<NotificationDispatch>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, NotificationDispatchStatus? status = null, DateTimeOffset? queuedFrom = null, DateTimeOffset? queuedTo = null, string? templateKey = null, CancellationToken ct = default) =>
            inner.ListByTenantAsync(tenantId, skip, take, status, queuedFrom, queuedTo, templateKey, ct);
        public Task UpdateAsync(NotificationDispatch dispatch, CancellationToken ct = default) => inner.UpdateAsync(dispatch, ct);
        public Task<bool> TryUpdateAsync(NotificationDispatch dispatch, int expectedVersion, NotificationDispatchStatus expectedStatus, CancellationToken ct = default) =>
            Task.FromResult(false);
        public Task<IReadOnlyList<NotificationDispatchRetryHandle>> FindDueRetriesAsync(DateTimeOffset asOfUtc, int maxRetryCount, int take, CancellationToken ct = default) =>
            inner.FindDueRetriesAsync(asOfUtc, maxRetryCount, take, ct);
        public Task<IReadOnlyList<NotificationDispatchExpiryHandle>> FindPermanentFailurePendingAsync(DateTimeOffset idleBefore, int take, CancellationToken ct = default) =>
            inner.FindPermanentFailurePendingAsync(idleBefore, take, ct);
        public Task<IReadOnlyList<NotificationDispatchExpiryHandle>> FindRetryWindowExpiredAsync(DateTimeOffset queuedBefore, int take, CancellationToken ct = default) =>
            inner.FindRetryWindowExpiredAsync(queuedBefore, take, ct);
    }

    private sealed class CountingEventBus : IEventBus
    {
        public int Published { get; private set; }

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
            where TEvent : IIntegrationEvent => PublishAsync(@event, new EventPublishOptions(), ct);

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, EventPublishOptions options, CancellationToken ct = default)
            where TEvent : IIntegrationEvent
        {
            Published++;
            return Task.FromResult(new EventEnvelope<TEvent>(
                new EventMetadata(Guid.NewGuid(), @event.EventName, @event.EventVersion, Guid.NewGuid(), null, options.TenantId, "test", DateTimeOffset.UtcNow),
                @event));
        }
    }
}
