using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services.Notifications;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// MOD-0357 S5b — <c>MessagingProviderEmailRequest.Attachments</c> reaches the real <see cref="MimeKit.MimeMessage"/>
/// <see cref="SmtpMessagingProvider.BuildMessage"/> builds, AND the field is proven additive: every existing
/// <c>NotificationsSmtpProviderBodyAndAuthTests</c> scenario omits it and stays green unmodified (see this
/// suite's own "Notifications" filter run in the WP's report) — this file only adds NEW coverage, it never
/// edits an existing assertion.
/// </summary>
public sealed class NotificationsAttachmentTests
{
    [Fact]
    public async Task An_attachment_on_the_request_reaches_the_sent_MimeMessage()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new NotificationsSmtpProviderTests.FakeSmtpTransport
        {
            SendBehavior = (_, _) => Task.FromResult("OK 250 ok")
        };
        var factory = new NotificationsSmtpProviderTests.RecordingSmtpClientFactory(transport);
        var provider = CreateProvider(settings, factory);

        var icsBytes = System.Text.Encoding.UTF8.GetBytes("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");
        var request = new MessagingProviderEmailRequest(
            DispatchId: Guid.NewGuid(),
            TenantId: tenantId,
            CorrelationId: "corr-attachment",
            Subject: "Subject",
            To: [new EmailRecipientDto("user@example.com", "User")],
            Cc: [],
            Bcc: [],
            BodyHtmlPreview: "<p>hi</p>",
            BodyTextPreview: "hi",
            Attachments: [new MessagingProviderAttachment("invite.ics", "text/calendar; charset=utf-8; method=REQUEST", icsBytes)]);

        var result = await provider.SendEmailAsync(request);

        Assert.True(result.Accepted);
        Assert.NotNull(transport.LastSentMessage);
        var attachment = Assert.Single(transport.LastSentMessage!.Attachments);
        var part = Assert.IsAssignableFrom<MimeKit.MimePart>(attachment);
        Assert.Equal("invite.ics", part.FileName);
        Assert.Equal("calendar", part.ContentType.MediaSubtype);
        Assert.Equal("text", part.ContentType.MediaType);
        Assert.Equal("REQUEST", part.ContentType.Parameters["method"]);

        using var stream = new MemoryStream();
        part.Content.DecodeTo(stream);
        Assert.Equal(icsBytes, stream.ToArray());
    }

    [Fact]
    public async Task No_attachments_field_produces_the_SAME_message_shape_as_before_this_field_existed()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new NotificationsSmtpProviderTests.FakeSmtpTransport
        {
            SendBehavior = (_, _) => Task.FromResult("OK 250 ok")
        };
        var factory = new NotificationsSmtpProviderTests.RecordingSmtpClientFactory(transport);
        var provider = CreateProvider(settings, factory);

        // Attachments omitted entirely — the exact shape every pre-existing caller/test still uses.
        var request = new MessagingProviderEmailRequest(
            DispatchId: Guid.NewGuid(),
            TenantId: tenantId,
            CorrelationId: "corr-none",
            Subject: "Subject",
            To: [new EmailRecipientDto("user@example.com", "User")],
            Cc: [],
            Bcc: [],
            BodyHtmlPreview: "<p>hi</p>",
            BodyTextPreview: "hi");

        var result = await provider.SendEmailAsync(request);

        Assert.True(result.Accepted);
        Assert.NotNull(transport.LastSentMessage);
        Assert.Empty(transport.LastSentMessage!.Attachments);
    }

    [Fact]
    public async Task An_empty_attachments_list_also_produces_no_attachment()
    {
        var tenantId = Guid.NewGuid();
        var settings = CreateRepository(CreateSettings(tenantId));
        var transport = new NotificationsSmtpProviderTests.FakeSmtpTransport
        {
            SendBehavior = (_, _) => Task.FromResult("OK 250 ok")
        };
        var factory = new NotificationsSmtpProviderTests.RecordingSmtpClientFactory(transport);
        var provider = CreateProvider(settings, factory);

        var request = new MessagingProviderEmailRequest(
            DispatchId: Guid.NewGuid(),
            TenantId: tenantId,
            CorrelationId: "corr-empty",
            Subject: "Subject",
            To: [new EmailRecipientDto("user@example.com", "User")],
            Cc: [],
            Bcc: [],
            BodyHtmlPreview: "<p>hi</p>",
            BodyTextPreview: "hi",
            Attachments: []);

        var result = await provider.SendEmailAsync(request);

        Assert.True(result.Accepted);
        Assert.Empty(transport.LastSentMessage!.Attachments);
    }

    private static SmtpMessagingProvider CreateProvider(
        ITenantMessagingSettingsRepository repository, ISmtpClientFactory factory) =>
        new(
            new NotificationsSmtpProviderTests.StaticOptionsMonitor<SmtpProviderOptions>(new SmtpProviderOptions()),
            repository,
            factory,
            new SecretReferenceResolver(new NotificationsSmtpProviderTests.InMemorySecretsProvider("resolved-password")),
            new NotificationsSmtpProviderTests.TestHostEnvironment("Development"),
            NullLogger<SmtpMessagingProvider>.Instance);

    private static NotificationsSmtpProviderTests.InMemoryTenantMessagingSettingsRepository CreateRepository(params TenantMessagingSettings[] items)
    {
        var repo = new NotificationsSmtpProviderTests.InMemoryTenantMessagingSettingsRepository();
        foreach (var item in items)
        {
            repo.CreateAsync(item).GetAwaiter().GetResult();
        }
        return repo;
    }

    private static TenantMessagingSettings CreateSettings(Guid tenantId, string senderEmail = "sender@example.com") => new()
    {
        TenantId = tenantId,
        IsPlatformDefault = false,
        ProviderCode = MessagingProviderCode.Smtp,
        SenderEmail = senderEmail,
        SenderName = "Sender",
        Host = "smtp.example.test",
        Port = 587,
        UseSsl = true,
        CredentialSecretRef = "secret:platform:smtp:default",
        IsEnabled = true,
        FallbackPolicy = NotificationFallbackPolicy.UsePlatformDefault
    };
}
