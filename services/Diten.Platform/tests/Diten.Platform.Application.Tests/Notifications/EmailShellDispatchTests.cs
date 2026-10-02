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
            string locale, Dictionary<string, object?> variables)
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
                    new QueueEmailNotificationRequest(TemplateKey, locale, variables, [new EmailRecipientDto("user@example.com", "User")]),
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
