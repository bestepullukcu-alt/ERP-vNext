using System.Text;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services.Notifications;
using Diten.Platform.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Xunit;
using Doubles = Diten.Platform.Application.Tests.Notifications.NotificationsSmtpIntegrationTests;
using SmtpDoubles = Diten.Platform.Application.Tests.Notifications.NotificationsSmtpProviderTests;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 — what actually LEAVES, read off the <see cref="MimeMessage"/> the real <see cref="SmtpMessagingProvider"/>
/// hands to its transport.
///
/// <para>Real here: <see cref="QueueEmailNotificationHandler"/>, <see cref="EmailDispatchJob"/>,
/// <see cref="EmailTemplateRenderer"/>, <see cref="EmailShellComposer"/>, <see cref="TenantEmailIdentityResolver"/>,
/// <see cref="TenantMessagingSettingsResolver"/>, <see cref="SmtpMessagingProvider"/> and MimeKit's message building.
/// Doubled: the SMTP transport (it records the message instead of opening a socket — nothing is sent anywhere), the
/// repositories (in memory), the tenant registry and the secrets provider.</para>
/// </summary>
public sealed class EmailShellDispatchTests
{
    private const string TemplateKey = "platform.tasks.assigned";

    [Fact]
    public async Task A_fragment_body_leaves_inside_the_shell_and_the_stored_preview_stays_the_fragment()
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>Task: {{TaskTitle}}</p>", "Task: {{TaskTitle}}");

        var response = await rig.QueueAsync("en", new() { ["TaskTitle"] = "Batch record review" });

        Assert.True(response.IsSuccessful, string.Join(" | ", response.Errors));
        var message = rig.Sent;
        Assert.StartsWith("<!doctype html>", message.HtmlBody);
        Assert.Contains(">Diten Pharma</td>", message.HtmlBody);
        Assert.Contains("<p>Task: Batch record review</p>", message.HtmlBody);
        Assert.Contains("This e-mail was sent by Di10 on behalf of Diten Pharma.", message.HtmlBody);
        Assert.Contains("Task: Batch record review", message.TextBody);
        Assert.DoesNotContain("<", message.TextBody);

