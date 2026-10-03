using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Users;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Services.EmailTemplates;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Application.Tests.Email;

/// <summary>
/// BL-454 fix round 1 — the AuthService side: an administrator's password reset says so (not "you are invited"), a
/// sender name cannot write a second mailbox, a bad reply or recipient address never reaches a header, and the
/// internal API key never follows a redirect.
/// </summary>
public sealed class TenantUserEmailFixRoundOneTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly string[] Seven = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private const string Recipient = "ayse@ditenpharma.test";

    [Fact]
    public async Task An_administrators_password_reset_of_an_account_in_use_has_its_own_words()
    {
        // What AdminResetPasswordCommandHandler leaves behind: a confirmed account that must change its password.
        var user = new User(Recipient, "hash", "Ayşe", "Kaya", Tenant);
        user.ConfirmEmail();
        user.RequirePasswordChange(null);
        var service = Service(new TenantEmailIdentity("Diten Pharma", "tr", null, null), [user]);

        using var message = await service.BuildMessageAsync(Recipient, "token-1", CancellationToken.None);

        Assert.Equal("Diten Pharma parolanızı yenileyin", message.Subject);
        var html = Html(message);
        Assert.Contains("Yeni parolanızı belirleyin</h1>", html);
        Assert.Contains("parolasını sıfırladı", html);
        Assert.Contains("Yeni parola belirle", html);
        Assert.DoesNotContain("davet", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hesap açtı", html);
        // Same link, same token: the flow did not move.
        Assert.Contains("https://app.di10.test/account/set-password?email=ayse%40ditenpharma.test&amp;token=token-1", html);
        Assert.Equal("Diten Pharma (Di10 üzerinden)", message.From!.DisplayName);
    }

    [Fact]
    public async Task A_reset_of_an_account_that_has_signed_in_but_never_confirmed_is_still_a_reset()
    {
        var user = new User(Recipient, "hash", "Ayşe", "Kaya", Tenant);
        user.RecordLoginSuccess();
        var service = Service(new TenantEmailIdentity("Diten Pharma", "en", null, null), [user]);

        using var message = await service.BuildMessageAsync(Recipient, "t", CancellationToken.None);

        Assert.Equal("Reset your Diten Pharma password", message.Subject);
    }

    [Fact]
    public async Task A_pending_invitation_still_reads_as_an_invitation()
    {
        var user = new User(Recipient, "hash", "Ayşe", "Kaya", Tenant);
        user.RequirePasswordChange(null);
        var service = Service(new TenantEmailIdentity("Diten Pharma", "tr", null, null), [user]);

        using var message = await service.BuildMessageAsync(Recipient, "t", CancellationToken.None);

        Assert.Equal("Diten Pharma hesabınız hazır", message.Subject);
        Assert.Contains("hesap açtı", Html(message));
    }

    [Fact]
    public void The_reset_words_exist_in_seven_languages_and_none_is_a_copy_of_English_or_of_the_invitation()
    {
        var sentences = typeof(TenantUserInvitationEmailTexts).GetProperties().Where(p => p.PropertyType == typeof(string)).ToList();
        var english = TenantUserInvitationEmailTemplate.ResetTextsFor("en");

        foreach (var language in Seven)
        {
            var reset = TenantUserInvitationEmailTemplate.ResetTextsFor(language);
            var invitation = TenantUserInvitationEmailTemplate.TextsFor(language);
            Assert.False(string.IsNullOrWhiteSpace(reset.Heading));
            Assert.NotEqual(invitation.Heading, reset.Heading);
            Assert.NotEqual(invitation.Subject, reset.Subject);
            Assert.NotEqual(invitation.BodyForTenant, reset.BodyForTenant);
            Assert.Contains("{0}", reset.SubjectForTenant);
            Assert.Contains("{0}", reset.BodyForTenant);
            foreach (var sentence in sentences)
            {
                Assert.False(string.IsNullOrWhiteSpace((string?)sentence.GetValue(reset)), $"{sentence.Name} empty in '{language}'.");
                if (language != "en")
                {
                    Assert.NotEqual((string?)sentence.GetValue(english), (string?)sentence.GetValue(reset));
                }
            }
        }

        Assert.Contains("parola", TenantUserInvitationEmailTemplate.ResetTextsFor("tr").Subject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("şifre", string.Join(" ", sentences.Select(p => (string?)p.GetValue(TenantUserInvitationEmailTemplate.ResetTextsFor("tr")))), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_tenant_sender_name_that_tries_to_write_a_second_mailbox_leaves_exactly_one_From_mailbox_on_the_wire()
    {
        var service = Service(new TenantEmailIdentity("Diten Pharma", "en", "Acme\" <evil@attacker.test>, \"", null));

        using var message = await service.BuildMessageAsync(Recipient, "t", CancellationToken.None);

        var from = WireHeader(message, "From");
        var mailboxes = new MailAddressCollection();
        mailboxes.Add(from);
        var mailbox = Assert.Single(mailboxes);
        Assert.Equal("no-reply@di10.test", mailbox.Address);
    }

    [Fact]
    public async Task A_reply_address_that_is_not_one_address_is_left_out()
    {
        var service = Service(new TenantEmailIdentity("Diten Pharma", "en", null, "ik@ditenpharma.test, other@evil.test"));

        using var message = await service.BuildMessageAsync(Recipient, "t", CancellationToken.None);

        Assert.Empty(message.ReplyToList);
        Assert.DoesNotContain("Your reply goes to", Html(message));
        Assert.DoesNotContain("other@evil.test", string.Join("\n", WireHeaders(message)));
    }

    [Theory]
    [InlineData("ayse@ditenpharma.test\r\nBcc: victim@evil.test")]
    [InlineData("ayse@ditenpharma.test, victim@evil.test")]
    [InlineData("not-an-address")]
    public async Task A_recipient_that_is_not_one_address_is_refused_with_a_code_before_anything_is_built(string recipient)
    {
        var service = Service(new TenantEmailIdentity("Diten Pharma", "en", null, null));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildMessageAsync(recipient, "t", CancellationToken.None));

        Assert.Equal("RECIPIENT_INVALID", refusal.Message);
    }

    [Fact]
    public async Task The_internal_key_never_follows_a_redirect_to_another_host()
    {
        using var target = new LoopbackServer(_ => (200, "{\"data\":{\"displayName\":\"Leaked\",\"language\":\"en\"}}", null));
        using var platform = new LoopbackServer(_ => (302, string.Empty, target.Url + "/stolen"));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(new PlatformServiceOptions { BaseUrl = platform.Url, InternalApiKey = "test-only-internal-key" }));
        PlatformTenantEmailIdentityClient.Register(services);
        await using var provider = services.BuildServiceProvider();

        var identity = await provider.GetRequiredService<ITenantEmailIdentityClient>().GetAsync(Tenant, CancellationToken.None);

        Assert.Null(identity);
        Assert.Single(platform.Requests);
        Assert.Empty(target.Requests);
    }

    private static TenantUserInvitationEmailService Service(TenantEmailIdentity? identity, IEnumerable<User>? users = null) => new(
        Options.Create(new SmtpOptions { Host = "smtp.invalid", Port = 25, FromEmail = "no-reply@di10.test" }),
        Options.Create(new PlatformServiceOptions { FrontendBaseUrl = "https://app.di10.test/" }),
        new Context { TenantId = Tenant },
        new InMemoryUserRepository(users ?? [new User(Recipient, "hash", "Ayşe", "Kaya", Tenant)]),
        new FixedIdentity(identity),
        NullLogger<TenantUserInvitationEmailService>.Instance);

    private static string Html(MailMessage message)
    {
        var view = Assert.Single(message.AlternateViews);
        view.ContentStream.Position = 0;
        using var reader = new StreamReader(view.ContentStream, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static string[] WireHeaders(MailMessage message)
    {
        var directory = Directory.CreateTempSubdirectory("di10-fix1-eml-");
        try
        {
            using (var client = new SmtpClient { DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory, PickupDirectoryLocation = directory.FullName })
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

    private static string WireHeader(MailMessage message, string name)
    {
        var lines = WireHeaders(message);
        var start = Array.FindIndex(lines, line => line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase));
        Assert.True(start >= 0, $"No {name} header.");
        var value = new StringBuilder(lines[start][(name.Length + 1)..]);
        for (var i = start + 1; i < lines.Length && lines[i].Length > 0 && char.IsWhiteSpace(lines[i][0]); i++)
        {
            value.Append(lines[i]);
        }

        return value.ToString().Trim();
    }

    private sealed class FixedIdentity(TenantEmailIdentity? answer) : ITenantEmailIdentityClient
    {
        public Task<TenantEmailIdentity?> GetAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(answer);
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

    /// <summary>A loopback HTTP endpoint that records what reached it. It proves where a request went, not who it trusts.</summary>
    private sealed class LoopbackServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Func<HttpListenerRequest, (int Status, string Body, string? Location)> _answer;

        public LoopbackServer(Func<HttpListenerRequest, (int Status, string Body, string? Location)> answer)
        {
            _answer = answer;
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            Url = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add(Url + "/");
            _listener.Start();
            _ = Task.Run(LoopAsync);
        }

        public string Url { get; }
        public List<(string Path, string? Key)> Requests { get; } = [];

        private async Task LoopAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch
                {
                    return;
                }

                lock (Requests)
                {
                    Requests.Add((context.Request.Url!.AbsolutePath, context.Request.Headers["X-Internal-Api-Key"]));
                }

                var (status, body, location) = _answer(context.Request);
                context.Response.StatusCode = status;
                if (location is not null)
                {
                    context.Response.RedirectLocation = location;
                }

                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        public void Dispose()
        {
            _listener.Stop();
            _listener.Close();
        }
    }
}
