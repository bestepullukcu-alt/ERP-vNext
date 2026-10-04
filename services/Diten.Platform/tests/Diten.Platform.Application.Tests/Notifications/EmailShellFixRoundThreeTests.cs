using System.Text.Json;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Notifications.Validators;
using Diten.Platform.Application.Features.Tenants.Notifications;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Eventing;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 — fix round 3 of WP-EMAIL-SHELL-01. Same rig as the rest of this class (real handler, job, renderer, composer
/// and SMTP provider; doubled transport and repositories). Grouped by the prompt's item numbers.
/// </summary>
public sealed partial class EmailShellDispatchTests
{
    private static BackgroundJobSchedulerOptions RetriesOn() => new()
    {
        Enabled = true,
        RegisterStandardJobs = true,
        EnabledJobs = new(StringComparer.OrdinalIgnoreCase) { [EmailDispatchSweepJob.JobId] = true }
    };

    // ---------------------------------------------------------------- 1. retention

    [Fact]
    public async Task With_no_retry_on_this_server_a_failed_first_send_keeps_no_variables_and_is_the_permanent_failure()
    {
        // What production ships: BackgroundJobs:Enabled=false, EnabledJobs={} — the sweep never runs.
        var logger = new LinesLogger<QueueEmailNotificationHandler>();
        var rig = new Rig { JobOptions = new BackgroundJobSchedulerOptions(), HandlerLogger = logger };
        rig.AddTemplate("en", "<p>{{TaskTitle}}</p>", "{{TaskTitle}}");
        rig.Transport.SendThrow = new InvalidOperationException("smtp down");

        var response = await rig.QueueAsync("en", new() { ["TaskTitle"] = "Batch 24-118 review" });

        Assert.False(response.IsSuccessful);
        var dispatch = Assert.Single(rig.Dispatches.Items);
        Assert.Equal(NotificationDispatch.ReleasedVariablesJson, dispatch.VariablesJson);
        Assert.Equal(NotificationDispatchStatus.Failed, dispatch.Status);
        Assert.Null(dispatch.NextRetryAt);
        Assert.NotNull(dispatch.PermanentlyFailedNotifiedAt);
        Assert.Contains(logger.Entries, entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning
            && entry.Line.Contains("permanently_failed", StringComparison.Ordinal)
            && entry.Line.Contains("RetryUnavailable", StringComparison.Ordinal));
    }

    [Fact]
    public async Task With_the_retry_sweep_on_a_failed_first_send_keeps_its_variables_and_is_due()
    {
        var rig = new Rig { JobOptions = RetriesOn() };
        rig.AddTemplate("en", "<p>{{TaskTitle}}</p>", "{{TaskTitle}}");
        rig.Transport.SendThrow = new InvalidOperationException("smtp down");

        await rig.QueueAsync("en", new() { ["TaskTitle"] = "Batch 24-118 review" });

        var dispatch = Assert.Single(rig.Dispatches.Items);
        Assert.Contains("Batch 24-118 review", dispatch.VariablesJson);
        Assert.NotNull(dispatch.NextRetryAt);
        Assert.Null(dispatch.PermanentlyFailedNotifiedAt);
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, null, false)]
    public void A_retry_is_possible_only_behind_the_same_three_gates_the_registrar_uses(bool enabled, bool standard, bool? flag, bool expected)
    {
        var options = new BackgroundJobSchedulerOptions { Enabled = enabled, RegisterStandardJobs = standard };
        if (flag is { } value)
        {
            options.EnabledJobs[EmailDispatchSweepJob.JobId] = value;
        }

        Assert.Equal(expected, EmailDispatchSweepJob.IsScheduled(options));
        // The registrar registers the sweep under the very id the gate reads.
        var registration = new Diten.Platform.Application.BackgroundJobs.PlatformRecurringJobRegistrar(
                Options.Create(options), Options.Create(new Diten.Platform.Application.Features.WorkingCalendarImport.WorkingCalendarImportOptions()))
            .GetRecurringJobs()
            .Single(job => job.HandlerType == typeof(EmailDispatchSweepJob));
        Assert.Equal(EmailDispatchSweepJob.JobId, registration.Descriptor.Id);
    }

    [Fact]
    public async Task A_dispatch_whose_retry_window_passed_is_closed_as_a_permanent_failure_and_its_variables_released()
    {
        var rig = new Rig();
        var expired = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 30);
        var crashed = Waiting(rig, NotificationDispatchStatus.Queued, hoursAgo: 30);
        var fresh = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 2);
        var sent = Waiting(rig, NotificationDispatchStatus.Sent, hoursAgo: 30);
        var closed = Waiting(rig, NotificationDispatchStatus.Failed, hoursAgo: 30);
        closed.PermanentlyFailedNotifiedAt = DateTimeOffset.UtcNow.AddHours(-29);
        closed.ErrorCode = "EarlierPermanent";
        var sweep = new EmailDispatchSweepJob(
            rig.Dispatches,
            new RecordingScheduler(),
            NullLogger<EmailDispatchSweepJob>.Instance,
            new RoutingMediator(rig.Dispatches),
            Options.Create(new EmailDispatchRetentionOptions { RetryWindowHours = 24 }));

        await sweep.HandleAsync(new EmailDispatchSweepJobArgs(), new BackgroundJobContext(), CancellationToken.None);

        foreach (var row in new[] { expired, crashed })
        {
            Assert.Equal(NotificationDispatchStatus.Failed, row.Status);
            Assert.Equal(NotificationDispatch.ReleasedVariablesJson, row.VariablesJson);
            Assert.Equal(EmailDispatchSweepJob.RetryWindowExpiredCode, row.ErrorCode);
            Assert.NotNull(row.PermanentlyFailedNotifiedAt);
        }

        Assert.Contains("kept", fresh.VariablesJson);
        Assert.Null(fresh.PermanentlyFailedNotifiedAt);
        Assert.Contains("kept", sent.VariablesJson);
        Assert.Equal("EarlierPermanent", closed.ErrorCode);
    }

    [Fact]
    public async Task A_retry_job_never_sends_a_dispatch_already_closed_as_permanent()
    {
        var rig = new Rig();
        var template = rig.AddTemplate("en", "<p>{{TaskTitle}}</p>", "{{TaskTitle}}");
        var dispatch = rig.AddFailedDispatch(NotificationDispatch.ReleasedVariablesJson, template.Id);
        dispatch.PermanentlyFailedNotifiedAt = DateTimeOffset.UtcNow;

        await rig.Job().HandleAsync(new EmailDispatchJobArgs(rig.TenantId, dispatch.Id), new BackgroundJobContext(), CancellationToken.None);

        Assert.Null(rig.Transport.LastSentMessage);
    }

    private static NotificationDispatch Waiting(Rig rig, NotificationDispatchStatus status, int hoursAgo)
    {
        var dispatch = rig.AddFailedDispatch("{\"TaskTitle\":\"kept\"}", null);
        dispatch.Status = status;
        dispatch.QueuedAt = DateTimeOffset.UtcNow.AddHours(-hoursAgo);
        return dispatch;
    }

    // ---------------------------------------------------------------- 2. names

    [Theory]
    [InlineData("RESETTOKEN")] [InlineData("ACCESSTOKEN")] [InlineData("temppassword")] [InlineData("SetpasswordUrl")]
    [InlineData("secretkey")] [InlineData("ActivationCode")] [InlineData("InviteCode")] [InlineData("InvitationCode")]
    [InlineData("RecoveryCode")] [InlineData("BackupCode")] [InlineData("MfaCode")] [InlineData("OneTimeCode")]
    [InlineData("SmsCode")] [InlineData("ConfirmCode")] [InlineData("EncryptionKey")] [InlineData("LicenseKey")]
    [InlineData("SshKey")] [InlineData("Passphrase")] [InlineData("TempPass")] [InlineData("ConnectionString")]
    [InlineData("SessionId")] [InlineData("Nonce")] [InlineData("Tokenizer")] [InlineData("Code")] [InlineData("Key")]
    [InlineData("ResetCode")] [InlineData("AccessKey")] [InlineData("SigningKey")] [InlineData("MeetingPasscode")]
    public void A_real_world_secret_name_is_masked(string name) =>
        Assert.True(NotificationSecrets.IsSecretName(name), name);

    [Theory]
    [InlineData("TenantCode")] [InlineData("PostalCode")] [InlineData("CountryCode")] [InlineData("CurrencyCode")]
    [InlineData("ProductCode")] [InlineData("WeekKey")] [InlineData("ResultCode")] [InlineData("TypeCode")]
    [InlineData("StatusCode")] [InlineData("LanguageCode")] [InlineData("ColorCode")] [InlineData("KeyResult")]
    [InlineData("Shipping")] [InlineData("Pinned")] [InlineData("Keynote")] [InlineData("Barcode")] [InlineData("Compass")]
    [InlineData("Passenger")] [InlineData("LotCode")] [InlineData("module_code")]
    public void A_name_whose_short_word_sits_next_to_a_neutral_word_or_is_only_letters_is_not_masked(string name) =>
        Assert.False(NotificationSecrets.IsSecretName(name), name);

    // ---------------------------------------------------------------- 3. values

    [Theory]
    [InlineData("https://app.example/login?next=%2Freset%3Ftoken%3Dabc123")]
    [InlineData("https://idp.example/authorize?client_id=x&redirect_uri=https%3A%2F%2Fapp.example%2Fcb%3Fcode%3Dabc")]
    [InlineData("mailto:a@b.test?subject=Hi&body=Open%20https%3A%2F%2Fapp.example%2Freset%3Ftoken%3Dabc%20today")]
    [InlineData("example.com?token=abc123")]
    [InlineData("https://teams.example/meet/123?p=abc")]
    [InlineData("https://app.example/x?pass=abc")]
    [InlineData("https://dropbox.example/s/f?rlkey=abc")]
    [InlineData("https://sso.example/x?ticket=abc")]
    [InlineData("https://app.example/x?session=abc")]
    [InlineData("https://app.example/x?nonce=abc")]
    [InlineData("https://app.example/x?hash=abc")]
    [InlineData("https://app.example/x?invite=abc")]
    [InlineData("https://app.example/x?accesstoken=abc")]
    [InlineData("https://app.example/cb?code=abc")]
    [InlineData("https://app.example/x?a=1?token=abc")]
    [InlineData("https://app.example/x?a=1#token=abc")]
    [InlineData("token:eyJ" + "hbGciOiJIUzI1NiJ9.eyJ" + "zdWIiOiIxIn0.c2lnbmF0dXJl")] // shapes split: no secret scanner reads a key here
    [InlineData("eyJ" + "hbGciOiJIUzI1NiJ9.eyJ" + "zdWIiOiIxIn0.c2lnbmF0dXJl;")]
    [InlineData("{\"t\":\"eyJ" + "hbGciOiJIUzI1NiJ9.eyJ" + "zdWIiOiIxIn0.c2lnbmF0dXJl\"}")]
    [InlineData("sk_" + "live_" + "0123456789abcdef")]
    [InlineData("gh" + "p_" + "0123456789abcdefghijABCDEFGHIJ")]
    [InlineData("xo" + "xb-" + "1234-5678-abcdefgh")]
    [InlineData("AKIA" + "ABCDEFGHIJKLMNOP")]
    public void A_credential_in_a_newer_shape_is_masked_whatever_its_name(string value) =>
        Assert.True(NotificationParsing.IsSensitiveVariable("Note", value), value);

    [Theory]
    [InlineData("Why?token=abc")]
    [InlineData("https://di10.example/x?next=%2FWorkCenterNext%2FDetails%2F12")]
    [InlineData("task-1234567890abcdef")]
    [InlineData("MSG.abcdefghijklmnop")]
    [InlineData("Note.txt")]
    public void A_value_that_only_resembles_a_credential_is_not_masked(string value) =>
        Assert.False(NotificationParsing.IsSensitiveVariable("Note", value), value);

    [Fact]
    public void A_list_handed_in_by_a_caller_inside_the_process_is_never_stored() =>
        Assert.True(NotificationSecrets.IsSecretValue(new List<string> { "a", "b" }));

    [Fact]
    public async Task A_json_object_that_arrived_over_http_is_never_stored()
    {
        var rig = new Rig { JobOptions = RetriesOn() };
        rig.AddTemplate("en", "<p>{{TaskTitle}}</p>", "{{TaskTitle}}");
        using var json = JsonDocument.Parse("{\"Details\":{\"owner\":\"someone\"},\"TaskTitle\":\"Batch\"}");
        var variables = json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
        rig.Transport.SendThrow = new InvalidOperationException("smtp down"); // a sent row releases its variables

        await rig.QueueAsync("en", variables);

        var stored = JsonSerializer.Deserialize<Dictionary<string, string?>>(Assert.Single(rig.Dispatches.Items).VariablesJson)!;
        Assert.Equal(QueueEmailNotificationHandler.RedactedToken, stored["Details"]);
        Assert.Equal("Batch", stored["TaskTitle"]);
    }

    // ---------------------------------------------------------------- 4. subject: masked first, cleaned and cut second

    [Theory]
    [InlineData(238)] [InlineData(243)] [InlineData(245)] [InlineData(248)] [InlineData(252)]
    public async Task A_secret_falling_on_the_subjects_cut_leaves_nothing_of_itself_in_the_stored_subject(int fillerLength)
    {
        const string secret = "Tmp-Pass-1234567890";
        var rig = new Rig();
        rig.AddTemplate("en", "<p>x</p>", "x", subject: "{{Filler}}{{TemporaryPassword}}");

        await rig.QueueAsync("en", new() { ["Filler"] = new string('a', fillerLength), ["TemporaryPassword"] = secret });

        var stored = Assert.Single(rig.Dispatches.Items).Subject;
        for (var k = 4; k <= secret.Length; k++)
        {
            Assert.DoesNotContain(secret[..k], stored);
        }
    }

    [Theory]
    [InlineData("Tmp\tPass-123456")]
    [InlineData("Tmp  Pass-123456")]
    [InlineData("Tmp­Pass-123456")]
    public async Task A_secret_the_subject_cleaning_would_change_is_still_masked_in_the_stored_subject(string secret)
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>x</p>", "x", subject: "Code {{TemporaryPassword}}");

        await rig.QueueAsync("en", new() { ["TemporaryPassword"] = secret });

        var stored = Assert.Single(rig.Dispatches.Items).Subject;
        Assert.DoesNotContain("Pass-123456", stored);
        Assert.Contains(QueueEmailNotificationHandler.RedactedToken, stored);
    }

    // ---------------------------------------------------------------- 5. overlapping secrets

    [Fact]
    public async Task A_short_secret_that_begins_a_longer_one_leaves_no_tail_of_the_longer_one()
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>{{ResetToken}}</p>", "{{ResetToken}}", subject: "{{ResetToken}}");

        // The short one is added FIRST: insertion order must not decide what is left behind.
        await rig.QueueAsync("en", new() { ["ResetCode"] = "abc123", ["ResetToken"] = "abc123-XYZ789" });

        var dispatch = Assert.Single(rig.Dispatches.Items);
        Assert.DoesNotContain("XYZ789", dispatch.Subject);
        Assert.DoesNotContain("XYZ789", dispatch.BodyHtmlPreview);
        Assert.DoesNotContain("XYZ789", dispatch.BodyTextPreview);
    }

    // ---------------------------------------------------------------- 7. in-process callers see RECIPIENT_INVALID

    [Fact]
    public async Task The_event_code_adapter_answers_a_refused_address_with_its_code()
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>x</p>", "x");
        var mediator = new PipelineMediator(rig);

        var response = await mediator.Send(new DispatchNotificationByEventCodeCommand(new NotificationEventDispatchRequest(
            rig.TenantId, PipelineMediator.EventCode, [new EmailRecipientDto("admin@localhost", "Admin")],
            new Dictionary<string, object?>(), "en")));

        Assert.False(response.IsSuccessful);
        Assert.Equal(QueueEmailNotificationHandler.ReasonRecipientInvalid, response.ReasonCode);
        Assert.Empty(rig.Dispatches.Items);
    }

    [Theory]
    [InlineData("suspended")]
    [InlineData("created")]
    public async Task A_tenant_event_whose_admin_address_can_never_be_valid_is_consumed_once_and_not_redelivered(string kind)
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>x</p>", "x");
        var adminId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = rig.TenantId, Code = "DITEN", Slug = "diten", Name = "diten-pharma", DisplayName = "Diten Pharma",
            Domain = "diten.test", Region = "EU", Environment = "Production", DefaultLanguage = "en"
        };
        tenant.AdminUsers.Add(new TenantAdminUser { Id = adminId, Name = "Admin", Email = "admin@localhost", Status = TenantAdminUserStatus.Active });
        rig.Tenants.Tenant = tenant;
        var consumed = new InMemoryConsumedEvents();
        var consumer = new TenantLifecycleNotificationConsumer(
            new ConsumedEventStore(consumed, NullLogger<ConsumedEventStore>.Instance),
            rig.Tenants,
            new PipelineMediator(rig),
            new TenantCreatedV1NotificationMapper(),
            new TenantSuspendedV1NotificationMapper(),
            new TenantReactivatedV1NotificationMapper(),
            NullLogger<TenantLifecycleNotificationConsumer>.Instance);
        var message = kind == "created"
            ? Message(TenantCreatedV1.Name, TenantCreatedV1.Version, rig.TenantId,
                new TenantCreatedV1(rig.TenantId, DateTimeOffset.UtcNow, null, Guid.NewGuid(), "Diten Pharma", "en", adminId))
            : Message(TenantSuspendedV1.Name, TenantSuspendedV1.Version, rig.TenantId,
                new TenantSuspendedV1(rig.TenantId, DateTimeOffset.UtcNow, "hold", Guid.NewGuid()));

        var result = await consumer.ConsumeAsync(message);

        Assert.Equal(ConsumedEventExecutionResult.Consumed, result);
        Assert.Equal(ConsumedEventStatus.Consumed, (await consumed.GetAsync(message.EventId, TenantLifecycleNotificationConsumer.ConsumerName))!.Status);
        Assert.Empty(rig.Dispatches.Items);
    }

    private static EventTransportMessage Message(string name, int version, Guid tenantId, object payload) =>
        new(Guid.NewGuid(), name, version, Guid.NewGuid(), Guid.NewGuid(), tenantId, "Diten.Platform.Tests", DateTimeOffset.UtcNow,
            JsonSerializer.Serialize(payload));

    /// <summary>The production validator in the production ValidationBehavior around the handler — what MediatR runs.</summary>
    private static Task<Response<NotificationDispatchDto>> Pipeline(Rig rig, QueueEmailNotificationCommand command) =>
        new ValidationBehavior<QueueEmailNotificationCommand, Response<NotificationDispatchDto>>([new QueueEmailNotificationValidator()])
            .Handle(command, () => rig.Handler().Handle(command, CancellationToken.None), CancellationToken.None);

    // ---------------------------------------------------------------- 9. small ones

    [Fact]
    public void A_stored_json_bool_reads_back_as_the_first_render_drew_it()
    {
        var first = NotificationVariables.Normalize(new Dictionary<string, object?> { ["Flag"] = true });
        var stored = NotificationVariables.FromJson("{\"Flag\":true,\"Off\":false}");

        Assert.Equal(first["Flag"], stored["Flag"]);
        Assert.Equal("True", stored["Flag"]);
        Assert.Equal("False", stored["Off"]);
    }

    [Fact]
    public async Task A_degraded_retry_is_a_warning_and_the_dispatch_itself_says_it_was_degraded()
    {
        var logger = new LinesLogger<EmailDispatchJob>();
        var rig = new Rig();
        var template = rig.AddTemplate("en", "<p>{{TemporaryPassword}}</p>", "{{TemporaryPassword}}");
        var dispatch = rig.AddFailedDispatch("{\"TemporaryPassword\":\"[REDACTED]\"}", template.Id);
        var job = new EmailDispatchJob(
            rig.Dispatches,
            new TenantMessagingSettingsResolver(rig.Settings),
            new NotificationsSmtpIntegrationTests.TestProviderResolver(rig.Provider),
            new RoutingMediator(rig.Dispatches),
            logger,
            rig.Templates,
            new EmailTemplateRenderer(),
            rig.Composer);

        await job.HandleAsync(new EmailDispatchJobArgs(rig.TenantId, dispatch.Id), new BackgroundJobContext(), CancellationToken.None);

        Assert.Equal(NotificationDispatchStatus.Sent, dispatch.Status);
        Assert.Equal(NotificationDispatch.RetryDegradedErrorCode, dispatch.ErrorCode);
        Assert.Contains("VariablesRedacted", dispatch.ErrorMessage);
        Assert.Contains(logger.Entries, entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning
            && entry.Line.Contains("retry_degraded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_full_retry_leaves_no_degraded_mark()
    {
        var rig = new Rig();
        var template = rig.AddTemplate("en", "<p>{{TaskTitle}}</p>", "{{TaskTitle}}");
        var dispatch = rig.AddFailedDispatch("{\"TaskTitle\":\"Batch\"}", template.Id);
        dispatch.ErrorCode = "ProviderConnectivityFailed";

        await new EmailDispatchJob(
                rig.Dispatches, new TenantMessagingSettingsResolver(rig.Settings),
                new NotificationsSmtpIntegrationTests.TestProviderResolver(rig.Provider), new RoutingMediator(rig.Dispatches),
                NullLogger<EmailDispatchJob>.Instance, rig.Templates, new EmailTemplateRenderer(), rig.Composer)
            .HandleAsync(new EmailDispatchJobArgs(rig.TenantId, dispatch.Id), new BackgroundJobContext(), CancellationToken.None);

        Assert.Equal(NotificationDispatchStatus.Sent, dispatch.Status);
        Assert.NotEqual(NotificationDispatch.RetryDegradedErrorCode, dispatch.ErrorCode);
    }

    [Fact]
    public async Task A_template_save_made_from_an_older_read_is_refused_and_writes_nothing()
    {
        var rig = new Rig();
        var template = rig.AddTemplate("en", "<p>current</p>", "current");
        template.Version = 3;
        var handler = new UpdateNotificationTemplateHandler(rig.Templates);

        var stale = await handler.Handle(new UpdateNotificationTemplateCommand(template.Id, null, Upsert(NotificationMappings.RowVersionOf(2))), CancellationToken.None);
        Assert.False(stale.IsSuccessful);
        Assert.Equal(409, stale.StatusCode);
        Assert.Equal(UpdateNotificationTemplateHandler.ReasonTemplateChanged, stale.ReasonCode);
        Assert.Equal("<p>current</p>", template.BodyHtmlTemplate);
        Assert.Equal(3, template.Version);

        var fresh = await handler.Handle(new UpdateNotificationTemplateCommand(template.Id, null, Upsert(NotificationMappings.RowVersionOf(3))), CancellationToken.None);
        Assert.True(fresh.IsSuccessful);
        Assert.Equal("<p>edited</p>", template.BodyHtmlTemplate);
        Assert.Equal(NotificationMappings.RowVersionOf(4), fresh.Data!.RowVersion);
    }

    [Fact]
    public async Task A_save_is_signed_with_the_signed_in_operators_own_id()
    {
        var rig = new Rig();
        var template = rig.AddTemplate("en", "<p>current</p>", "current");
        var operatorId = Guid.NewGuid();

        await new UpdateNotificationTemplateHandler(rig.Templates, new SignedIn(operatorId))
            .Handle(new UpdateNotificationTemplateCommand(template.Id, null, Upsert(null)), CancellationToken.None);

        Assert.Equal(operatorId.ToString(), template.UpdatedBy);
    }

    private static NotificationTemplateUpsertRequest Upsert(byte[]? rowVersion) => new(
        IsPlatformDefault: true,
        TemplateKey: TemplateKey,
        Channel: "Email",
        Locale: "en",
        SubjectTemplate: "Subject",
        BodyHtmlTemplate: "<p>edited</p>",
        BodyTextTemplate: "edited",
        Variables: [],
        Status: "Active",
        SemanticVersion: "1.0.0",
        RowVersion: rowVersion);

    // ---------------------------------------------------------------- untested rules, each with its own test

    [Fact]
    public async Task A_stored_row_whose_names_differ_only_in_case_still_retries_in_full()
    {
        var rig = new Rig();
        var template = rig.AddTemplate("en", "<p>Title: {{TaskTitle}}</p>", "Title: {{TaskTitle}}");
        // An older row: written before names were folded, from a caller whose dictionary told them apart.
        var dispatch = rig.AddFailedDispatch("{\"TaskTitle\":\"Batch 24-118\",\"tasktitle\":\"Batch 24-118\"}", template.Id);

        await rig.Job().HandleAsync(new EmailDispatchJobArgs(rig.TenantId, dispatch.Id), new BackgroundJobContext(), CancellationToken.None);

        Assert.Contains("Title: Batch 24-118", rig.Sent.HtmlBody);
        Assert.DoesNotContain("stored preview", rig.Sent.HtmlBody);
    }

    [Fact]
    public void The_variables_fixed_at_queue_time_are_looked_up_without_regard_to_case()
    {
        var normalized = NotificationVariables.Normalize(new Dictionary<string, object?>(StringComparer.Ordinal) { ["TaskTitle"] = "x" });
        Assert.True(normalized.TryGetValue("tasktitle", out var value));
        Assert.Equal("x", value);
    }

    [Fact]
    public void The_variables_read_back_from_a_dispatch_are_looked_up_without_regard_to_case()
    {
        var stored = NotificationVariables.FromJson("{\"TaskTitle\":\"x\"}");
        Assert.True(stored.TryGetValue("TASKTITLE", out var value));
        Assert.Equal("x", value);
    }

    [Fact]
    public async Task The_shell_reads_a_case_sensitive_dictionary_without_regard_to_case()
    {
        var rig = new Rig();
        var template = rig.AddTemplate("en", "<p>x</p>", "x", shell: new NotificationTemplateShell { HeadingTemplate = "Heading {{tasktitle}}" });

        var composed = await rig.Composer.ComposeAsync(
            rig.TenantId, template, "en", "Subject", "<p>x</p>", "x",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["TaskTitle"] = "Batch 24-118" },
            CancellationToken.None);

        Assert.Contains("Heading Batch 24-118", composed.BodyHtml);
    }

    // ---------------------------------------------------------------- doubles

    /// <summary>The mark-sent / mark-failed commands go to their REAL handlers over the rig's dispatch store.</summary>
    private sealed class RoutingMediator(INotificationDispatchRepository dispatches) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object response = request switch
            {
                MarkNotificationDispatchFailedCommand failed =>
                    await new MarkNotificationDispatchFailedHandler(dispatches, new NotificationsSmtpIntegrationTests.RecordingEventBus()).Handle(failed, cancellationToken),
                MarkNotificationDispatchSentCommand sent =>
                    await new MarkNotificationDispatchSentHandler(dispatches, new NotificationsSmtpIntegrationTests.RecordingEventBus()).Handle(sent, cancellationToken),
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

    /// <summary>
    /// MediatR as production composes it for these two commands: ValidationBehavior OUTSIDE ExceptionBehavior around the
    /// real handler (AddApplication's order), the real event-code adapter, the production validator.
    /// </summary>
    private sealed class PipelineMediator(Rig rig) : IMediator
    {
        public const string EventCode = "tenant.lifecycle.suspended";

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            switch (request)
            {
                case QueueEmailNotificationCommand queue:
                    return (TResponse)(object)await Run(queue, [new QueueEmailNotificationValidator()],
                        () => rig.Handler().Handle(queue, cancellationToken), cancellationToken);
                case DispatchNotificationByEventCodeCommand dispatch:
                    var adapter = new NotificationEventDispatchAdapter(
                        new OneActiveEvent(), this, new FakeNotificationLocaleResolver("en"), NullLogger<NotificationEventDispatchAdapter>.Instance);
                    return (TResponse)(object)await Run(dispatch, [],
                        () => new DispatchNotificationByEventCodeHandler(adapter).Handle(dispatch, cancellationToken), cancellationToken);
                default:
                    throw new NotSupportedException(request.GetType().Name);
            }
        }

        private static Task<Response<NotificationDispatchDto>> Run<TRequest>(
            TRequest request, FluentValidation.IValidator<TRequest>[] validators,
            Func<Task<Response<NotificationDispatchDto>>> handler, CancellationToken ct)
            where TRequest : IRequest<Response<NotificationDispatchDto>>
        {
            var exception = new ExceptionBehavior<TRequest, Response<NotificationDispatchDto>>(
                NullLogger<ExceptionBehavior<TRequest, Response<NotificationDispatchDto>>>.Instance);
            var validation = new ValidationBehavior<TRequest, Response<NotificationDispatchDto>>(validators);
            return validation.Handle(request, () => exception.Handle(request, () => handler(), ct), ct);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class OneActiveEvent : INotificationEventDefinitionRepository
    {
        private static readonly NotificationEventDefinition Definition = new()
        {
            EventCode = PipelineMediator.EventCode,
            DefaultTemplateKey = TemplateKey,
            Status = NotificationEventStatus.Active,
            Channel = NotificationChannelCode.Email
        };

        public Task<NotificationEventDefinition> CreateAsync(NotificationEventDefinition definition, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(NotificationEventDefinition definition, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NotificationEventDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<NotificationEventDefinition?>(null);
        public Task<NotificationEventDefinition?> GetByEventCodeAsync(string eventCode, CancellationToken ct = default) =>
            Task.FromResult<NotificationEventDefinition?>(Definition);
        public Task<IReadOnlyList<NotificationEventDefinition>> ListAsync(string? ownerModuleId = null, NotificationChannelCode? channel = null, NotificationEventStatus? status = null, bool? canTenantOverride = null, NotificationEventUsageType? usageType = null, int skip = 0, int take = 100, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationEventDefinition>>([Definition]);
        public Task<IReadOnlyList<NotificationEventDefinition>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationEventDefinition>>([Definition]);
    }

    private sealed class RecordingScheduler : IBackgroundJobScheduler
    {
        public List<object?> Enqueued { get; } = [];

        public Task<string> EnqueueAsync<TArgs, THandler>(TArgs args, BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : IBackgroundJobHandler<TArgs>
        {
            Enqueued.Add(args);
            return Task.FromResult("job");
        }

        public Task<string> ScheduleAsync<TArgs, THandler>(TArgs args, DateTimeOffset enqueueAtUtc, BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : IBackgroundJobHandler<TArgs> => throw new NotSupportedException();

        public Task RegisterRecurringAsync(RecurringJobRegistration registration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SignedIn(Guid userId) : Diten.Platform.Application.Contracts.ICurrentUserContext
    {
        public Guid UserId => userId;
        public string? Email => null;
        public string? DisplayName => null;
        public string ActorName => "operator-test";
        public bool IsAuthenticated => true;
    }

    /// <summary>The consumed-event store's semantics, as TenantLifecycleNotificationConsumerTests double them.</summary>
    private sealed class InMemoryConsumedEvents : IConsumedEventRepository
    {
        private readonly Dictionary<(Guid EventId, string ConsumerName), ConsumedEvent> _items = [];

        public Task<ConsumedEventStartResult> TryStartAsync(ConsumedEvent consumedEvent, CancellationToken cancellationToken = default)
        {
            var key = (consumedEvent.EventId, consumedEvent.ConsumerName);
            if (!_items.TryGetValue(key, out var existing))
            {
                _items[key] = consumedEvent;
                return Task.FromResult(new ConsumedEventStartResult(ConsumedEventStartStatus.Started, consumedEvent));
            }

            if (existing.Status == ConsumedEventStatus.Failed)
            {
                existing.MarkRetryStarted();
                return Task.FromResult(new ConsumedEventStartResult(ConsumedEventStartStatus.Started, existing));
            }

            return Task.FromResult(new ConsumedEventStartResult(ConsumedEventStartStatus.ConsumedDuplicate, existing));
        }

        public Task MarkConsumedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            _items[(eventId, consumerName)].MarkConsumed();
            return Task.CompletedTask;
        }

        public Task MarkSkippedDuplicateAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            _items[(eventId, consumerName)].MarkSkippedDuplicate();
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid eventId, string consumerName, string error, CancellationToken cancellationToken = default)
        {
            _items[(eventId, consumerName)].MarkFailed(error);
            return Task.CompletedTask;
        }

        public Task<ConsumedEvent?> GetAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            _items.TryGetValue((eventId, consumerName), out var value);
            return Task.FromResult(value);
        }
    }
}