        // What is stored is the author's fragment: the frame is applied when a message leaves, never persisted.
        var dispatch = Assert.Single(rig.Dispatches.Items);
        Assert.Equal("<p>Task: Batch record review</p>", dispatch.BodyHtmlPreview);
    }

    [Fact]
    public async Task A_body_that_is_already_a_whole_document_is_sent_exactly_as_written()
    {
        var rig = new Rig();
        const string document = "<!DOCTYPE html><html><body><h1>Own layout {{TaskTitle}}</h1></body></html>";
        rig.AddTemplate("en", document, "Own layout {{TaskTitle}}");

        await rig.QueueAsync("en", new() { ["TaskTitle"] = "X" });

        Assert.Equal("<!DOCTYPE html><html><body><h1>Own layout X</h1></body></html>", rig.Sent.HtmlBody);
        Assert.Equal("Own layout X", rig.Sent.TextBody);
        // The sender-name rule still applies: it is about who the message is from, not how it looks.
        Assert.Equal("Diten Pharma (via Di10)", rig.Sent.From.Mailboxes.Single().Name);
    }

    [Fact]
    public async Task Without_the_composer_a_message_leaves_as_it_did_before_the_shell_existed()
    {
        var rig = new Rig { WithShell = false };
        rig.AddTemplate("en", "<p>Task: {{TaskTitle}}</p>", "Task: {{TaskTitle}}");

        await rig.QueueAsync("en", new() { ["TaskTitle"] = "X" });

        Assert.Equal("<p>Task: X</p>", rig.Sent.HtmlBody);
        Assert.Equal("Diten PPM", rig.Sent.From.Mailboxes.Single().Name);
    }

    [Fact]
    public async Task A_line_break_in_a_task_title_starts_no_header_and_the_stored_subject_is_the_sent_subject()
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>{{TaskTitle}}</p>", "{{TaskTitle}}", subject: "Assigned: {{TaskTitle}}");

        await rig.QueueAsync("en", new() { ["TaskTitle"] = "Review\r\nBcc: victim@evil.test\r\nSubject: hijacked" });

        const string expected = "Assigned: Review Bcc: victim@evil.test Subject: hijacked";
        Assert.Equal(expected, Assert.Single(rig.Dispatches.Items).Subject);
        Assert.Equal(expected, rig.Sent.Subject);
        AssertNoInjectedHeader(rig.Sent);
    }

    [Fact]
    public async Task The_provider_cleans_a_subject_and_names_that_reach_it_raw()
    {
        // The provider is the last code before the wire: it does not trust that its caller cleaned anything.
        var rig = new Rig();
        var request = new MessagingProviderEmailRequest(
            Guid.NewGuid(), rig.TenantId, "corr",
            "Hello\r\nBcc: victim@evil.test",
            [new EmailRecipientDto("user@example.com", "User\r\nBcc: victim2@evil.test")], [], [],
            "<p>x</p>", "x", "<p>x</p>", "x",
            SenderName: "Acme\r\nBcc: victim3@evil.test");

        var result = await rig.Provider.SendEmailAsync(request);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal("Hello Bcc: victim@evil.test", rig.Sent.Subject);
        Assert.Equal("Acme Bcc: victim3@evil.test", rig.Sent.From.Mailboxes.Single().Name);
        Assert.Equal("User Bcc: victim2@evil.test", rig.Sent.To.Mailboxes.Single().Name);
        AssertNoInjectedHeader(rig.Sent);
    }

    [Fact]
    public async Task A_line_break_in_a_tenant_name_starts_no_header()
    {
        var rig = new Rig();
        rig.Tenants.Tenant = Rig.NewTenant("Acme\r\nBcc: victim@evil.test");
        rig.AddTemplate("en", "<p>x</p>", "x");

        await rig.QueueAsync("en", new());

        Assert.Equal("Acme Bcc: victim@evil.test (via Di10)", rig.Sent.From.Mailboxes.Single().Name);
        AssertNoInjectedHeader(rig.Sent);
    }

    [Fact]
    public async Task A_tenant_that_wrote_its_own_sender_name_sends_under_it()
    {
        var rig = new Rig();
        rig.Settings.CreateAsync(Rig.SettingsRow(rig.TenantId, "Diten Pharma İK", replyTo: null)).GetAwaiter().GetResult();
        rig.AddTemplate("en", "<p>x</p>", "x");

        await rig.QueueAsync("en", new());

        Assert.Equal("Diten Pharma İK", rig.Sent.From.Mailboxes.Single().Name);
    }

    [Fact]
    public async Task A_tenant_without_its_own_sender_name_sends_as_its_display_name_via_the_product()
    {
        var rig = new Rig();
        rig.Settings.CreateAsync(Rig.SettingsRow(rig.TenantId, senderName: null, replyTo: null)).GetAwaiter().GetResult();
        rig.AddTemplate("tr", "<p>x</p>", "x");

        await rig.QueueAsync("tr", new());

        Assert.Equal("Diten Pharma (Di10 üzerinden)", rig.Sent.From.Mailboxes.Single().Name);
    }

    [Fact]
    public async Task The_platform_default_rows_name_is_never_a_tenants_sender_name()
    {
        // No tenant row at all: the message is sent with the platform default row ("Diten PPM" on it, as an existing
        // database still has) — and under the TENANT's name, not that one.
        var rig = new Rig();
        rig.AddTemplate("en", "<p>x</p>", "x");

        await rig.QueueAsync("en", new());

        var from = rig.Sent.From.Mailboxes.Single();
        Assert.Equal("Diten Pharma (via Di10)", from.Name);
        Assert.Equal("bildirim@di10.test", from.Address);
    }

    [Fact]
    public async Task A_platform_email_is_sent_under_the_products_one_name()
    {
        var rig = new Rig { TenantId = Guid.Empty };
        rig.AddTemplate("en", "<p>x</p>", "x");

        await rig.QueueAsync("en", new());

        Assert.Equal("Di10", rig.Sent.From.Mailboxes.Single().Name);
        Assert.Contains("This e-mail was sent by Di10.", rig.Sent.HtmlBody);
    }

    [Fact]
    public async Task The_reply_address_is_a_header_and_a_sentence_only_when_the_tenant_has_one()
    {
        var with = new Rig();
        with.Settings.CreateAsync(Rig.SettingsRow(with.TenantId, senderName: null, replyTo: "ik@ditenpharma.test")).GetAwaiter().GetResult();
        with.AddTemplate("en", "<p>x</p>", "x");
        await with.QueueAsync("en", new());

        Assert.Equal("ik@ditenpharma.test", with.Sent.ReplyTo.Mailboxes.Single().Address);
        Assert.Contains("Your reply goes to ik@ditenpharma.test.", with.Sent.HtmlBody);
        Assert.Contains("Your reply goes to ik@ditenpharma.test.", with.Sent.TextBody);

        var without = new Rig();
        without.AddTemplate("en", "<p>x</p>", "x");
        await without.QueueAsync("en", new());

        Assert.Empty(without.Sent.ReplyTo);
        Assert.DoesNotContain("Your reply goes to", without.Sent.HtmlBody);
    }

    [Fact]
    public async Task A_language_nobody_wrote_a_template_in_falls_back_to_the_tenants_language()
    {
        var rig = new Rig { TenantLocale = "tr" };
        rig.AddTemplate("tr", "<p>Görev</p>", "Görev");
        rig.AddTemplate("en", "<p>Task</p>", "Task");

        var response = await rig.QueueAsync("fr", new());

        Assert.True(response.IsSuccessful, string.Join(" | ", response.Errors));
        Assert.Equal("tr", Assert.Single(rig.Dispatches.Items).Locale);
        Assert.Contains("<p>Görev</p>", rig.Sent.HtmlBody);
        // The frame speaks the language of the template that was used, not the one that was asked for.
        Assert.Contains("Bu e-posta Diten Pharma adına Di10 üzerinden gönderildi.", rig.Sent.HtmlBody);
        Assert.Equal("Diten Pharma (Di10 üzerinden)", rig.Sent.From.Mailboxes.Single().Name);
    }

    [Fact]
    public async Task With_no_template_in_the_tenants_language_either_the_email_goes_out_in_English_and_the_dispatch_says_so()
    {
        // The tenant lifecycle e-mails exist in en/tr only. A tenant reading French used to get nothing at all.
        var rig = new Rig { TenantLocale = "fr" };
        rig.AddTemplate("en", "<p>Task</p>", "Task");

        var response = await rig.QueueAsync("fr", new());

        Assert.True(response.IsSuccessful, string.Join(" | ", response.Errors));
        var dispatch = Assert.Single(rig.Dispatches.Items);
        Assert.Equal("en", dispatch.Locale);
        Assert.Equal(NotificationDispatchStatus.Sent, dispatch.Status);
        Assert.Contains("<p>Task</p>", rig.Sent.HtmlBody);
    }

    [Fact]
    public async Task With_no_template_in_any_language_the_refusal_is_what_it_was()
    {
        var rig = new Rig();

        var response = await rig.QueueAsync("fr", new());

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(QueueEmailNotificationHandler.ReasonTemplateNotFound, response.ReasonCode);
        Assert.Empty(rig.Dispatches.Items);
    }

    [Fact]
    public async Task A_templates_heading_table_and_action_are_filled_from_its_variables()
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>The task below is yours.</p>", "The task below is yours.", shell: TaskShell());

        await rig.QueueAsync("en", new()
        {
            ["TaskTitle"] = "Batch <24-118> review",
            ["Priority"] = "High",
            ["AssignerName"] = "",
            ["TaskUrl"] = "https://di10.example/WorkCenterNext/Details/42"
        });

        var html = rig.Sent.HtmlBody;
        Assert.Contains("A task was assigned to you</h1>", html);
        Assert.Contains(">Task</td>", html);
        Assert.Contains("Batch &lt;24-118&gt; review</td>", html);
        Assert.Contains(">High</td>", html);
        // A row whose value is empty is left out rather than drawn blank.
        Assert.DoesNotContain(">Assigned by</td>", html);
        Assert.Contains("<a href=\"https://di10.example/WorkCenterNext/Details/42\"", html);
        Assert.Contains("Open the task", html);
        Assert.Contains("You received this notification because the task was assigned to you.", html);
        Assert.Contains("Open the task:\nhttps://di10.example/WorkCenterNext/Details/42", rig.Sent.TextBody.Replace("\r\n", "\n"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("")]
    public async Task An_action_address_that_is_not_http_draws_no_button(string url)
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>x</p>", "x", shell: TaskShell());

        await rig.QueueAsync("en", new() { ["TaskTitle"] = "T", ["TaskUrl"] = url });

        Assert.DoesNotContain("<a ", rig.Sent.HtmlBody);
        Assert.DoesNotContain("Open the task", rig.Sent.HtmlBody);
    }

    [Fact]
    public async Task A_retry_that_only_has_the_stored_preview_sends_it_inside_the_shell()
    {
        var rig = new Rig();
        var dispatch = rig.AddFailedDispatch(variablesJson: "{\"TaskTitle\":\"[REDACTED]\"}", templateId: null);

        await rig.Job().HandleAsync(new EmailDispatchJobArgs(rig.TenantId, dispatch.Id), new BackgroundJobContext());

        var html = rig.Sent.HtmlBody;
        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("<p>stored preview</p>", html);
        Assert.Contains("Stored subject</h1>", html);
        Assert.Contains(">Diten Pharma</td>", html);
        Assert.Equal("Diten Pharma (via Di10)", rig.Sent.From.Mailboxes.Single().Name);
    }

    [Fact]
    public async Task A_retry_that_can_reproduce_the_body_frames_it_with_the_templates_own_parts()
    {
        var rig = new Rig();
        var template = rig.AddTemplate("en", "<p>The task below is yours.</p>", "The task below is yours.", shell: TaskShell());
        var dispatch = rig.AddFailedDispatch(
            variablesJson: "{\"TaskTitle\":\"Review\",\"TaskUrl\":\"https://di10.example/t/1\"}",
            templateId: template.Id);

        await rig.Job().HandleAsync(new EmailDispatchJobArgs(rig.TenantId, dispatch.Id), new BackgroundJobContext());

        var html = rig.Sent.HtmlBody;
        Assert.Contains("A task was assigned to you</h1>", html);
        Assert.Contains("<p>The task below is yours.</p>", html);
        Assert.Contains("<a href=\"https://di10.example/t/1\"", html);
        Assert.DoesNotContain("stored preview", html);
    }

    [Fact]
    public async Task A_retry_cleans_a_subject_stored_before_subjects_were_cleaned()
    {
        var rig = new Rig();
        var dispatch = rig.AddFailedDispatch("{}", templateId: null, subject: "Old\r\nBcc: victim@evil.test");

        await rig.Job().HandleAsync(new EmailDispatchJobArgs(rig.TenantId, dispatch.Id), new BackgroundJobContext());

        Assert.Equal("Old Bcc: victim@evil.test", rig.Sent.Subject);
        AssertNoInjectedHeader(rig.Sent);
    }

    [Fact]
    public async Task The_preview_keeps_every_field_it_had_and_adds_the_framed_form()
    {
        var rig = new Rig();
        var request = new RenderTemplatePreviewRequest(
            "Assigned: {{TaskTitle}}",
            "<p>Task: {{TaskTitle}}</p>",
            "Task: {{TaskTitle}}",
            [new TemplateVariableDefinitionDto("TaskTitle", "String", true)],
            new Dictionary<string, object?> { ["TaskTitle"] = "Review" });

        var before = await new RenderNotificationTemplatePreviewHandler(new EmailTemplateRenderer())
            .Handle(new RenderNotificationTemplatePreviewQuery(request), CancellationToken.None);
        var after = await new RenderNotificationTemplatePreviewHandler(new EmailTemplateRenderer(), rig.Composer)
            .Handle(new RenderNotificationTemplatePreviewQuery(request), CancellationToken.None);

        Assert.True(after.IsSuccessful);
        // Every field an older client reads is byte for byte what it was.
        Assert.Equal(before.Data!.Subject, after.Data!.Subject);
        Assert.Equal(before.Data.BodyHtml, after.Data.BodyHtml);
        Assert.Equal(before.Data.BodyText, after.Data.BodyText);
        Assert.Equal(before.Data.BodyHtmlPreview, after.Data.BodyHtmlPreview);
        Assert.Equal(before.Data.BodyTextPreview, after.Data.BodyTextPreview);
        Assert.Equal("<p>Task: Review</p>", after.Data.BodyHtml);
        Assert.Null(before.Data.BodyHtmlFramed);

        Assert.StartsWith("<!doctype html>", after.Data.BodyHtmlFramed);
        Assert.Contains("<p>Task: Review</p>", after.Data.BodyHtmlFramed);
        Assert.Contains("Assigned: Review</h1>", after.Data.BodyHtmlFramed);
        Assert.Contains("Task: Review", after.Data.BodyTextFramed);
    }

    [Fact]
    public async Task A_preview_that_fails_still_fails_the_same_way()
    {
        var rig = new Rig();
        var request = new RenderTemplatePreviewRequest(
            "S", "<p>{{TaskTitle}}</p>", null,
            [new TemplateVariableDefinitionDto("TaskTitle", "String", true)],
            new Dictionary<string, object?>());

        var response = await new RenderNotificationTemplatePreviewHandler(new EmailTemplateRenderer(), rig.Composer)
            .Handle(new RenderNotificationTemplatePreviewQuery(request), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    // ── BL-454 fix round 1 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_retry_of_a_task_mail_with_a_spaced_title_sends_the_very_same_body_as_the_first_attempt()
    {
        // The tenant reads English, this mail was asked for in Turkish: the retry must stay Turkish, not re-resolve.
        var rig = new Rig { TenantLocale = "en" };
        rig.AddTemplate("tr", "<p>Aşağıdaki görev sizde.</p>", "Aşağıdaki görev sizde.", subject: "Size bir görev atandı: {{TaskTitle}}", shell: TaskShell());
        var variables = new Dictionary<string, object?>
        {
            ["TaskTitle"] = "Parti kaydı incelemesi, LOT 24-118",
            ["Priority"] = "Yüksek",
            ["AssignerName"] = "Burak Şen",
            ["TaskUrl"] = "https://di10.example/WorkCenterNext/Details/42"
        };

        // The first attempt reaches the transport and is refused there.
        rig.Transport.SendThrow = new InvalidOperationException("Mailbox unavailable");
        var queued = await rig.QueueAsync("tr", variables);
        Assert.False(queued.IsSuccessful);
        var first = rig.Sent;
        var firstHtml = first.HtmlBody;
        var firstText = first.TextBody;
        Assert.Contains("Open the task", firstHtml);
        Assert.Contains(">Burak Şen</td>", firstHtml);

        var dispatch = Assert.Single(rig.Dispatches.Items);
        Assert.Equal(NotificationDispatchStatus.Failed, dispatch.Status);
        Assert.DoesNotContain("[REDACTED]", dispatch.VariablesJson);

        rig.Transport.SendThrow = null;
        await rig.Job().HandleAsync(new EmailDispatchJobArgs(rig.TenantId, dispatch.Id), new BackgroundJobContext());

        Assert.Equal(firstHtml, rig.Sent.HtmlBody);
        Assert.Equal(firstText, rig.Sent.TextBody);
        Assert.Equal(first.Subject, rig.Sent.Subject);
        // The retry speaks the language of the first attempt (the dispatch's own locale, not a fresh lookup).
        Assert.Contains("Bu e-posta Diten Pharma adına Di10 üzerinden gönderildi.", rig.Sent.HtmlBody);
    }

    [Fact]
    public async Task A_secret_in_a_tenant_admin_invitation_is_never_stored_and_never_sent_by_a_retry()
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>Your password: {{TemporaryPassword}}</p><p>{{LoginUrl}}</p>", "Your password: {{TemporaryPassword}}");
        const string secret = "Tmp Pass 9!x";

        rig.Transport.SendThrow = new InvalidOperationException("Mailbox unavailable");
        await rig.QueueAsync("en", new() { ["TemporaryPassword"] = secret, ["LoginUrl"] = "https://di10.example/login" });
        var dispatch = Assert.Single(rig.Dispatches.Items);

        Assert.DoesNotContain(secret, dispatch.VariablesJson);
        Assert.DoesNotContain(secret, dispatch.BodyHtmlPreview);
        Assert.Contains("[REDACTED]", dispatch.VariablesJson);
        // A name that is not sensitive is stored as given — the retry needs it.
        Assert.Contains("https://di10.example/login", dispatch.VariablesJson);

        rig.Transport.SendThrow = null;
        await rig.Job().HandleAsync(new EmailDispatchJobArgs(rig.TenantId, dispatch.Id), new BackgroundJobContext());

        Assert.DoesNotContain(secret, rig.Sent.HtmlBody);
        Assert.DoesNotContain(secret, rig.Sent.TextBody ?? string.Empty);
    }

    [Fact]
    public void The_sensitive_variable_names_are_one_pinned_list()
    {
        Assert.Equal(["secret", "token", "password", "apikey", "api_key"], NotificationParsing.SensitiveVariableNameParts);
        Assert.True(NotificationParsing.IsSensitiveVariableName("TemporaryPassword"));
        Assert.True(NotificationParsing.IsSensitiveVariableName("ResetToken"));
        Assert.True(NotificationParsing.IsSensitiveVariableName("ApiKey"));
        Assert.False(NotificationParsing.IsSensitiveVariableName("TaskTitle"));
        Assert.False(NotificationParsing.IsSensitiveVariableName("LoginUrl"));
    }

    [Theory]
    [InlineData("LoginUrl", "https://di10.example/set?token=abc", true)]
    [InlineData("SetupLink", "https://di10.example/reset?email=a%40b.test&Code=123", true)]
    [InlineData("Download", "https://files.example/f.pdf?X-Amz-Signature=deadbeef", true)]
    [InlineData("TaskUrl", "https://di10.example/WorkCenterNext/Details/123", false)]
    [InlineData("ListUrl", "https://di10.example/a?page=2", false)]
    [InlineData("TaskTitle", "Parti kaydı incelemesi, LOT 24-118", false)]
    [InlineData("Formula", "a = b + c", false)]
    [InlineData("TemporaryPassword", "anything", true)]
    public void Only_a_secret_name_or_a_credential_bearing_link_is_masked(string name, string value, bool masked)
    {
        Assert.Equal(masked, NotificationParsing.IsSensitiveVariable(name, value));
    }

    [Fact]
    public async Task A_set_password_link_under_an_ordinary_name_is_never_stored_and_a_task_link_is_kept()
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>{{LoginUrl}} {{TaskUrl}}</p>", "{{LoginUrl}}");
        const string link = "https://di10.example/account/set-password?email=a%40b.test&token=s3cr3t";

        // The first attempt fails, so the row is what a retry would work from (a sent row keeps no variables).
        rig.Transport.SendThrow = new InvalidOperationException("Mailbox unavailable");
        await rig.QueueAsync("en", new() { ["LoginUrl"] = link, ["TaskUrl"] = "https://di10.example/WorkCenterNext/Details/123" });

        var dispatch = Assert.Single(rig.Dispatches.Items);
        Assert.DoesNotContain("s3cr3t", dispatch.VariablesJson);
        Assert.DoesNotContain("s3cr3t", dispatch.BodyHtmlPreview);
        Assert.Contains("https://di10.example/WorkCenterNext/Details/123", dispatch.VariablesJson);
        // The first send still carries the link: masking is for what is kept, not for what is sent.
        Assert.Contains("s3cr3t", rig.Sent.HtmlBody);
    }

    [Fact]
    public void A_dispatch_keeps_its_variables_only_while_a_retry_can_still_follow()
    {
        NotificationDispatch Waiting() => new() { Status = NotificationDispatchStatus.Failed, VariablesJson = "{\"TaskTitle\":\"Review\"}" };

        var sent = Waiting();
        Assert.True(sent.TryMarkSent("id", DateTimeOffset.UtcNow));
        Assert.Equal("{}", sent.VariablesJson);

        var permanent = Waiting();
        Assert.True(permanent.TryMarkFailed("X", "y", DateTimeOffset.UtcNow, isPermanent: true));
        Assert.Equal("{}", permanent.VariablesJson);

        var retryPending = Waiting();
        Assert.True(retryPending.TryMarkFailed("X", "y", DateTimeOffset.UtcNow));
        Assert.Equal("{\"TaskTitle\":\"Review\"}", retryPending.VariablesJson);

        var cancelled = new NotificationDispatch { Status = NotificationDispatchStatus.Queued, VariablesJson = "{\"TaskTitle\":\"Review\"}" };
        Assert.True(cancelled.TryCancel(DateTimeOffset.UtcNow));
        Assert.Equal("{}", cancelled.VariablesJson);
    }

    [Fact]
    public async Task The_retry_sweeps_permanent_failure_releases_the_variables_in_the_same_transition()
    {
        var rig = new Rig();
        var dispatch = rig.AddFailedDispatch("{\"TaskTitle\":\"Review\"}", templateId: null);
        var handler = new MarkNotificationDispatchFailedHandler(rig.Dispatches, new Doubles.RecordingEventBus());

        await handler.Handle(new MarkNotificationDispatchFailedCommand(rig.TenantId, dispatch.Id, "X", "y", RetryCount: 5, IsPermanentFailure: true), CancellationToken.None);
        Assert.Equal("{}", dispatch.VariablesJson);

        var waiting = rig.AddFailedDispatch("{\"TaskTitle\":\"Review\"}", templateId: null);
        await handler.Handle(new MarkNotificationDispatchFailedCommand(rig.TenantId, waiting.Id, "X", "y", RetryCount: 2, NextRetryAt: DateTimeOffset.UtcNow.AddMinutes(5)), CancellationToken.None);
        Assert.Equal("{\"TaskTitle\":\"Review\"}", waiting.VariablesJson);
    }

    [Fact]
    public void The_monitoring_answer_shows_variable_names_and_never_their_values()
    {
        var dispatch = new NotificationDispatch
        {
            TenantId = Guid.NewGuid(), TemplateKey = "platform.tasks.assigned", Subject = "S",
            VariablesJson = "{\"TaskTitle\":\"Parti kaydı incelemesi\",\"AssignerName\":\"Burak Şen\"}"
        };

        var dto = dispatch.ToDto();

        Assert.DoesNotContain("Parti", dto.VariablesJson);
        Assert.DoesNotContain("Burak", dto.VariablesJson);
        Assert.Contains("\"TaskTitle\"", dto.VariablesJson);
        Assert.Contains("\"AssignerName\"", dto.VariablesJson);
    }

    [Fact]
    public async Task A_tenant_sender_name_that_tries_to_write_a_second_mailbox_leaves_exactly_one_From_mailbox()
    {
        var rig = new Rig();
        rig.Settings.CreateAsync(Rig.SettingsRow(rig.TenantId, "Acme\" <evil@attacker.test>, \"", replyTo: null)).GetAwaiter().GetResult();
        rig.AddTemplate("en", "<p>x</p>", "x");

        await rig.QueueAsync("en", new());

        var header = WireHeader(rig.Sent, "From");
        var parsed = InternetAddressList.Parse(header);
        var mailbox = Assert.Single(parsed.Mailboxes);
        Assert.Equal("bildirim@di10.test", mailbox.Address);
        Assert.DoesNotContain("attacker", mailbox.Address);

        // OUR rule, not the library's: MimeKit quotes and escapes a display name itself, so the wire alone would stay
        // single even if the name kept its quote. The name handed to the library must already be safe — the same
        // name System.Net.Mail (AuthService) would put in quotes without escaping.
        var name = rig.Sent.From.Mailboxes.Single().Name;
        Assert.DoesNotContain('"', name);
        Assert.DoesNotContain('<', name);
        Assert.Equal("Acme evil@attacker.test,", name);
    }

    [Fact]
    public async Task A_direction_override_in_a_task_title_never_reaches_the_subject_on_the_wire()
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>x</p>", "x", subject: "Assigned: {{TaskTitle}}");

        await rig.QueueAsync("en", new() { ["TaskTitle"] = "Invoice " + (char)0x202E + "fdp.exe" + (char)0x200B });

        Assert.Equal("Assigned: Invoice fdp.exe", rig.Sent.Subject);
        Assert.DoesNotContain((char)0x202E, WireHeader(rig.Sent, "Subject"));
    }

    [Fact]
    public async Task A_template_with_no_text_body_sends_a_text_part_derived_from_its_html()
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>Open <a href=\"https://di10.example/t/1\">the task</a>.</p>", null!);

        await rig.QueueAsync("en", new());

        Assert.Contains("Open the task (https://di10.example/t/1).", rig.Sent.TextBody);
    }

    [Fact]
    public async Task A_template_whose_body_says_nothing_sends_no_empty_text_part()
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p> </p>", null!);

        await rig.QueueAsync("en", new());

        Assert.Null(rig.Sent.TextBody);
    }

    [Fact]
    public async Task A_reply_address_that_is_not_one_address_is_left_out_and_the_footer_does_not_promise_it()
    {
        var rig = new Rig();
        rig.Settings.CreateAsync(Rig.SettingsRow(rig.TenantId, null, replyTo: "ik@ditenpharma.test\r\nBcc: victim@evil.test")).GetAwaiter().GetResult();
        rig.AddTemplate("en", "<p>x</p>", "x");

        await rig.QueueAsync("en", new());

        Assert.Empty(rig.Sent.ReplyTo);
        Assert.DoesNotContain("Your reply goes to", rig.Sent.HtmlBody);
        Assert.DoesNotContain("victim", WireHeaders(rig.Sent));
    }

    [Theory]
    [InlineData("user@example.com\r\nBcc: victim@evil.test")]
    [InlineData("user@example.com, victim@evil.test")]
    [InlineData("not-an-address")]
    public async Task A_recipient_that_is_not_one_address_is_refused_with_a_code_and_nothing_is_queued_for_retry(string recipient)
    {
        var rig = new Rig();
        rig.AddTemplate("en", "<p>x</p>", "x");

        var response = await rig.QueueAsync("en", new(), recipient);

        Assert.False(response.IsSuccessful);
        Assert.Equal(QueueEmailNotificationHandler.ReasonRecipientInvalid, response.ReasonCode);
        Assert.Empty(rig.Dispatches.Items);
        Assert.Null(rig.Transport.LastSentMessage);
    }

    [Fact]
    public async Task The_preview_of_a_saved_template_draws_its_own_heading_table_and_button()
    {
        var rig = new Rig();
        var saved = rig.AddTemplate("en", "<p>x</p>", "x", shell: TaskShell());
        var request = new RenderTemplatePreviewRequest(
            "Assigned: {{TaskTitle}}", "<p>{{TaskTitle}}</p>", null,
            [new TemplateVariableDefinitionDto("TaskTitle", "String", true)],
            new Dictionary<string, object?> { ["TaskTitle"] = "Review", ["TaskUrl"] = "https://di10.example/t/1" },
            saved.Id);

        var response = await new RenderNotificationTemplatePreviewHandler(new EmailTemplateRenderer(), rig.Composer, rig.Templates)
            .Handle(new RenderNotificationTemplatePreviewQuery(request), CancellationToken.None);

        Assert.Contains("A task was assigned to you</h1>", response.Data!.BodyHtmlFramed);
        Assert.Contains(">Review</td>", response.Data.BodyHtmlFramed);
        Assert.Contains("<a href=\"https://di10.example/t/1\"", response.Data.BodyHtmlFramed);
    }

    [Fact]
    public async Task A_tenant_whose_own_settings_row_is_switched_off_falls_back_to_the_platform_default_for_name_and_reply()
    {
        var rig = new Rig();
        var own = Rig.SettingsRow(rig.TenantId, "Diten Pharma İK", replyTo: "ik@ditenpharma.test");
        own.IsEnabled = false;
        rig.Settings.CreateAsync(own).GetAwaiter().GetResult();

        var identity = await new TenantEmailIdentityResolver(rig.Tenants, rig.Settings, new FakeNotificationLocaleResolver("en"))
            .ResolveAsync(rig.TenantId);

        Assert.Null(identity!.SenderName);
        Assert.Null(identity.ReplyToEmail);
        Assert.Equal("Diten Pharma", identity.DisplayName);
    }

    private static string WireHeaders(MimeMessage message)
    {
        using var stream = new MemoryStream();
        message.WriteTo(stream);
        var wire = Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
        return wire[..wire.IndexOf("\n\n", StringComparison.Ordinal)];
    }

    /// <summary>One header as written on the wire, folded lines joined.</summary>
    private static string WireHeader(MimeMessage message, string name)
    {
        var lines = WireHeaders(message).Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase));
        Assert.True(start >= 0, $"No {name} header.");
        var value = new StringBuilder(lines[start][(name.Length + 1)..]);
        for (var i = start + 1; i < lines.Length && lines[i].Length > 0 && char.IsWhiteSpace(lines[i][0]); i++)
        {
            value.Append(lines[i]);
        }

        return value.ToString().Trim();
    }

    private static NotificationTemplateShell TaskShell() => new()
    {
        HeadingTemplate = "A task was assigned to you",
        InfoRows =
        [
            new NotificationTemplateShellRow { Label = "Task", ValueTemplate = "{{TaskTitle}}" },
            new NotificationTemplateShellRow { Label = "Priority", ValueTemplate = "{{Priority}}" },
            new NotificationTemplateShellRow { Label = "Assigned by", ValueTemplate = "{{AssignerName}}" }
        ],
        ActionLabel = "Open the task",
        ActionUrlVariable = "TaskUrl",
        FootnoteTemplate = "You received this notification because the task was assigned to you."
    };

    /// <summary>
    /// The header block exactly as it goes on the wire. One Subject, one From, no Bcc: a value that smuggled a line
    /// break through would show up here as a header line of its own.
    /// </summary>
    private static void AssertNoInjectedHeader(MimeMessage message)
    {
        using var stream = new MemoryStream();
        message.WriteTo(stream);
        var wire = Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
        var headers = wire[..wire.IndexOf("\n\n", StringComparison.Ordinal)].Split('\n');

        Assert.DoesNotContain(headers, line => line.StartsWith("Bcc:", StringComparison.OrdinalIgnoreCase));
        Assert.Single(headers, line => line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase));
        Assert.Single(headers, line => line.StartsWith("From:", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(message.Bcc);
        Assert.DoesNotContain('\n', message.Subject);
        Assert.DoesNotContain('\r', message.Subject);
    }

    private sealed class Rig
    {
        public Guid TenantId { get; init; } = Guid.NewGuid();
        public string TenantLocale { get; init; } = "en";
        public bool WithShell { get; init; } = true;

        public Doubles.InMemoryTenantMessagingSettingsRepository Settings { get; } = new();
        public Doubles.InMemoryNotificationTemplateRepository Templates { get; } = new();
        public Doubles.InMemoryNotificationDispatchRepository Dispatches { get; } = new();
        public SmtpDoubles.FakeSmtpTransport Transport { get; } = new();
        public FakeTenants Tenants { get; } = new() { Tenant = NewTenant("Diten Pharma") };

        public Rig()
        {
            // What an existing database holds: the platform default row, still named by the old Smtp:FromName.
            Settings.CreateAsync(new TenantMessagingSettings
            {
                TenantId = null,
                IsPlatformDefault = true,
                ProviderCode = MessagingProviderCode.Smtp,
                SenderEmail = "bildirim@di10.test",
                SenderName = "Diten PPM",
                Host = "smtp.example.test",
                Port = 587,
                CredentialSecretRef = "secret:platform:smtp:default",
                IsEnabled = true
            }).GetAwaiter().GetResult();
        }

        public MimeMessage Sent => Transport.LastSentMessage ?? throw new InvalidOperationException("Nothing reached the transport.");

        public IEmailShellComposer Composer => new EmailShellComposer(
            new TenantEmailIdentityResolver(Tenants, Settings, new FakeNotificationLocaleResolver(TenantLocale)),
            NullLogger<EmailShellComposer>.Instance);

        public SmtpMessagingProvider Provider => new(
            new SmtpDoubles.StaticOptionsMonitor<SmtpProviderOptions>(new SmtpProviderOptions()),
            Settings,
            new SmtpDoubles.RecordingSmtpClientFactory(Transport),
            new SecretReferenceResolver(new SmtpDoubles.InMemorySecretsProvider("not-a-real-secret")),
            new SmtpDoubles.TestHostEnvironment("Development"),
            NullLogger<SmtpMessagingProvider>.Instance);

        public Task<Diten.Platform.Application.Common.Response<NotificationDispatchDto>> QueueAsync(
            string locale, Dictionary<string, object?> variables, string recipient = "user@example.com")
        {
            var handler = new QueueEmailNotificationHandler(
                new TenantMessagingSettingsResolver(Settings),
                Templates,
                new EmailTemplateRenderer(),
                Dispatches,
                new Doubles.TestProviderResolver(Provider),
                new Doubles.RecordingEventBus(),
                NullLogger<QueueEmailNotificationHandler>.Instance,
                WithShell ? Composer : null,
                WithShell ? new FakeNotificationLocaleResolver(TenantLocale) : null);

            return handler.Handle(
                new QueueEmailNotificationCommand(
                    TenantId,
                    new QueueEmailNotificationRequest(TemplateKey, locale, variables, [new EmailRecipientDto(recipient, "User")]),
                    "corr-shell"),
                CancellationToken.None);
        }

        public EmailDispatchJob Job() => new(
            Dispatches,
            new TenantMessagingSettingsResolver(Settings),
            new Doubles.TestProviderResolver(Provider),
            new SilentMediator(),
            NullLogger<EmailDispatchJob>.Instance,
            Templates,
            new EmailTemplateRenderer(),
            Composer);

        public NotificationTemplate AddTemplate(
            string locale, string html, string text, string subject = "Subject", NotificationTemplateShell? shell = null)
        {
            var template = new NotificationTemplate
            {
                IsPlatformDefault = true,
                TemplateKey = TemplateKey,
                Channel = NotificationChannelCode.Email,
                Locale = locale,
                SubjectTemplate = subject,
                BodyHtmlTemplate = html,
                BodyTextTemplate = text,
                Status = NotificationTemplateStatus.Active,
                SemanticVersion = "1.0.0",
                Shell = shell
            };
            Templates.CreateAsync(template).GetAwaiter().GetResult();
            return template;
        }

        public NotificationDispatch AddFailedDispatch(string variablesJson, Guid? templateId, string subject = "Stored subject")
        {
            var dispatch = new NotificationDispatch
            {
                TenantId = TenantId,
                TemplateKey = TemplateKey,
                TemplateId = templateId,
                TemplateSemanticVersion = "1.0.0",
                Locale = "en",
                Channel = NotificationChannelCode.Email,
                ProviderCode = MessagingProviderCode.Smtp,
                Status = NotificationDispatchStatus.Failed,
                To = [new EmailRecipient { Email = "user@example.com" }],
                Subject = subject,
                BodyHtmlPreview = "<p>stored preview</p>",
                BodyTextPreview = "stored preview",
                VariablesJson = variablesJson,
                QueuedAt = DateTimeOffset.UtcNow,
                RetryCount = 1,
                NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            };
            Dispatches.Items.Add(dispatch);
            return dispatch;
        }

        public static Tenant NewTenant(string displayName) => new()
        {
            Code = "DITEN",
            Slug = "diten",
            Name = "diten-pharma",
            DisplayName = displayName,
            Domain = "diten.test"
        };

        public static TenantMessagingSettings SettingsRow(Guid tenantId, string? senderName, string? replyTo) => new()
        {
            TenantId = tenantId,
            IsPlatformDefault = false,
            ProviderCode = MessagingProviderCode.Smtp,
            SenderEmail = "bildirim@di10.test",
            SenderName = senderName,
            ReplyToEmail = replyTo,
            Host = "smtp.example.test",
            Port = 587,
            CredentialSecretRef = "secret:platform:smtp:default",
            IsEnabled = true
        };
    }

    internal sealed class FakeTenants : ITenantRegistryRepository
    {
        public Tenant? Tenant { get; set; }

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Tenant);
        public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Tenant>, long)>(([], 0));
        public Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    /// <summary>The job marks the dispatch through MediatR; what it marks is not this file's subject.</summary>
    private sealed class SilentMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult(default(TResponse)!);
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
