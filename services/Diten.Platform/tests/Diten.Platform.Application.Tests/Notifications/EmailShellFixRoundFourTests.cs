using System.Diagnostics;
using System.Reflection;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Notifications.Validators;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prometheus;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 — fix round 4 (the last) of WP-EMAIL-SHELL-01. Every mark-failed / mark-sent command here goes through the
/// PRODUCTION ValidationBehavior with the production validators before its real handler: round 3's sweep passed its
/// own tests through a mediator that skipped validation, while in production the validator refused every close.
/// </summary>
public sealed partial class EmailShellDispatchTests
{
    private const string MeetingKey = "platform.meetings.invite";

    // ---------------------------------------------------------------- 1. the close and the job pass the real validator

    [Fact]
    public async Task The_window_close_passes_the_real_validator_and_keeps_the_last_real_error()
    {
        var rig = new Rig();
        var row = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 30);
        row.ErrorCode = "SMTP_TIMEOUT";
        var mediator = new ValidatingMediator(rig.Dispatches);

        await Sweep(rig, mediator).HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

        Assert.Empty(mediator.Refusals);
        Assert.Equal(EmailDispatchSweepJob.RetryWindowExpiredCode, row.ErrorCode);
        Assert.Equal("RetryWindowExpired:SMTP_TIMEOUT", row.ErrorMessage);
        Assert.Equal(NotificationDispatch.ReleasedVariablesJson, row.VariablesJson);
        Assert.NotNull(row.PermanentlyFailedNotifiedAt);
    }

    [Fact]
    public async Task A_failed_retry_whose_provider_gave_no_message_passes_the_real_validator()
    {
        var rig = new Rig();
        var template = rig.AddTemplate("en", "<p>{{TaskTitle}}</p>", "{{TaskTitle}}");
        var row = rig.AddFailedDispatch("{\"TaskTitle\":\"Batch\"}", template.Id);
        var mediator = new ValidatingMediator(rig.Dispatches);
        var job = new EmailDispatchJob(
            rig.Dispatches, new TenantMessagingSettingsResolver(rig.Settings),
            new NotificationsSmtpIntegrationTests.TestProviderResolver(new SilentFailure()), mediator,
            NullLogger<EmailDispatchJob>.Instance, rig.Templates, new EmailTemplateRenderer(), rig.Composer);

        await job.HandleAsync(new EmailDispatchJobArgs(rig.TenantId, row.Id), new BackgroundJobContext(), CancellationToken.None);

        Assert.Empty(mediator.Refusals);
        Assert.Equal(2, row.RetryCount);
        Assert.Equal(EmailDispatchJob.ProviderRejectedMessage, row.ErrorMessage);
    }

    // ---------------------------------------------------------------- 3. the close is conditional

    [Fact]
    public async Task A_window_close_that_lost_to_a_send_writes_nothing()
    {
        var rig = new Rig();
        var row = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 30);
        var readVersion = row.Version;
        row.TryMarkSent("sent-meanwhile", DateTimeOffset.UtcNow); // a retry that landed after the sweep read the row

        var response = await new ValidatingMediator(rig.Dispatches).Send(new MarkNotificationDispatchFailedCommand(
            rig.TenantId, row.Id, EmailDispatchSweepJob.RetryWindowExpiredCode, EmailDispatchSweepJob.ClosingMessage(null),
            IsPermanentFailure: true, ExpectedVersion: readVersion, ExpectedStatus: NotificationDispatchStatus.Failed));

        Assert.False(response.IsSuccessful);
        Assert.Equal(MarkNotificationDispatchFailedHandler.ReasonDispatchChanged, response.ReasonCode);
        Assert.Equal(NotificationDispatchStatus.Sent, row.Status);
        Assert.Null(row.PermanentlyFailedNotifiedAt);
    }

    // ---------------------------------------------------------------- 4. the first run after a long pause is quiet

    [Fact]
    public async Task A_row_older_than_three_windows_is_closed_without_telling_the_organizer_and_a_younger_one_tells_them()
    {
        var rig = new Rig();
        var meetings = new MeetingDoubles();
        var ancient = MeetingRow(rig, meetings, hoursAgo: 24 * EmailDispatchSweepJob.SilentWindowMultiple + 5);
        var recent = MeetingRow(rig, meetings, hoursAgo: 30);
        var before = Counter(MeetingKey, meeting: true);

        await Sweep(rig, new ValidatingMediator(rig.Dispatches, meetings))
            .HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

        Assert.NotNull(ancient.PermanentlyFailedNotifiedAt);
        Assert.NotNull(recent.PermanentlyFailedNotifiedAt);
        Assert.Equal(before + 2, Counter(MeetingKey, meeting: true));
        Assert.Equal([recent.MeetingAttendeeUserId!.Value], meetings.Undelivered);
        Assert.Single(meetings.OrganizerNotices);
    }

    // ---------------------------------------------------------------- 5. 1a goes through the same permanent path

    [Fact]
    public async Task With_no_retry_on_this_server_a_failed_first_send_is_counted_as_a_permanent_failure()
    {
        var meetings = new MeetingDoubles();
        var rig = new Rig
        {
            JobOptions = new BackgroundJobSchedulerOptions(),
            PermanentFailure = meetings.Effects()
        };
        rig.AddTemplate("en", "<p>{{MeetingTitle}}</p>", "{{MeetingTitle}}");
        rig.Transport.SendThrow = new InvalidOperationException("smtp down");
        var attendee = Guid.NewGuid();
        var before = Counter(TemplateKey, meeting: false);

        await rig.Handler().Handle(new QueueEmailNotificationCommand(
            rig.TenantId,
            new QueueEmailNotificationRequest(TemplateKey, "en", new Dictionary<string, object?> { ["MeetingTitle"] = "Review" },
                [new EmailRecipientDto("attendee@example.test", "Attendee")], CausationId: meetings.MeetingId, MeetingAttendeeUserId: attendee),
            "corr"), CancellationToken.None);

        Assert.Equal(before + 1, Counter(TemplateKey, meeting: false));
        var dispatch = Assert.Single(rig.Dispatches.Items);
        Assert.NotNull(dispatch.PermanentlyFailedNotifiedAt);
    }

    [Fact]
    public async Task With_no_retry_on_this_server_a_failed_meeting_invite_badges_the_attendee()
    {
        var meetings = new MeetingDoubles();
        var rig = new Rig { JobOptions = new BackgroundJobSchedulerOptions(), PermanentFailure = meetings.Effects() };
        rig.Templates.CreateAsync(new NotificationTemplate
        {
            IsPlatformDefault = true, TemplateKey = MeetingKey, Channel = NotificationChannelCode.Email, Locale = "en",
            SubjectTemplate = "Invite", BodyHtmlTemplate = "<p>{{MeetingTitle}}</p>", BodyTextTemplate = "{{MeetingTitle}}",
            Status = NotificationTemplateStatus.Active, SemanticVersion = "1.0.0"
        }).GetAwaiter().GetResult();
        rig.Transport.SendThrow = new InvalidOperationException("smtp down");
        var attendee = Guid.NewGuid();

        await rig.Handler().Handle(new QueueEmailNotificationCommand(
            rig.TenantId,
            new QueueEmailNotificationRequest(MeetingKey, "en", new Dictionary<string, object?> { ["MeetingTitle"] = "Review" },
                [new EmailRecipientDto("attendee@example.test", "Attendee")], CausationId: meetings.MeetingId, MeetingAttendeeUserId: attendee),
            "corr"), CancellationToken.None);

        Assert.Equal([attendee], meetings.Undelivered);
        Assert.Single(meetings.OrganizerNotices);
    }

    // ---------------------------------------------------------------- 7. every address rule carries the code

    [Theory]
    [InlineData("")]
    [InlineData("long")]
    public async Task An_empty_or_overlong_address_is_refused_with_the_recipient_code(string kind)
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>x</p>", "x");
        var address = kind == "long" ? new string('a', 245) + "@example.com" : string.Empty;
        Assert.True(kind != "long" || address.Length == 257);
        var command = new QueueEmailNotificationCommand(
            rig.TenantId,
            new QueueEmailNotificationRequest(TemplateKey, "en", new Dictionary<string, object?>(), [new EmailRecipientDto(address, "User")]),
            "corr");

        var refusal = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => Pipeline(rig, command));

        Assert.True(QueueEmailNotificationValidator.IsRecipientRefusal(refusal));
    }

    // ---------------------------------------------------------------- 8. a hostile value cannot hold the queue

    [Fact]
    public void A_megabyte_of_almost_tokens_is_judged_within_the_time_limit_and_masked()
    {
        var hostile = string.Concat(Enumerable.Repeat("eyJ-", 256 * 1024)); // 1 MB, no dot: the worst case for the shape search
        var clock = Stopwatch.StartNew();

        var secret = NotificationSecrets.IsSecretText(hostile);

        clock.Stop();
        Assert.True(secret);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"took {clock.Elapsed}");
    }

    // ---------------------------------------------------------------- 9. ERP identifiers stay plain

    [Theory]
    [InlineData("EmployeeCode")] [InlineData("CustomerCode")] [InlineData("SupplierCode")] [InlineData("VendorCode")]
    [InlineData("WarehouseCode")] [InlineData("CompanyCode")] [InlineData("BranchCode")] [InlineData("LegalEntityCode")]
    [InlineData("AccountCode")] [InlineData("SiteCode")] [InlineData("PlantCode")] [InlineData("LocationCode")]
    [InlineData("OrderCode")] [InlineData("ItemCode")] [InlineData("ProjectKey")] [InlineData("DocumentCode")]
    [InlineData("CodeType")] [InlineData("KeyStatus")]
    public void An_erp_master_data_identifier_is_not_masked(string name) =>
        Assert.False(NotificationSecrets.IsSecretName(name), name);

    [Theory]
    [InlineData("VerificationCodeMessage")] [InlineData("ResetCode")] [InlineData("AccessKey")] [InlineData("SessionTitle")]
    [InlineData("CodeOwner")]
    public void A_word_after_a_secret_word_excuses_it_only_when_it_is_result_type_or_status(string name) =>
        Assert.True(NotificationSecrets.IsSecretName(name), name);

    // ---------------------------------------------------------------- 10. small ones

    [Theory]
    [InlineData(0, 1)] [InlineData(-5, 1)] [InlineData(24, 24)] [InlineData(720, 720)] [InlineData(100000, 720)]
    public void The_retry_window_is_held_between_one_hour_and_thirty_days(int configured, int effective) =>
        Assert.Equal(effective, EmailDispatchRetentionOptions.EffectiveWindowHours(configured));

    [Theory]
    [InlineData("Tag the review #code=4 in the thread")]
    [InlineData("?code=1 is the default")]
    public void A_hash_or_question_mark_in_plain_text_is_not_a_link(string value) =>
        Assert.False(NotificationSecrets.IsSecretText(value), value);

    [Theory]
    [InlineData("https://report:" + "Hunter2x@files.example/q3.pdf")]
    [InlineData("See ftp://ops@backup.example/dump")]
    public void A_link_that_carries_a_user_before_its_host_is_masked(string value) =>
        Assert.True(NotificationSecrets.IsSecretText(value), value);

    [Fact]
    public async Task A_handler_composed_without_job_settings_keeps_no_variables_for_a_retry()
    {
        var rig = new Rig { JobOptions = null }; // composed without settings: fails closed
        rig.AddTemplate("en", "<p>{{TaskTitle}}</p>", "{{TaskTitle}}");
        rig.Transport.SendThrow = new InvalidOperationException("smtp down");

        await rig.QueueAsync("en", new() { ["TaskTitle"] = "Batch" });

        var dispatch = Assert.Single(rig.Dispatches.Items);
        Assert.Equal(NotificationDispatch.ReleasedVariablesJson, dispatch.VariablesJson);
        Assert.Null(dispatch.NextRetryAt);
    }

    // ---------------------------------------------------------------- 12. rules that had no test

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void The_registrar_and_the_queue_handler_give_the_same_answer(bool enabled, bool standard, bool flag)
    {
        var options = new BackgroundJobSchedulerOptions { Enabled = enabled, RegisterStandardJobs = standard };
        options.EnabledJobs[EmailDispatchSweepJob.JobId] = flag;

        var descriptor = new Diten.Platform.Application.BackgroundJobs.PlatformRecurringJobRegistrar(
                Options.Create(options), Options.Create(new Diten.Platform.Application.Features.WorkingCalendarImport.WorkingCalendarImportOptions()))
            .GetRecurringJobs()
            .Single(job => job.HandlerType == typeof(EmailDispatchSweepJob))
            .Descriptor;
        var handler = new Rig { JobOptions = options }.Handler();

        Assert.Equal(EmailDispatchSweepJob.IsScheduled(options), descriptor.IsEnabled);
        Assert.Equal(descriptor.IsEnabled, handler.RetriesScheduled);
    }

    [Fact]
    public async Task The_window_closes_each_tenants_row_in_its_own_tenant()
    {
        var rig = new Rig();
        var mine = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 30);
        var otherTenant = Guid.NewGuid();
        var theirs = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 30);
        theirs.TenantId = otherTenant;
        var mediator = new ValidatingMediator(rig.Dispatches);

        await Sweep(rig, mediator).HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

        Assert.Equal(EmailDispatchSweepJob.RetryWindowExpiredCode, mine.ErrorCode);
        Assert.Equal(EmailDispatchSweepJob.RetryWindowExpiredCode, theirs.ErrorCode);
        Assert.Equal(
            new[] { rig.TenantId, otherTenant }.OrderBy(x => x),
            mediator.Closed.Select(c => c.TenantId).OrderBy(x => x));
    }

    [Fact]
    public void Every_platform_class_that_sends_the_internal_key_to_AuthService_uses_the_internal_client()
    {
        var root = RepositoryRoot();
        var services = Path.Combine(root, "services/Diten.Platform/src/Diten.Platform.Infrastructure");
        var helper = File.ReadAllText(Path.Combine(services, "Services/InternalHttpClients.cs"));
        var senders = Directory.GetFiles(services, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => !path.EndsWith("InternalHttpClients.cs", StringComparison.Ordinal))
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .Where(file => file.Text.Contains("X-Internal-Api-Key", StringComparison.Ordinal)
                && file.Text.Contains("AuthServiceOptions", StringComparison.Ordinal))
            .ToList();

        Assert.True(senders.Count >= 9, $"found {senders.Count} senders: the guard would prove nothing");
        foreach (var (path, text) in senders)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var calls = System.Text.RegularExpressions.Regex.Matches(text, @"CreateClient\(([^)]*)\)");
            foreach (System.Text.RegularExpressions.Match call in calls)
            {
                Assert.True(
                    call.Groups[1].Value is "InternalHttpClients.AuthInternal" or "AuthInternalClientName",
                    $"{name} asks the factory for '{call.Groups[1].Value}', not the internal client.");
            }

            if (calls.Count == 0)
            {
                Assert.Contains($", {name}>", helper);
            }
        }
    }

    [Fact]
    public void No_notification_test_carries_a_literal_shaped_like_a_real_key()
    {
        // Item 11 — secret scanners and push protection read source, not the compiled string: every key-shaped test
        // value is written in pieces. The patterns are themselves built from pieces, so this file passes its own check.
        var shapes = new System.Text.RegularExpressions.Regex(
            "sk_" + "live_|sk_" + "test_|gh" + "p_[A-Za-z0-9]{8}|xo" + "xb-|AK" + "IA[0-9A-Z]{16}|ey" + "J[A-Za-z0-9_-]{8,}\\.ey" + "J|sk-" + "live-");
        var folder = Path.Combine(RepositoryRoot(), "services/Diten.Platform/tests/Diten.Platform.Application.Tests/Notifications");
        var offenders = Directory.GetFiles(folder, "*.cs")
            .Where(path => shapes.IsMatch(File.ReadAllText(path)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(Directory.GetFiles(folder, "*.cs").Length > 10);
        Assert.Empty(offenders);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("repository_root_not_found");
    }

    // ---------------------------------------------------------------- helpers

    private static EmailDispatchSweepJob Sweep(Rig rig, IMediator mediator) => new(
        rig.Dispatches,
        new RecordingScheduler(),
        NullLogger<EmailDispatchSweepJob>.Instance,
        mediator,
        Options.Create(new EmailDispatchRetentionOptions { RetryWindowHours = 24 }));

    private static NotificationDispatch MeetingRow(Rig rig, MeetingDoubles meetings, int hoursAgo)
    {
        var row = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo);
        row.TemplateKey = MeetingKey;
        row.CausationId = meetings.MeetingId;
        row.MeetingAttendeeUserId = Guid.NewGuid();
        return row;
    }

    private static double Counter(string templateKey, bool meeting) =>
        Metrics.CreateCounter(
                "notification_dispatch_permanently_failed",
                "Notification dispatches whose final retry attempt was exhausted with no further retry due.",
                new CounterConfiguration { LabelNames = ["template_key", "is_meeting_related"] })
            .WithLabels(templateKey, meeting ? "true" : "false").Value;

    /// <summary>
    /// The two transition commands as MediatR runs them in production: the production validator inside the production
    /// ValidationBehavior, then the real handler over the rig's store. A refusal is recorded, not hidden.
    /// </summary>
    private sealed class ValidatingMediator(INotificationDispatchRepository dispatches, MeetingDoubles? meetings = null) : IMediator
    {
        public List<string> Refusals { get; } = [];
        public List<MarkNotificationDispatchFailedCommand> Closed { get; } = [];

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            try
            {
                object response = request switch
                {
                    MarkNotificationDispatchFailedCommand failed => await new ValidationBehavior<MarkNotificationDispatchFailedCommand, Response<NotificationDispatchDto>>(
                            [new MarkNotificationDispatchFailedValidator()])
                        .Handle(failed, async () =>
                        {
                            var result = await new MarkNotificationDispatchFailedHandler(
                                    dispatches, new NotificationsSmtpIntegrationTests.RecordingEventBus(), null,
                                    meetings?.Meetings, meetings?.Attendees, meetings?.Notifications)
                                .Handle(failed, cancellationToken);
                            if (result.IsSuccessful && failed.IsPermanentFailure)
                            {
                                Closed.Add(failed);
                            }

                            return result;
                        }, cancellationToken),
                    MarkNotificationDispatchSentCommand sent => await new ValidationBehavior<MarkNotificationDispatchSentCommand, Response<NotificationDispatchDto>>(
                            [new MarkNotificationDispatchSentValidator()])
                        .Handle(sent, () => new MarkNotificationDispatchSentHandler(dispatches, new NotificationsSmtpIntegrationTests.RecordingEventBus())
                            .Handle(sent, cancellationToken), cancellationToken),
                    _ => throw new NotSupportedException(request.GetType().Name)
                };
                return (TResponse)response;
            }
            catch (FluentValidation.ValidationException refusal)
            {
                Refusals.Add(string.Join(" | ", refusal.Errors.Select(e => e.PropertyName + ": " + e.ErrorMessage)));
                throw;
            }
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }

    /// <summary>A provider that fails and says nothing about why.</summary>
    private sealed class SilentFailure : IMessagingProvider
    {
        public MessagingProviderCode ProviderCode => MessagingProviderCode.Smtp;

        public Task<MessagingProviderResult> SendEmailAsync(MessagingProviderEmailRequest request, CancellationToken ct = default) =>
            Task.FromResult(MessagingProviderResult.Fail("SMTP_TIMEOUT", null!));
    }

    /// <summary>The three stores a meeting's permanent failure writes to, recording what was written.</summary>
    internal sealed class MeetingDoubles
    {
        public Guid MeetingId { get; } = Guid.NewGuid();
        public Guid OrganizerId { get; } = Guid.NewGuid();
        public List<Guid> Undelivered { get; } = [];
        public List<UserNotification> OrganizerNotices { get; } = [];

        public IMeetingRepository Meetings => Recorder<IMeetingRepository>.Create((method, args) =>
            method == nameof(IMeetingRepository.GetByIdAsync)
                ? new Meeting
                {
                    Id = MeetingId, TenantId = Guid.NewGuid(), Title = "Review", OrganizerUserId = OrganizerId, MeetingTypeId = Guid.NewGuid(),
                    StartAt = DateTimeOffset.UtcNow.AddDays(1), EndAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                    IdempotencyKey = Guid.NewGuid().ToString("N")
                }
                : null);

        public IMeetingAttendeeRepository Attendees => Recorder<IMeetingAttendeeRepository>.Create((method, args) =>
        {
            if (method == nameof(IMeetingAttendeeRepository.MarkMailUndeliveredAsync))
            {
                Undelivered.Add((Guid)args[1]!);
            }

            return null;
        });

        public IUserNotificationRepository Notifications => Recorder<IUserNotificationRepository>.Create((method, args) =>
        {
            if (method == nameof(IUserNotificationRepository.CreateAsync) && args[0] is UserNotification notice)
            {
                OrganizerNotices.Add(notice);
                return notice;
            }

            return null;
        });

        public NotificationPermanentFailureEffects Effects() => new(NullLogger.Instance, Meetings, Attendees, Notifications);
    }

    /// <summary>Any interface, every member answered by one function; a Task result is wrapped as the member expects.</summary>
    public class Recorder<T> : DispatchProxy where T : class
    {
        private Func<string, object?[], object?> _answer = (_, _) => null;

        public static T Create(Func<string, object?[], object?> answer)
        {
            var proxy = Create<T, Recorder<T>>();
            ((Recorder<T>)(object)proxy)._answer = answer;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var value = _answer(targetMethod!.Name, args ?? []);
            var type = targetMethod.ReturnType;
            if (type == typeof(Task))
            {
                return Task.CompletedTask;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var inner = type.GetGenericArguments()[0];
                var result = value ?? (inner.IsValueType ? Activator.CreateInstance(inner) : null);
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, [result]);
            }

            return value ?? (type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null);
        }
    }
}
