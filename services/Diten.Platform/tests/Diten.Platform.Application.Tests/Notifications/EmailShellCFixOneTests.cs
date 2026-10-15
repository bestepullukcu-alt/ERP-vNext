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
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>BL-454 — WP-EMAIL-SHELL-01 slice 2, the A-FIX1 + C fix round (P-2026-10-07-EMAIL-S2C-FIX1), in memory.</summary>
public sealed partial class EmailShellDispatchTests
{
    // ---------------------------------------------------------------- 2 / K8 / K9: the policy decides, closed by default

    [Theory]
    [InlineData((NotificationFallbackPolicy)99)]
    [InlineData((NotificationFallbackPolicy)3)]
    public async Task An_unknown_fallback_policy_never_opens_the_platform_mailbox(NotificationFallbackPolicy policy)
    {
        var rig = new Rig();
        var own = Rig.SettingsRow(rig.TenantId, "Diten Pharma", null);
        own.IsEnabled = false;
        own.FallbackPolicy = policy;
        await rig.Settings.CreateAsync(own);

        var resolved = await new TenantMessagingSettingsResolver(rig.Settings).ResolveAsync(rig.TenantId);
        var provided = await rig.Provider.SendEmailAsync(ProviderRequest(rig));

        Assert.False(resolved.IsSuccessful);
        Assert.Equal(MessagingSettingsSelection.ReasonTenantFallbackPolicyUnknown, resolved.ReasonCode);
        Assert.False(provided.Accepted);
        Assert.Equal(MessagingSettingsSelection.ReasonTenantFallbackPolicyUnknown, provided.ErrorCode);
        Assert.Null(rig.Transport.LastSentMessage);
    }

    [Theory]
    [InlineData(false)] // no tenant row
    [InlineData(true)]  // the tenant's row is disabled and says "use the platform default"
    public async Task A_disabled_platform_default_is_never_used(bool tenantRowDisabled)
    {
        var rig = new Rig();
        (await rig.Settings.GetPlatformDefaultAsync())!.IsEnabled = false;
        if (tenantRowDisabled)
        {
            var own = Rig.SettingsRow(rig.TenantId, "Diten Pharma", null);
            own.IsEnabled = false;
            own.FallbackPolicy = NotificationFallbackPolicy.UsePlatformDefault;
            await rig.Settings.CreateAsync(own);
        }

        var resolved = await new TenantMessagingSettingsResolver(rig.Settings).ResolveAsync(rig.TenantId);
        var provided = await rig.Provider.SendEmailAsync(ProviderRequest(rig));

        Assert.Equal(MessagingSettingsSelection.ReasonPlatformDefaultUnavailable, resolved.ReasonCode);
        Assert.Equal(MessagingSettingsSelection.ReasonPlatformDefaultUnavailable, provided.ErrorCode);
        Assert.Null(rig.Transport.LastSentMessage);
    }

    [Fact]
    public async Task A_deleted_tenant_row_decides_nothing_the_platform_default_is_used_as_if_there_were_none()
    {
        var rig = new Rig();
        var own = Rig.SettingsRow(rig.TenantId, "Diten Pharma", null);
        own.IsEnabled = false;
        own.FallbackPolicy = NotificationFallbackPolicy.DisableSending;
        own.IsDeleted = true;
        await rig.Settings.CreateAsync(own);

        var resolved = await new TenantMessagingSettingsResolver(rig.Settings).ResolveAsync(rig.TenantId);
        var provided = await rig.Provider.SendEmailAsync(ProviderRequest(rig));

        Assert.True(resolved.IsSuccessful);
        Assert.True(resolved.Data!.IsPlatformDefault);
        Assert.True(provided.Accepted);
    }

