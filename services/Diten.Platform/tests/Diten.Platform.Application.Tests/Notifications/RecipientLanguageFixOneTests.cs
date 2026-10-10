using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// WP-EMAIL-LANG-01 delivery 1, fix round 1 (P-2026-10-09-EMAIL-LANG-01-FIX1) — splitting a mail by language must not turn
/// one refused reader into a repeat mail for everybody else: a call in which SOME language groups were queued SUCCEEDS
/// (PARTIAL_RECIPIENT_FAILURE), so a caller that retries on failure never mails the queued readers again.
/// </summary>
public sealed class RecipientLanguageFixOneTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private const string TenantLanguage = "tr";
    private const string EventCode = "task.assigned";

    // ---------------------------------------------------------------- R1 — the answer of a partly sent mail

    [Fact]
    public async Task Some_groups_queued_is_a_success_that_names_the_groups_that_were_not()
    {
        var (adapter, queue) = Build(failLocale: "ru");

        var result = await adapter.DispatchByEventCodeAsync(Request(
            new EmailRecipientDto("fay@acme.test", "Fay", "fr"),
            new EmailRecipientDto("ruslan@acme.test", "Ruslan", "ru")));

        Assert.True(result.IsSuccessful);
        Assert.Equal(NotificationEventDispatchAdapter.ReasonPartialRecipientFailure, result.ReasonCode);
        Assert.Contains(result.Errors, e => e.StartsWith("ru:", StringComparison.Ordinal));
        Assert.Equal(["fay@acme.test"], queue.Queued.SelectMany(c => c.Request.To).Select(r => r.Email));
    }

    [Fact]
    public async Task No_group_queued_is_a_failure()
    {
        var (adapter, _) = Build(failLocale: "fr");

        var result = await adapter.DispatchByEventCodeAsync(Request(new EmailRecipientDto("fay@acme.test", "Fay", "fr")));

        Assert.False(result.IsSuccessful);
    }

    [Fact]
    public async Task A_group_that_throws_is_caught_and_the_next_group_is_still_tried()
    {
        var (adapter, queue) = Build(throwLocale: "fr");

        var result = await adapter.DispatchByEventCodeAsync(Request(
            new EmailRecipientDto("fay@acme.test", "Fay", "fr"),
            new EmailRecipientDto("ruslan@acme.test", "Ruslan", "ru")));

        Assert.True(result.IsSuccessful);
        Assert.Equal(NotificationEventDispatchAdapter.ReasonPartialRecipientFailure, result.ReasonCode);
        Assert.Equal(["ruslan@acme.test"], queue.Queued.SelectMany(c => c.Request.To).Select(r => r.Email));
    }

    [Fact]
    public async Task When_no_group_is_queued_the_first_exception_is_thrown_as_it_was()
    {
        // A single dispatch behaves exactly as before FIX1: a thrown refusal reaches the caller unchanged.
        var (adapter, _) = Build(throwLocale: "fr");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.DispatchByEventCodeAsync(Request(new EmailRecipientDto("fay@acme.test", "Fay", "fr"))));

        Assert.Equal("the queue is down for this group", thrown.Message);
    }

    // ---------------------------------------------------------------- the minors of the adapter

    [Fact]
    public async Task No_preference_and_an_explicit_choice_of_the_tenants_language_are_one_mail()
    {
        // The language is resolved FIRST and the groups are made by the resolved value.
        var (adapter, queue) = Build();

        await adapter.DispatchByEventCodeAsync(Request(
            new EmailRecipientDto("ayse@acme.test", "Ayşe"),
            new EmailRecipientDto("mehmet@acme.test", "Mehmet", TenantLanguage)));

        var command = Assert.Single(queue.Sent);
        Assert.Equal(TenantLanguage, command.Request.Locale);
        Assert.Equal(2, command.Request.To.Count);
    }

    [Fact]
    public async Task The_same_address_in_to_cc_and_bcc_is_sent_once()
    {
        var (adapter, queue) = Build();
        var request = Request(
            new EmailRecipientDto("Fay@Acme.test", "Fay", "fr"),
            new EmailRecipientDto("fay@acme.test ", "Fay again", "es")) with
        {
            Cc = [new EmailRecipientDto("FAY@acme.test", "Fay as copy"), new EmailRecipientDto("boss@acme.test", "Boss")],
            Bcc = [new EmailRecipientDto("boss@acme.test", "Boss hidden"), new EmailRecipientDto("audit@acme.test", "Audit")]
        };

        await adapter.DispatchByEventCodeAsync(request);

        var command = Assert.Single(queue.Sent); // the second "fay" (es) was the same address: no es mail at all
        Assert.Equal(["Fay@Acme.test"], command.Request.To.Select(r => r.Email));
        Assert.Equal(["boss@acme.test"], command.Request.Cc!.Select(r => r.Email));
        Assert.Equal(["audit@acme.test"], command.Request.Bcc!.Select(r => r.Email));
    }

    [Fact]
    public async Task Cc_and_Bcc_ride_with_the_first_group_that_is_queued()
    {
        var (adapter, queue) = Build(failLocale: "fr");
        var request = Request(
            new EmailRecipientDto("fay@acme.test", "Fay", "fr"),
            new EmailRecipientDto("ruslan@acme.test", "Ruslan", "ru")) with
        {
            Cc = [new EmailRecipientDto("cc@acme.test", "Cc")],
            Bcc = [new EmailRecipientDto("bcc@acme.test", "Bcc")]
        };

        await adapter.DispatchByEventCodeAsync(request);

        var queued = Assert.Single(queue.Queued);
        Assert.Equal("ru", queued.Request.Locale); // the copies are in that group's language
        Assert.Equal(["cc@acme.test"], queued.Request.Cc!.Select(r => r.Email));
        Assert.Equal(["bcc@acme.test"], queued.Request.Bcc!.Select(r => r.Email));
    }

    // ---------------------------------------------------------------- R1 end to end: the hourly due-soon reminder

    [Fact]
    public async Task A_reminder_whose_one_language_group_is_refused_keeps_its_claim_and_mails_the_others_exactly_once()
    {
        // A pool task held by two people: Fay reads French, Ruslan Russian — and Ruslan's address is refused for good.
        var position = Guid.NewGuid();
        var fay = Guid.NewGuid();
        var ruslan = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), TenantId = Tenant, Title = "Pool deadline", Lifecycle = TaskLifecycle.InProgress,
            AssignmentTarget = TaskAssignmentTarget.PositionPool, PoolPositionId = position, OrganizationUnitId = Guid.NewGuid(),
            EmailNotificationsEnabled = true, DueAt = now.AddDays(1), ReminderLeadDays = 3, Version = 1
        };
        var tasks = new FakeTaskItemRepository(task);
        var (adapter, queue) = Build(failLocale: "ru", eventCode: TaskNotificationEvents.DueSoon);
        var notifications = new TaskNotificationService(
            adapter,
            new FakeNotificationLocaleResolver(),
            new LanguagePeople((fay, "fay@acme.test", "fr"), (ruslan, "ruslan@acme.test", "ru")),
            new FakePositionAssignmentRepository(Holder(position, fay), Holder(position, ruslan)),
            new FakeUserNotificationRepository(),
            new FakeTenantContext(Tenant),
            NullLogger<TaskNotificationService>.Instance);
        var reminders = new SendDueSoonRemindersHandler(tasks, notifications, NullLogger<SendDueSoonRemindersHandler>.Instance);

        var first = await reminders.Handle(new SendDueSoonRemindersCommand(now, 100, "sweep-1"), CancellationToken.None);
        var second = await reminders.Handle(new SendDueSoonRemindersCommand(now.AddHours(1), 100, "sweep-2"), CancellationToken.None);

        // FIX2 — a reminder that reached only some readers is counted as PARTIAL (not "sent", not "not delivered").
        Assert.Equal(1, first.Data!.Partial);
        Assert.Equal(0, first.Data.NotDelivered);
        Assert.Equal(0, second.Data!.RemindersSent + second.Data.Partial);
        Assert.False(string.IsNullOrWhiteSpace((await tasks.GetByIdAsync(task.Id, CancellationToken.None))!.LastDueSoonReminderKey)); // the claim is KEPT
        Assert.Equal(["fay@acme.test"], queue.Queued.SelectMany(c => c.Request.To).Select(r => r.Email)); // once, not every hour
    }

    // ---------------------------------------------------------------- FIX2 — a partly sent reminder is counted and said

    [Fact]
    public async Task A_partly_sent_reminder_is_PartiallyDispatched_counted_logged_and_keeps_its_claim()
    {
        var (task, tasks, notifications, logger, queue) = PoolReminder(failLocale: "ru");
        var reminders = new SendDueSoonRemindersHandler(tasks, notifications, NullLogger<SendDueSoonRemindersHandler>.Instance);

        var outcome = await notifications.NotifyAsync(task, TaskNotificationEvents.DueSoon, [Fay, Ruslan], Guid.Empty, CancellationToken.None);
        Assert.Equal(TaskNotificationOutcome.PartiallyDispatched, outcome);
        var warning = Assert.Single(logger.Entries, e => e.Line.Contains("task.notification.partially_dispatched", StringComparison.Ordinal));
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, warning.Level);
        Assert.Contains(task.Id.ToString(), warning.Line);
        Assert.Contains("ru: RECIPIENT_INVALID", warning.Line);
        Assert.DoesNotContain("@acme.test", warning.Line); // codes, never an address

        var swept = await reminders.Handle(new SendDueSoonRemindersCommand(Now, 100, "sweep-1"), CancellationToken.None);

        Assert.Equal(1, swept.Data!.Partial);
        Assert.Equal(0, swept.Data.RemindersSent);
        Assert.Equal(0, swept.Data.NotDelivered);
        Assert.False(string.IsNullOrWhiteSpace((await tasks.GetByIdAsync(task.Id, CancellationToken.None))!.LastDueSoonReminderKey)); // kept
    }

    [Fact]
    public async Task A_sweep_with_a_partly_sent_reminder_is_logged_as_a_Warning()
    {
        var logger = new Lines<Diten.Platform.Application.Features.Tasks.BackgroundJobs.TaskDueSoonSweepJob>();
        var job = new Diten.Platform.Application.Features.Tasks.BackgroundJobs.TaskDueSoonSweepJob(
            new ActiveTenants(), new FakeTenantContext(Tenant), new OneSweepAnswer(new SendDueSoonRemindersResponse(1, 0, 0, 0, 0, Partial: 1)), logger);

        await job.HandleAsync(
            new Diten.Platform.Application.Features.Tasks.BackgroundJobs.TaskDueSoonSweepJobArgs(0),
            new Diten.BuildingBlocks.BackgroundJobs.BackgroundJobContext(),
            CancellationToken.None);

        var line = Assert.Single(logger.Entries, e => e.Line.Contains("task.duesoon.sweep.completed", StringComparison.Ordinal));
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, line.Level);
        Assert.Contains("Partial=1", line.Line);
    }

    [Fact]
    public async Task A_cancellation_the_caller_did_not_ask_for_is_one_groups_failure()
    {
        // A provider's own timeout (TaskCanceledException) with the caller's token NOT cancelled: the next group still goes.
        var (adapter, queue) = Build(throwLocale: "fr", throwForeignCancellation: true);

        var result = await adapter.DispatchByEventCodeAsync(Request(
            new EmailRecipientDto("fay@acme.test", "Fay", "fr"),
            new EmailRecipientDto("ruslan@acme.test", "Ruslan", "ru")));

        Assert.True(result.IsSuccessful);
        Assert.Equal(NotificationEventDispatchAdapter.ReasonPartialRecipientFailure, result.ReasonCode);
        Assert.Equal(["ruslan@acme.test"], queue.Queued.SelectMany(c => c.Request.To).Select(r => r.Email));
    }

    [Fact]
    public async Task Copies_on_a_provider_refused_row_are_not_sent_again_with_the_next_group()
    {
        // PROVIDER_REJECTED leaves its dispatch row — with the copies — for the retry job: the next group must not carry them.
        var (adapter, queue) = Build(failLocale: "fr", failReason: "PROVIDER_REJECTED");
        var request = Request(
            new EmailRecipientDto("fay@acme.test", "Fay", "fr"),
            new EmailRecipientDto("ruslan@acme.test", "Ruslan", "ru")) with
        {
            Cc = [new EmailRecipientDto("cc@acme.test", "Cc")],
            Bcc = [new EmailRecipientDto("bcc@acme.test", "Bcc")]
        };

        await adapter.DispatchByEventCodeAsync(request);

        Assert.Equal(1, queue.Sent.Count(c => c.Request.Cc is { Count: > 0 }));
        Assert.Equal(1, queue.Sent.Count(c => c.Request.Bcc is { Count: > 0 }));
        Assert.Null(Assert.Single(queue.Queued).Request.Cc);
    }

    [Fact]
    public async Task The_same_address_twice_in_to_keeps_the_entry_that_knows_a_language()
    {
        var (adapter, queue) = Build();

        await adapter.DispatchByEventCodeAsync(Request(
            new EmailRecipientDto("ana@acme.test", "Ana"),
            new EmailRecipientDto("ANA@acme.test", "Ana", "es")));

        var command = Assert.Single(queue.Sent);
        Assert.Equal("es", command.Request.Locale);
        Assert.Single(command.Request.To);
    }

    [Fact]
    public async Task A_reader_the_real_validator_refuses_is_a_partial_failure_and_the_others_get_the_reminder()
    {
        // The real pipeline validator (IsRecipientRefusal), not a scripted refusal: Ruslan's stored address is not one
        // plain address. The French reader still gets the mail; the call is a partial success.
        var mediator = new TaskNotificationLocaleTests.RealValidationMediator();
        var adapter = new NotificationEventDispatchAdapter(
            new OneEvent(new NotificationEventDefinition
            {
                EventCode = TaskNotificationEvents.DueSoon, SourceType = NotificationEventSourceType.PlatformSeed, OwnerModuleId = "MOD-0024",
                Channel = NotificationChannelCode.Email, DefaultTemplateKey = TaskNotificationEvents.DueSoon + ".email",
                FallbackDisplayName = TaskNotificationEvents.DueSoon, Status = NotificationEventStatus.Active
            }),
            mediator, new TenantLanguageResolver(), NullLogger<NotificationEventDispatchAdapter>.Instance);

        var result = await adapter.DispatchByEventCodeAsync(new NotificationEventDispatchRequest(
            Tenant, TaskNotificationEvents.DueSoon,
            [new EmailRecipientDto("fay@acme.test", "Fay", "fr"), new EmailRecipientDto("ruslan@acme.test, other@evil.test", "Ruslan", "ru")],
            new Dictionary<string, object?> { ["TaskTitle"] = "Review" }));

        Assert.True(result.IsSuccessful);
        Assert.Equal(NotificationEventDispatchAdapter.ReasonPartialRecipientFailure, result.ReasonCode);
        Assert.Contains(result.Errors, e => e.StartsWith("ru:", StringComparison.Ordinal) && e.Contains("RECIPIENT_INVALID", StringComparison.Ordinal));
        Assert.Equal(["fay@acme.test"], mediator.Queued.SelectMany(c => c.Request.To).Select(r => r.Email));
    }

    private static readonly Guid Fay = Guid.Parse("f0f0f0f0-0000-0000-0000-00000000f0f0");
    private static readonly Guid Ruslan = Guid.Parse("e0e0e0e0-0000-0000-0000-00000000e0e0");
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    private static (TaskItem Task, FakeTaskItemRepository Tasks, TaskNotificationService Notifications, Lines<TaskNotificationService> Logger, RecordingQueue Queue)
        PoolReminder(string failLocale)
    {
        var position = Guid.NewGuid();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), TenantId = Tenant, Title = "Pool deadline", Lifecycle = TaskLifecycle.InProgress,
            AssignmentTarget = TaskAssignmentTarget.PositionPool, PoolPositionId = position, OrganizationUnitId = Guid.NewGuid(),
            EmailNotificationsEnabled = true, DueAt = Now.AddDays(1), ReminderLeadDays = 3, Version = 1
        };
        var tasks = new FakeTaskItemRepository(task);
        var (adapter, queue) = Build(failLocale: failLocale, eventCode: TaskNotificationEvents.DueSoon);
        var logger = new Lines<TaskNotificationService>();
        var notifications = new TaskNotificationService(
            adapter,
            new FakeNotificationLocaleResolver(),
            new LanguagePeople((Fay, "fay@acme.test", "fr"), (Ruslan, "ruslan@acme.test", "ru")),
            new FakePositionAssignmentRepository(Holder(position, Fay), Holder(position, Ruslan)),
            new FakeUserNotificationRepository(),
            new FakeTenantContext(Tenant),
            logger);
        return (task, tasks, notifications, logger, queue);
    }

    internal sealed class Lines<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<(Microsoft.Extensions.Logging.LogLevel Level, string Line)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    /// <summary>The sweep's per-tenant command, answering a fixed result.</summary>
    private sealed class OneSweepAnswer(SendDueSoonRemindersResponse answer) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            Task.FromResult((TResponse)(object)Response<SendDueSoonRemindersResponse>.Success(answer));
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }

    /// <summary>One active tenant; nothing else of the registry is used by the sweep.</summary>
    private sealed class ActiveTenants : ITenantRegistryRepository
    {
        public Task<IReadOnlyList<Diten.Platform.Domain.Entities.Tenant>> GetActiveTenantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Diten.Platform.Domain.Entities.Tenant>>(
            [
                new Diten.Platform.Domain.Entities.Tenant { Id = Tenant, Code = "LANG", Slug = "lang", Name = "lang", DisplayName = "Lang", Domain = "lang.test", Region = "EU", Environment = "Production" }
            ]);
        public Task<Diten.Platform.Domain.Entities.Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Diten.Platform.Domain.Entities.Tenant t, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Diten.Platform.Domain.Entities.Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Diten.Platform.Domain.Entities.Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Diten.Platform.Domain.Entities.Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Diten.Platform.Domain.Entities.Tenant> CreateAsync(Diten.Platform.Domain.Entities.Tenant t, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateStatusAsync(Guid id, Diten.Platform.Domain.Entities.TenantStatus status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Diten.Platform.Domain.Entities.Tenant>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<Diten.Platform.Domain.Entities.Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    // ---------------------------------------------------------------- the three other producers pass the person's language

    [Fact]
    public async Task The_timer_auto_close_mail_is_in_the_persons_language()
    {
        var user = Guid.NewGuid();
        var dispatched = new RecordingNotificationDispatchAdapter();
        var notifier = new TimerAutoCloseNotifier(dispatched, new LanguagePeople((user, "kim@acme.test", "zh")), new Links(),
            NullLogger<TimerAutoCloseNotifier>.Instance);

        await notifier.NotifyAsync(new TimerSegment
        {
            TenantId = Tenant, UserId = user, LocalDate = new DateOnly(2026, 10, 8), TimeZoneId = "UTC", WeekKey = "2026-W41",
            DurationSeconds = 3600
        });

        Assert.Equal("zh", Assert.Single(Assert.Single(dispatched.Requests).To).Locale);
    }

    [Fact]
    public async Task The_weekly_timesheet_reminder_is_in_the_persons_language()
    {
        var user = Guid.NewGuid();
        var dispatched = new RecordingNotificationDispatchAdapter();
        var notifier = new TimeEntryNotifier(
            dispatched, new LanguagePeople((user, "ana@acme.test", "es")), new FreshMarks(), new NoNames(), new Links(),
            new FakeTenantContext(Tenant), TimeProvider.System, NullLogger<TimeEntryNotifier>.Instance);

        await notifier.WeekReminderAsync(user, new DateOnly(2026, 10, 5));

        Assert.Equal("es", Assert.Single(Assert.Single(dispatched.Requests).To).Locale);
    }

    // ---------------------------------------------------------------- helpers

    private static PositionAssignment Holder(Guid position, Guid user) => new()
    {
        TenantId = Tenant, PositionId = position, UserId = user, EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30), EffectiveTo = null
    };

    private static (NotificationEventDispatchAdapter Adapter, RecordingQueue Queue) Build(
        string? failLocale = null, string? throwLocale = null, string eventCode = EventCode,
        string failReason = "RECIPIENT_INVALID", bool throwForeignCancellation = false)
    {
        var queue = new RecordingQueue(failLocale, throwLocale, failReason, throwForeignCancellation);
        var events = new OneEvent(new NotificationEventDefinition
        {
            EventCode = eventCode, SourceType = NotificationEventSourceType.PlatformSeed, OwnerModuleId = "MOD-0024",
            Channel = NotificationChannelCode.Email, DefaultTemplateKey = eventCode + ".email", FallbackDisplayName = eventCode,
            Status = NotificationEventStatus.Active
        });
        return (new NotificationEventDispatchAdapter(events, queue, new TenantLanguageResolver(),
            NullLogger<NotificationEventDispatchAdapter>.Instance), queue);
    }

    private static NotificationEventDispatchRequest Request(params EmailRecipientDto[] to) =>
        new(Tenant, EventCode, to, new Dictionary<string, object?> { ["TaskTitle"] = "Review" });

    private sealed class TenantLanguageResolver : INotificationLocaleResolver
    {
        public Task<string> ResolveAsync(Guid tenantId, string? requested, CancellationToken ct = default) =>
            Task.FromResult(string.IsNullOrWhiteSpace(requested) ? TenantLanguage : requested);
    }

    /// <summary>The queue: every command it was handed (Sent) and those it accepted (Queued); one locale is refused or throws.</summary>
    private sealed class RecordingQueue(string? failLocale, string? throwLocale, string failReason = "RECIPIENT_INVALID", bool throwForeignCancellation = false) : IMediator
    {
        public List<QueueEmailNotificationCommand> Sent { get; } = [];
        public List<QueueEmailNotificationCommand> Queued { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var command = (QueueEmailNotificationCommand)(object)request;
            Sent.Add(command);
            if (command.Request.Locale == throwLocale)
            {
                // A provider's OWN timeout is a cancellation the caller did not ask for (FIX2).
                throw throwForeignCancellation
                    ? new TaskCanceledException("the provider timed out")
                    : new InvalidOperationException("the queue is down for this group");
            }

            if (command.Request.Locale == failLocale)
            {
                return Task.FromResult((TResponse)(object)Response<NotificationDispatchDto>.Fail("refused", 400, failReason));
            }

            Queued.Add(command);
            return Task.FromResult((TResponse)(object)Response<NotificationDispatchDto>.Success(new NotificationDispatchDto(
                Guid.NewGuid(), Tenant, command.Request.TemplateKey, null, command.Request.Locale, "Email", "Smtp", null, "Queued",
                command.Request.To, 0, 0, "", null, null, "{}", DateTimeOffset.UtcNow, null, null, 0, null, null, null), 202));
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class OneEvent(NotificationEventDefinition definition) : INotificationEventDefinitionRepository
    {
        public Task<NotificationEventDefinition?> GetByEventCodeAsync(string eventCode, CancellationToken ct = default) =>
            Task.FromResult<NotificationEventDefinition?>(eventCode == definition.EventCode ? definition : null);
        public Task<NotificationEventDefinition> CreateAsync(NotificationEventDefinition d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(NotificationEventDefinition d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NotificationEventDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NotificationEventDefinition>> ListAsync(
            string? ownerModuleId = null, NotificationChannelCode? channel = null, NotificationEventStatus? status = null,
            bool? canTenantOverride = null, NotificationEventUsageType? usageType = null,
            int skip = 0, int take = 100, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NotificationEventDefinition>> ListActiveAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>AuthService's contacts, with each person's preferred language.</summary>
    internal sealed class LanguagePeople(params (Guid Id, string Email, string Language)[] people) : ITaskNotificationRecipientResolver
    {
        public Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TaskNotificationRecipient>>(people
                .Where(p => userIds.Contains(p.Id))
                .Select(p => new TaskNotificationRecipient(p.Id, p.Email, p.Email, p.Language))
                .ToList());
    }

    private sealed class Links : ITimeEntryLinks
    {
        public string MyWeek(string weekKey) => "https://app.test/TimeEntry?week=" + weekKey;
        public string ApprovalWeek(Guid weekId) => "https://app.test/TimeEntry/Approvals/" + weekId;
    }

    private sealed class FreshMarks : ITimeEntryNotificationMarkRepository
    {
        private readonly HashSet<string> _claimed = [];
        public Task<bool> TryClaimAsync(string kind, string key, DateTimeOffset claimedAtUtc, CancellationToken ct = default) =>
            Task.FromResult(_claimed.Add(kind + "|" + key));
        public Task<bool> ExistsAsync(string kind, string key, CancellationToken ct = default) =>
            Task.FromResult(_claimed.Contains(kind + "|" + key));
    }

    private sealed class NoNames : IUserDisplayNameResolver
    {
        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }
}
