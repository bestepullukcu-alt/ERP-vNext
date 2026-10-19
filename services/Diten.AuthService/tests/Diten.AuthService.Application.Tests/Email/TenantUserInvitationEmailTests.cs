using System.Net.Mail;
using System.Text;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Users;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Services.EmailTemplates;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Application.Tests.Email;

/// <summary>
/// BL-454 — the tenant-user invitation as it leaves AuthService: read off the <see cref="MailMessage"/> the real
/// <see cref="TenantUserInvitationEmailService"/> builds, and — for the header-injection case — off the .eml file the
/// real <see cref="SmtpClient"/> writes to a pickup directory. No SMTP server is contacted and nothing is sent.
/// </summary>
public sealed class TenantUserInvitationEmailTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly string[] Seven = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private const string Recipient = "ayse@ditenpharma.test";

    [Fact]
    public async Task The_invitation_leaves_under_the_tenants_name_in_the_tenants_language_with_its_reply_address()
    {
        var service = Service(new TenantEmailIdentity("Diten Pharma", "tr", null, "ik@ditenpharma.test"));

        using var message = await service.BuildMessageAsync(Recipient, "token-1", CancellationToken.None);

        Assert.Equal("Diten Pharma (Di10 üzerinden)", message.From!.DisplayName);
        Assert.Equal("no-reply@di10.test", message.From.Address);
        Assert.Equal("Diten Pharma hesabınız hazır", message.Subject);
        Assert.Equal("ik@ditenpharma.test", Assert.Single(message.ReplyToList).Address);
        Assert.Equal(Recipient, Assert.Single(message.To).Address);

        var html = Html(message);
        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("<html lang=\"tr\" dir=\"ltr\"", html);
        Assert.Contains(">Diten Pharma</td>", html);
        Assert.Contains("Hesabınız hazır, parolanızı belirleyin</h1>", html);
        Assert.Contains("Merhaba Ayşe, Diten Pharma yöneticiniz sizin için bir hesap açtı.", html);
        Assert.Contains("Parolamı belirle", html);
        Assert.Contains("https://app.di10.test/account/set-password?email=ayse%40ditenpharma.test&amp;token=token-1", html);
        Assert.Contains("Yanıtınız ik@ditenpharma.test adresine ulaşır.", html);
    }

    [Fact]
    public async Task The_invitation_has_a_plain_text_part_that_carries_the_link()
    {
        var service = Service(new TenantEmailIdentity("Diten Pharma", "tr", null, null));

        using var message = await service.BuildMessageAsync(Recipient, "token-1", CancellationToken.None);

        Assert.False(message.IsBodyHtml);
        Assert.DoesNotContain("<", message.Body);
        Assert.Contains("Merhaba Ayşe,", message.Body);
        Assert.Contains("Parolamı belirle:\nhttps://app.di10.test/account/set-password?email=ayse%40ditenpharma.test&token=token-1", message.Body);
        Assert.Contains("Bağlantı 7 gün geçerlidir ve yalnız bir kez kullanılabilir.", message.Body);
        Assert.Equal("text/html", Assert.Single(message.AlternateViews).ContentType.MediaType);
    }

    [Fact]
    public async Task A_sender_name_the_tenant_wrote_itself_is_used_as_written()
    {
        var service = Service(new TenantEmailIdentity("Diten Pharma", "tr", "Diten Pharma İK", null));

        using var message = await service.BuildMessageAsync(Recipient, "t", CancellationToken.None);

        Assert.Equal("Diten Pharma İK", message.From!.DisplayName);
    }

    [Fact]
    public async Task When_Platform_cannot_answer_the_invitation_still_leaves_under_the_products_name_in_English()
    {
        var service = Service((TenantEmailIdentity?)null);

        using var message = await service.BuildMessageAsync(Recipient, "t", CancellationToken.None);

        Assert.Equal("Di10", message.From!.DisplayName);
        Assert.Equal("Your account is ready", message.Subject);
        Assert.Empty(message.ReplyToList);
        var html = Html(message);
        Assert.Contains("Hello Ayşe, Your administrator has created an account for you.", html);
        Assert.Contains("This e-mail was sent by Di10.", html);
        Assert.Contains("Set my password", html);
    }

    [Fact]
    public async Task Without_a_name_on_record_nobody_is_greeted()
    {
        var service = Service(new TenantEmailIdentity("Diten Pharma", "tr", null, null), users: []);

        using var message = await service.BuildMessageAsync(Recipient, "t", CancellationToken.None);

        Assert.DoesNotContain("Merhaba", message.Body);
        Assert.Contains("Diten Pharma yöneticiniz sizin için bir hesap açtı.", message.Body);
    }

    [Fact]
    public async Task A_platform_administrator_inviting_into_a_tenant_sends_as_that_tenant()
    {
        var identity = new RecordingIdentity(new TenantEmailIdentity("Diten Pharma", "en", null, null));
        var service = Service(identity, context: new Context { IsPlatformContext = true, TargetTenantId = Tenant, TenantId = Guid.Empty });

        using var message = await service.BuildMessageAsync(Recipient, "t", CancellationToken.None);

        Assert.Equal([Tenant], identity.Asked);
        Assert.Equal("Diten Pharma (via Di10)", message.From!.DisplayName);
    }

    [Fact]
    public async Task With_no_tenant_in_the_request_Platform_is_not_asked_at_all()
    {
        var identity = new RecordingIdentity(new TenantEmailIdentity("Diten Pharma", "en", null, null));
        var service = Service(identity, context: new Context { IsResolved = false, TenantId = Guid.Empty });

        using var message = await service.BuildMessageAsync(Recipient, "t", CancellationToken.None);

        Assert.Empty(identity.Asked);
        Assert.Equal("Di10", message.From!.DisplayName);
    }

    [Fact]
    public async Task Markup_in_a_tenant_name_or_a_first_name_arrives_as_text()
    {
        var users = new[] { new User(Recipient, "hash", "<b>Ayşe</b>", "Kaya", Tenant) };
        var service = Service(new TenantEmailIdentity("Acme <script>alert(1)</script>", "en", null, null), users);

        using var message = await service.BuildMessageAsync(Recipient, "t", CancellationToken.None);

        var html = Html(message);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>Ayşe</b>", html);
        Assert.Contains("&lt;b&gt;Ayşe&lt;/b&gt;", html);
    }

    [Fact]
    public async Task A_line_break_in_a_tenant_name_or_a_first_name_starts_no_header_on_the_wire()
    {
        var users = new[] { new User(Recipient, "hash", "Ayşe\r\nBcc: victim2@evil.test", "Kaya", Tenant) };
        var service = Service(
            new TenantEmailIdentity("Acme\r\nBcc: victim@evil.test\r\nSubject: hijacked", "en", null, "ik@ditenpharma.test"), users);

        using var message = await service.BuildMessageAsync(Recipient, "t", CancellationToken.None);

        Assert.Equal("Acme Bcc: victim@evil.test Subject: hijacked (via Di10)", message.From!.DisplayName);
        Assert.Equal("Your Acme Bcc: victim@evil.test Subject: hijacked account is ready", message.Subject);
        Assert.Empty(message.Bcc);

        // The header block exactly as System.Net.Mail writes it — the library's own serialisation, not our model of it.
        var headers = WireHeaders(message);
        Assert.DoesNotContain(headers, line => line.StartsWith("Bcc:", StringComparison.OrdinalIgnoreCase));
        Assert.Single(headers, line => line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase));
        Assert.Single(headers, line => line.StartsWith("From:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_invitation_exists_in_each_of_the_seven_languages_and_none_is_a_copy_of_English()
    {
        var sentences = typeof(TenantUserInvitationEmailTexts).GetProperties().Where(p => p.PropertyType == typeof(string)).ToList();
        Assert.Equal(9, sentences.Count);
        var english = TenantUserInvitationEmailTemplate.TextsFor("en");

        foreach (var language in Seven)
        {
            var texts = TenantUserInvitationEmailTemplate.TextsFor(language);
            foreach (var sentence in sentences)
            {
                var value = (string?)sentence.GetValue(texts);
                Assert.False(string.IsNullOrWhiteSpace(value), $"{sentence.Name} is empty in '{language}'.");
                if (language != "en")
                {
                    Assert.NotEqual((string?)sentence.GetValue(english), value);
                }
            }

            Assert.Contains("{0}", texts.SubjectForTenant);
            Assert.Contains("{0}", texts.Greeting);
            Assert.Contains("{0}", texts.BodyForTenant);
            Assert.DoesNotContain("{0}", texts.Body);
            Assert.Contains("7", texts.ActionNote);
        }
    }

    [Fact]
    public void An_Arabic_invitation_is_right_to_left()
    {
        var html = TenantUserInvitationEmailTemplate.Render("ar", "Diten Pharma", "Ayşe", "https://app.di10.test/x", null).Html;

        Assert.Contains("<html lang=\"ar\" dir=\"rtl\"", html);
        Assert.Contains("تعيين كلمة المرور", html);
    }

    private static TenantUserInvitationEmailService Service(
        TenantEmailIdentity? identity, IEnumerable<User>? users = null, Context? context = null)
        => Service(new RecordingIdentity(identity), users, context);

    private static TenantUserInvitationEmailService Service(
        ITenantEmailIdentityClient identity, IEnumerable<User>? users = null, Context? context = null) => new(
        Options.Create(new SmtpOptions { Host = "smtp.invalid", Port = 25, FromEmail = "no-reply@di10.test" }),
        Options.Create(new PlatformServiceOptions { FrontendBaseUrl = "https://app.di10.test/" }),
        context ?? new Context { TenantId = Tenant },
        new InMemoryUserRepository(users ?? [new User(Recipient, "hash", "Ayşe", "Kaya", Tenant)]),
        identity,
        NullLogger<TenantUserInvitationEmailService>.Instance);

    private static string Html(MailMessage message)
    {
        using var reader = new StreamReader(Assert.Single(message.AlternateViews).ContentStream, Encoding.UTF8, leaveOpen: true);
        message.AlternateViews[0].ContentStream.Position = 0;
        return reader.ReadToEnd();
    }

    private static string[] WireHeaders(MailMessage message)
    {
        var directory = Directory.CreateTempSubdirectory("di10-invitation-eml-");
        try
        {
            using (var client = new SmtpClient
                   {
                       DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                       PickupDirectoryLocation = directory.FullName
                   })
            {
                client.Send(message);
            }

            var wire = File.ReadAllText(Assert.Single(directory.GetFiles("*.eml")).FullName).Replace("\r\n", "\n");
            return wire[..wire.IndexOf("\n\n", StringComparison.Ordinal)].Split('\n');
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private sealed class RecordingIdentity(TenantEmailIdentity? answer) : ITenantEmailIdentityClient
    {
        public List<Guid> Asked { get; } = [];

        public Task<TenantEmailIdentity?> GetAsync(Guid tenantId, CancellationToken ct)
        {
            Asked.Add(tenantId);
            return Task.FromResult(answer);
        }
    }

    private sealed class Context : ITenantContext
    {
        public Guid TenantId { get; set; }
        public bool IsResolved { get; set; } = true;
        public bool IsPlatformContext { get; set; }
        public Guid? TargetTenantId { get; set; }
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
        public void SetPlatformContext(Guid targetTenantId) => TargetTenantId = targetTenantId;
    }
}