    [Fact]
    public void A_deleted_tenant_row_handed_to_the_selection_rule_decides_nothing()
    {
        // The repositories already leave deleted rows out (the test above goes through them, so it cannot see this):
        // the rule itself must not let a deleted row's policy refuse — or a deleted enabled row send. Measured on the rule.
        var platform = new TenantMessagingSettings { IsPlatformDefault = true, IsEnabled = true, SenderEmail = "p@di10.test" };
        var refusing = new TenantMessagingSettings { TenantId = Guid.NewGuid(), IsEnabled = false, IsDeleted = true, FallbackPolicy = NotificationFallbackPolicy.DisableSending };
        var sending = new TenantMessagingSettings { TenantId = Guid.NewGuid(), IsEnabled = true, IsDeleted = true, SenderEmail = "t@tenant.test" };

        Assert.Equal((platform, (string?)null), MessagingSettingsSelection.Select(refusing, () => platform));
        Assert.Equal((platform, (string?)null), MessagingSettingsSelection.Select(sending, () => platform));
    }

    // ---------------------------------------------------------------- 3: the first send keeps the resolver's name

    [Theory]
    [InlineData("disable", MessagingSettingsSelection.ReasonTenantSendingDisabled)]
    [InlineData("failfast", MessagingSettingsSelection.ReasonTenantSettingsDisabled)]
    [InlineData("nodefault", MessagingSettingsSelection.ReasonPlatformDefaultUnavailable)]
    public async Task A_first_send_refused_by_the_settings_tells_the_producer_which_rule_refused_it(string kind, string expected)
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>x</p>", "x");
        if (kind == "nodefault")
        {
            (await rig.Settings.GetPlatformDefaultAsync())!.IsEnabled = false;
        }
        else
        {
            var own = Rig.SettingsRow(rig.TenantId, "Diten Pharma", null);
            own.IsEnabled = false;
            own.FallbackPolicy = kind == "disable" ? NotificationFallbackPolicy.DisableSending : NotificationFallbackPolicy.FailFast;
            await rig.Settings.CreateAsync(own);
        }

        var response = await rig.QueueAsync("en", new() { ["TaskTitle"] = "x" });

