using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prometheus;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>BL-454 — WP-EMAIL-SHELL-01 slice 2, stage A, fix round 1 (the in-memory half; the Mongo half is in
/// NotificationDispatchRetentionMongoTests).</summary>
public sealed partial class EmailShellDispatchTests
{
    // ---------------------------------------------------------------- 2. a failed effect leaves the row pending

    [Fact]
    public async Task An_effect_that_fails_once_leaves_the_row_pending_and_the_next_sweep_writes_the_notification_exactly_once()
    {
        var rig = new Rig();
        var meetings = new MeetingDoubles { ThrowOnNoticeTimes = 1 };
        var row = PendingMeetingRow(rig, meetings);
        var logger = new LinesLogger<NotificationPermanentFailureEffects>();
        var sweep = PendingSweep(rig, meetings, new TenantContext(), logger);

        await RunAsync(sweep);

        Assert.Empty(meetings.OrganizerNotices);
        Assert.True(NotificationDispatch.IsPermanentFailurePending(row)); // not marked: the effect failed
        Assert.Contains(logger.Lines, line => line.Contains("effects_failed", StringComparison.Ordinal) && line.Contains("Attempt=1", StringComparison.Ordinal));

        await RunAsync(sweep); // inside the grace of its own claim: left alone
        Assert.Empty(meetings.OrganizerNotices);

        row.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30); // the grace has passed
        await RunAsync(sweep);
        row.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        await RunAsync(sweep);

        Assert.Single(meetings.OrganizerNotices);
        Assert.False(NotificationDispatch.IsPermanentFailurePending(row));
        Assert.True(row.PermanentlyFailedNotifiedAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task Effects_that_keep_failing_are_given_up_after_the_last_attempt_named_counted_and_logged()
    {
        var rig = new Rig();
        var meetings = new MeetingDoubles { ThrowOnNoticeTimes = int.MaxValue };
        var key = UniqueKey("platform.meetings");
        var row = PendingMeetingRow(rig, meetings, key);
        var logger = new LinesLogger<NotificationPermanentFailureEffects>();
        var sweep = PendingSweep(rig, meetings, new TenantContext(), logger);
        var givenUp = GivenUp(key);
        var permanentlyFailed = Counter(key, meeting: true);

        for (var attempt = 1; attempt <= NotificationPermanentFailureEffects.MaxAttempts; attempt++)
        {
            Assert.True(NotificationDispatch.IsPermanentFailurePending(row), $"attempt {attempt}");
            row.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
            await RunAsync(sweep);
        }

        Assert.Equal(NotificationPermanentFailureEffects.MaxAttempts, meetings.NoticeAttempts);
        Assert.Equal(givenUp + 1, GivenUp(key));
        Assert.Equal(permanentlyFailed + 1, Counter(key, meeting: true)); // C-FIX1 4: the main counter too, once
        Assert.Single(logger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error
            && e.Line.Contains("effects_given_up", StringComparison.Ordinal) && e.Line.Contains("Reason=EffectsKeptFailing", StringComparison.Ordinal));
        Assert.False(NotificationDispatch.IsPermanentFailurePending(row)); // given up: no endless re-drive
        Assert.Empty(meetings.OrganizerNotices);
    }

    // ---------------------------------------------------------------- 4. a normal permanent failure ends with the real time

    [Fact]
    public async Task A_normal_permanent_failure_through_the_transition_command_ends_with_the_real_time_not_pending()
    {
        var rig = new Rig();
        var row = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 2);
        var before = DateTimeOffset.UtcNow;

        var response = await new MarkNotificationDispatchFailedHandler(rig.Dispatches, new NotificationsSmtpIntegrationTests.RecordingEventBus())
            .Handle(new MarkNotificationDispatchFailedCommand(rig.TenantId, row.Id, "SMTP_TIMEOUT", "SMTP_TIMEOUT", RetryCount: 5, IsPermanentFailure: true), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.False(NotificationDispatch.IsPermanentFailurePending(row));
        Assert.True(row.PermanentlyFailedNotifiedAt >= before, row.PermanentlyFailedNotifiedAt?.ToString("O"));
        Assert.Empty(await rig.Dispatches.FindPermanentFailurePendingAsync(DateTimeOffset.UtcNow.AddHours(1), 50));
    }

    [Fact]
    public async Task A_first_send_failure_with_no_retry_job_ends_with_the_real_time_not_pending()
    {
        var rig = new Rig { JobOptions = new BackgroundJobSchedulerOptions(), PermanentFailure = new MeetingDoubles().Effects() };
        rig.AddTemplate("en", "<p>x</p>", "x");
        rig.Transport.SendThrow = new InvalidOperationException("smtp down");
        var before = DateTimeOffset.UtcNow;

        await rig.QueueAsync("en", new() { ["TaskTitle"] = "x" });

        var row = Assert.Single(rig.Dispatches.Items);
        Assert.Equal(NotificationDispatchStatus.Failed, row.Status);
        Assert.False(NotificationDispatch.IsPermanentFailurePending(row));
        Assert.True(row.PermanentlyFailedNotifiedAt >= before, row.PermanentlyFailedNotifiedAt?.ToString("O"));
        Assert.Empty(await rig.Dispatches.FindPermanentFailurePendingAsync(DateTimeOffset.UtcNow.AddHours(1), 50));
    }

    // ---------------------------------------------------------------- 5a. every re-driven effect in its own tenant

    [Fact]
    public async Task Each_re_driven_meeting_effect_runs_in_its_own_rows_tenant_and_no_tenant_is_left_behind()
    {
        var rig = new Rig();
        var ambient = new TenantContext();
        var meetings = new MeetingDoubles { Ambient = ambient };
        var first = PendingMeetingRow(rig, meetings);
        var second = PendingMeetingRow(rig, meetings);
        second.TenantId = Guid.NewGuid();

        await RunAsync(PendingSweep(rig, meetings, ambient));

        // Per row: the attendee badge, then the organizer notification — each under that row's tenant.
        Assert.Equal(new Guid?[] { first.TenantId, first.TenantId, second.TenantId, second.TenantId }, meetings.TenantsSeen);
        Assert.False(ambient.IsResolved);
    }

    // ---------------------------------------------------------------- 8. state checks

    [Fact]
    public void A_send_that_lands_clears_a_pending_marker()
    {
        var row = new NotificationDispatch { Status = NotificationDispatchStatus.Failed, PermanentlyFailedNotifiedAt = NotificationDispatch.PermanentFailurePending };

        Assert.True(row.TryMarkSent("provider-id", DateTimeOffset.UtcNow));

        Assert.Equal(NotificationDispatchStatus.Sent, row.Status);
        Assert.Null(row.PermanentlyFailedNotifiedAt);
    }

    [Fact]
    public async Task A_pending_row_that_is_Sent_by_now_gets_no_undelivered_effect()
    {
        var rig = new Rig();
        var meetings = new MeetingDoubles();
        var row = PendingMeetingRow(rig, meetings);
        row.Status = NotificationDispatchStatus.Sent; // a retry landed between the read and the re-drive

        var applied = await meetings.Effects().ApplyAndMarkAsync(row, silent: false, rig.Dispatches, CancellationToken.None);

        Assert.False(applied);
        Assert.Empty(meetings.Undelivered);
        Assert.Empty(meetings.OrganizerNotices);
    }

    // ---------------------------------------------------------------- 6. the secret shapes the review found

    public static TheoryData<string, bool> SecretShapes() => new()
    {
        // PEM out of the look-behind: preceded by a word character, and inside a JSON-escaped bundle.
        { "x-----BEGIN " + "PRIVATE KEY-----", true },
        { "{\"bundle\":\"-----BEGIN CERTIFICATE-----\\nTEST\\n-----END CERTIFICATE-----\\n-----BEGIN " + "PRIVATE KEY-----\\nTEST-ONLY\"}", true },
        // PGP.
        { "-----BEGIN PGP " + "PRIVATE KEY BLOCK-----\nTEST-ONLY", true },
        // AIza with no look-ahead: 35 after the prefix and then more, or a letter / '_' / '-' right after.
        { "AI" + "za" + new string('T', 36), true },
        { "AI" + "za" + new string('T', 35) + "x", true },
        { "AI" + "za" + new string('T', 35) + "_", true },
        { "AI" + "za" + new string('T', 35) + "-", true },
        // C-FIX1 K3 — right after a JSON escape (\n, \t, \r): the escape's letter is not a word that hides the key.
        { "{\"error\":\"x\\nAK" + "IA" + new string('T', 16) + "\"}", true },
        { "{\"error\":\"x\\tAI" + "za" + new string('T', 35) + "\"}", true },
        { "{\"error\":\"x\\rsk" + "-" + new string('t', 20) + "\"}", true },
        { "{\"error\":\"x\\ngh" + "p_" + new string('t', 36) + "\"}", true },
        { "{\"error\":\"x\\ney" + "J" + "abcdef.ghijkl\"}", true },
        { "desk-mounted-display", false },
        { "xAK" + "IA" + new string('T', 16), false },
        // Still NOT secrets: the word alone, a short AIza, a public certificate.
        { "AI" + "za", false },
        { "AI" + "za" + new string('T', 34), false },
        { "AI" + "zaShortValue", false },
        { "-----BEGIN CERTIFICATE----- is public", false }
    };

    [Theory]
    [MemberData(nameof(SecretShapes))]
    public void The_secret_detector_matches_the_shapes_the_review_named(string text, bool isSecret) =>
        Assert.Equal(isSecret, NotificationSecrets.IsSecretText(text));

    // ---------------------------------------------------------------- 7. the error code / message path

    [Fact]
    public async Task A_provider_error_that_is_a_google_key_is_stored_redacted_on_the_dispatch()
    {
        var rig = new Rig();
        var template = rig.AddTemplate("en", "<p>{{TaskTitle}}</p>", "{{TaskTitle}}");
        var row = rig.AddFailedDispatch("{\"TaskTitle\":\"Batch\"}", template.Id);
        var job = new EmailDispatchJob(
            rig.Dispatches, new TenantMessagingSettingsResolver(rig.Settings),
            new NotificationsSmtpIntegrationTests.TestProviderResolver(new BlankFailure(TestOnlyGoogleKey)), new ValidatingMediator(rig.Dispatches),
            NullLogger<EmailDispatchJob>.Instance, NoInvitationLedger.Instance, rig.Templates, new EmailTemplateRenderer(), rig.Composer);

        await job.HandleAsync(new EmailDispatchJobArgs(rig.TenantId, row.Id), new BackgroundJobContext(), CancellationToken.None);

        Assert.Equal("[REDACTED]", row.ErrorCode);
        Assert.Equal("[REDACTED]", row.ErrorMessage);
        Assert.True(NotificationParsing.LooksLikeRawSecret(TestOnlyGoogleKey));
    }

    // ---------------------------------------------------------------- helpers

    private static NotificationDispatch PendingMeetingRow(Rig rig, MeetingDoubles meetings, string templateKey = MeetingKey)
    {
        var row = MeetingRow(rig, meetings, hoursAgo: 2, templateKey);
        row.PermanentlyFailedNotifiedAt = NotificationDispatch.PermanentFailurePending;
        row.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30); // the source transition wrote it half an hour ago
        return row;
    }

    private static EmailDispatchSweepJob PendingSweep(
        Rig rig, MeetingDoubles meetings, ITenantContext ambient, Microsoft.Extensions.Logging.ILogger? effectsLogger = null) =>
        TestSweeps.Create(
            rig.Dispatches,
            new RecordingScheduler(),
            NullLogger<EmailDispatchSweepJob>.Instance,
            new ValidatingMediator(rig.Dispatches, meetings),
            Options.Create(new EmailDispatchRetentionOptions { RetryWindowHours = 24 }),
            ambient,
            new NotificationPermanentFailureEffects(effectsLogger ?? NullLogger.Instance, meetings.Meetings, meetings.Attendees, meetings.Notifications));

    private static Task RunAsync(EmailDispatchSweepJob sweep) =>
        sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

    private static double GivenUp(string templateKey) =>
        Metrics.CreateCounter(
                "notification_dispatch_permanent_effects_given_up",
                "Permanent-failure effects (organizer notification, attendee badge) that kept failing and were given up.",
                new CounterConfiguration { LabelNames = ["template_key"] })
            .WithLabels(templateKey).Value;
}