        Assert.False(response.IsSuccessful);
        Assert.Equal(expected, response.ReasonCode);
        Assert.Empty(rig.Dispatches.Items);
    }

    // ---------------------------------------------------------------- 4: the main counter and line, whatever the effects did

    [Fact]
    public async Task Effects_that_are_given_up_still_count_the_permanent_failure_once_and_write_its_line()
    {
        var rig = new Rig();
        var meetings = new MeetingDoubles { ThrowOnNoticeTimes = int.MaxValue };
        var key = UniqueKey("platform.meetings");
        var row = PendingMeetingRow(rig, meetings, key);
        var logger = new LinesLogger<NotificationPermanentFailureEffects>();
        var sweep = PendingSweep(rig, meetings, new TenantContext(), logger);
        var before = Counter(key, meeting: true);

        for (var attempt = 1; attempt <= NotificationPermanentFailureEffects.MaxAttempts; attempt++)
        {
            row.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
            await RunAsync(sweep);
        }

        Assert.Equal(before + 1, Counter(key, meeting: true)); // once, on the give-up — not zero, not five
        Assert.Single(logger.Lines, line => line.StartsWith("email.dispatch.permanently_failed DispatchId", StringComparison.Ordinal)
            && line.Contains("EffectsGivenUp=True", StringComparison.Ordinal));
    }

    [Fact]
    public async Task With_no_retry_job_a_failed_organizer_notification_still_counts_the_permanent_failure_and_writes_its_line()
    {
        var meetings = new MeetingDoubles { ThrowOnNoticeTimes = int.MaxValue };
        var key = UniqueKey("platform.meetings");
        var logger = new LinesLogger<NotificationPermanentFailureEffects>();
        var effects = new NotificationPermanentFailureEffects(logger, meetings.Meetings, meetings.Attendees, meetings.Notifications);
        var dispatch = new NotificationDispatch
        {
            TenantId = Guid.NewGuid(), TemplateKey = key, Status = NotificationDispatchStatus.Failed,
            CausationId = meetings.MeetingId, MeetingAttendeeUserId = Guid.NewGuid(),
            To = [new EmailRecipient { Email = "attendee@example.test" }]
        };
        var before = Counter(key, meeting: true);

        await effects.ApplyAsync(dispatch, silent: false, CancellationToken.None);

        Assert.Empty(meetings.OrganizerNotices);
        Assert.Equal(before + 1, Counter(key, meeting: true));
        Assert.Contains(logger.Lines, line => line.Contains("organizer_notification_write_failed", StringComparison.Ordinal));
        Assert.Contains(logger.Lines, line => line.StartsWith("email.dispatch.permanently_failed DispatchId", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- K10 (CT E1): far older than the window = silent

    [Fact]
    public async Task A_re_driven_row_far_older_than_the_retry_window_tells_no_organizer_but_is_still_counted()
    {
        var rig = new Rig();
        var meetings = new MeetingDoubles();
        var key = UniqueKey("platform.meetings");
        var row = PendingMeetingRow(rig, meetings, key);
        row.QueuedAt = DateTimeOffset.UtcNow.AddHours(-4 * 24); // the window is 24 h: more than 3× older
        var logger = new LinesLogger<NotificationPermanentFailureEffects>();
        var before = Counter(key, meeting: true);

        await RunAsync(PendingSweep(rig, meetings, new TenantContext(), logger));

        Assert.Empty(meetings.OrganizerNotices);
        Assert.Empty(meetings.Undelivered);
        Assert.Equal(before + 1, Counter(key, meeting: true));
        Assert.Contains(logger.Lines, line => line.StartsWith("email.dispatch.permanently_failed DispatchId", StringComparison.Ordinal)
            && line.Contains("Silent=True", StringComparison.Ordinal));
        Assert.False(NotificationDispatch.IsPermanentFailurePending(row));
    }

    // ---------------------------------------------------------------- K2: the grace comes from the options, clamped

    [Theory]
    [InlineData(30, false)] // idle 20 min, grace 30: still the source's
    [InlineData(10, true)]  // idle 20 min, grace 10: re-driven
    public async Task The_effects_grace_is_read_from_the_options(int graceMinutes, bool redriven)
    {
        var rig = new Rig();
        var meetings = new MeetingDoubles();
        var row = PendingMeetingRow(rig, meetings);
        row.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var sweep = TestSweeps.Create(rig.Dispatches, new RecordingScheduler(), NullLogger<EmailDispatchSweepJob>.Instance,
            new ValidatingMediator(rig.Dispatches, meetings),
            Options.Create(new EmailDispatchRetentionOptions { RetryWindowHours = 24, EffectsGraceMinutes = graceMinutes }),
            new TenantContext(), meetings.Effects());

        await RunAsync(sweep);

        Assert.Equal(redriven, meetings.OrganizerNotices.Count == 1);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(240, 240)]
    [InlineData(500, 240)]
    public void The_effects_grace_is_clamped_to_one_minute_up_to_four_hours(int configured, int effective) =>
        Assert.Equal(effective, EmailDispatchRetentionOptions.EffectiveGraceMinutes(configured));

    // ---------------------------------------------------------------- K5: a send keeps a REAL "not delivered" record

    [Fact]
    public async Task A_late_send_keeps_the_real_not_delivered_time_and_says_so_by_name()
    {
        var rig = new Rig();
        var row = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 2);
        var closedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        row.PermanentlyFailedNotifiedAt = closedAt;
        var logger = new LinesLogger<MarkNotificationDispatchSentHandler>();

        var response = await new MarkNotificationDispatchSentHandler(rig.Dispatches, new NotificationsSmtpIntegrationTests.RecordingEventBus(), logger)
            .Handle(new MarkNotificationDispatchSentCommand(rig.TenantId, row.Id, "late-provider-id"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(NotificationDispatchStatus.Sent, row.Status);
        Assert.Equal(closedAt, row.PermanentlyFailedNotifiedAt); // the effects happened; the record of them stays
        Assert.Contains(logger.Lines, line => line.Contains("sent_after_permanent_failure", StringComparison.Ordinal));
    }

    private static MessagingProviderEmailRequest ProviderRequest(Rig rig) => new(
        Guid.NewGuid(), rig.TenantId, "corr", "Subject",
        [new EmailRecipientDto("user@example.com", "User")], [], [],
        "<p>x</p>", "x", "<p>x</p>", "x", null, null);
}
