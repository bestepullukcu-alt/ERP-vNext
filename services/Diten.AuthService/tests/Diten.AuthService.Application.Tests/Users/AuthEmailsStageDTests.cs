using System.Net.Mail;
using System.Reflection;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Services.EmailTemplates;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-454 — WP-EMAIL-SHELL-01 slice 2 stage D: the AuthService e-mails that were still outside the shell. The platform
/// administrator's set-password link (two languages — the platform console's) and the sign-in verification code (seven —
/// it is sent by the TENANT login). Each measured on the production service class, short of the SMTP send.
/// </summary>
public sealed class AuthEmailsStageDTests
{
    private const string Token = "stage-d-test-only-token";

    // ---------------------------------------------------------------- platform administrator's set-password link

    [Theory]
    [InlineData(null, "en", 1)]
    [InlineData("en-GB,en;q=0.9", "en", 1)]
    [InlineData("tr-TR,tr;q=0.9,en;q=0.5", "tr", 24)]
    [InlineData("de-DE", "en", 24)]
    public async Task The_platform_set_password_mail_is_in_the_shell_in_the_console_language_and_states_the_stored_expiry(
        string? acceptLanguage, string expectedLanguage, int hours)
    {
        var expiresAt = new DateTime(2026, 10, 7, 9, 30, 0, DateTimeKind.Utc).AddHours(hours);
        var service = PlatformEmails(acceptLanguage, StoredLink(Token, expiresAt));

        using var message = await service.BuildMessageAsync("padmin@platform.test", Token, CancellationToken.None);

        var texts = PlatformPasswordResetEmailTemplate.TextsFor(expectedLanguage);
        var html = Html(message);
        Assert.Equal(texts.Subject, message.Subject);
        Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase); // the shell's frame
        Assert.Contains(texts.Heading, message.Body);
        Assert.Contains(expiresAt.ToString("yyyy-MM-dd HH:mm"), message.Body); // the stored time, 1 h or 24 h
        Assert.Contains("/platform/reset-password?email=padmin%40platform.test&token=" + Token, message.Body); // the address, written out
        Assert.Contains("/platform/reset-password?email=padmin%40platform.test&amp;token=" + Token, html);   // and the button
        Assert.DoesNotContain("one hour", message.Body, StringComparison.OrdinalIgnoreCase); // the old, wrong-for-24h words
        Assert.DoesNotContain(Token, message.Subject);
    }

    [Fact]
    public async Task A_platform_link_whose_stored_hash_is_another_tokens_states_no_time_rather_than_a_wrong_one()
    {
        var service = PlatformEmails(null, StoredLink("another-token", DateTime.UtcNow.AddHours(24)));

        using var message = await service.BuildMessageAsync("padmin@platform.test", Token, CancellationToken.None);

        Assert.Contains(PlatformPasswordResetEmailTemplate.TextsFor("en").ActionNote, message.Body);
        Assert.DoesNotContain("valid until", message.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Both_platform_languages_have_every_text()
    {
        Assert.Equal(["en", "tr"], PlatformPasswordResetEmailTemplate.Languages);
        foreach (var language in PlatformPasswordResetEmailTemplate.Languages)
        {
            var texts = PlatformPasswordResetEmailTemplate.TextsFor(language);
            Assert.All(new[] { texts.Subject, texts.Heading, texts.Body, texts.Action, texts.ActionNote, texts.Footnote },
                t => Assert.False(string.IsNullOrWhiteSpace(t)));
            Assert.Contains("{0}", texts.ActionNoteUntil);
        }

        Assert.NotEqual(PlatformPasswordResetEmailTemplate.TextsFor("en").Body, PlatformPasswordResetEmailTemplate.TextsFor("tr").Body);
    }

    // ---------------------------------------------------------------- the sign-in verification code

    public static TheoryData<string> SevenLanguages() => new() { "en", "tr", "fr", "es", "zh", "ar", "ru" };

    [Theory]
    [MemberData(nameof(SevenLanguages))]
    public async Task The_verification_code_mail_is_in_the_shell_in_the_tenants_language_with_the_code_in_the_body_only(string language)
    {
        const string code = "482915";
        var expiresAt = new DateTime(2026, 10, 7, 9, 35, 0, DateTimeKind.Utc);
        var service = new SmtpOtpDeliveryService(
            Options.Create(new SmtpOptions { Host = "smtp.test", FromEmail = "noreply@di10.test" }),
            Proxy<ITenantEmailIdentityClient>.Answering((method, _) =>
                Task.FromResult<TenantEmailIdentity?>(new TenantEmailIdentity("Diten Pharma", language, null, null))));

        using var message = await service.BuildMessageAsync(Guid.NewGuid(), "user@tenant.test", code, expiresAt, CancellationToken.None);

        var texts = VerificationCodeEmailTemplate.TextsFor(language);
        Assert.Equal(texts.Subject, message.Subject);
        Assert.DoesNotContain(code, message.Subject); // never on a lock screen
        Assert.Contains(code, message.Body);
        Assert.Contains(texts.CodeLabel, message.Body);
        Assert.Contains("2026-10-07 09:35", message.Body);
        Assert.Contains("<html", Html(message), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Diten Pharma", message.From!.DisplayName);
        if (language != "en")
        {
            Assert.NotEqual(VerificationCodeEmailTemplate.TextsFor("en").Subject, message.Subject);
        }
    }

    [Fact]
    public void Every_verification_code_language_has_every_text()
    {
        Assert.Equal(["en", "tr", "fr", "es", "zh", "ar", "ru"], VerificationCodeEmailTemplate.Languages);
        Assert.Equal(7, VerificationCodeEmailTemplate.Languages.Select(l => VerificationCodeEmailTemplate.TextsFor(l).Heading).Distinct().Count());
    }

    [Fact]
    public async Task The_code_mail_names_the_challenges_tenant_not_the_one_a_request_header_names()
    {
        // BL-454 stage D FIX1 K1 — the resend's request may carry ANOTHER tenant's header: the code still goes out under the
        // tenant the challenge (and the user) belongs to.
        var (service, delivered, _, challenge) = Mfa(expiresAtUtc: DateTime.UtcNow.AddMinutes(4), requestTenant: Guid.NewGuid());
        var recorder = Recorder!;

        await service.ResendEmailChallengeAsync("challenge-1", "127.0.0.1", null, CancellationToken.None);

        Assert.Single(delivered);
        Assert.Equal(challenge.TenantId, Assert.Single(recorder.Tenants));

        var asked = new List<Guid>();
        var mail = new SmtpOtpDeliveryService(
            Options.Create(new SmtpOptions { Host = "smtp.test", FromEmail = "noreply@di10.test" }),
            Proxy<ITenantEmailIdentityClient>.Answering((method, args) =>
            {
                asked.Add((Guid)args[0]!);
                return Task.FromResult<TenantEmailIdentity?>(new TenantEmailIdentity("Right Tenant", "en", null, null));
            }));
        using var message = await mail.BuildMessageAsync(challenge.TenantId, "user@tenant.test", "123456", DateTime.UtcNow.AddMinutes(4), CancellationToken.None);
        Assert.Equal([challenge.TenantId], asked);
        Assert.Contains("Right Tenant", message.Body);
    }

    [Fact]
    public async Task A_new_challenge_belongs_to_the_users_tenant_not_to_the_one_a_request_header_names()
    {
        // FIX2 K9.
        var (service, delivered, _, _) = Mfa(expiresAtUtc: DateTime.UtcNow.AddMinutes(4), requestTenant: Guid.NewGuid());
        var recorder = Recorder!;
        var userTenant = Guid.NewGuid();
        var user = new User("other@tenant.test", "hash", "Other", "User", userTenant);

        await service.CreateEmailChallengeAsync(user, "127.0.0.1", null, CancellationToken.None);

        Assert.Single(delivered);
        Assert.Equal(userTenant, Assert.Single(recorder.Tenants));
        Assert.Equal(userTenant, Assert.Single(Created).TenantId);
    }

    // ---------------------------------------------------------------- an expired code is never sent again

    [Fact]
    public async Task A_resend_of_an_expired_challenge_sends_nothing_and_no_code_reaches_the_audit()
    {
        var (service, delivered, audit, _) = Mfa(expiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ResendEmailChallengeAsync("challenge-1", "127.0.0.1", null, CancellationToken.None));

        Assert.Empty(delivered);
        Assert.All(audit, line => Assert.DoesNotContain("code", line.Metadata.Replace("\"channel\"", string.Empty), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_resend_of_a_live_challenge_sends_a_NEW_code_stored_only_as_a_hash()
    {
        var (service, delivered, audit, challenge) = Mfa(expiresAtUtc: DateTime.UtcNow.AddMinutes(4));
        var previousHash = challenge.CodeHash;

        await service.ResendEmailChallengeAsync("challenge-1", "127.0.0.1", null, CancellationToken.None);

        var (_, code, expiresAt) = Assert.Single(delivered);
        Assert.NotEqual(previousHash, challenge.CodeHash);         // a new code, not the old one again
        Assert.DoesNotContain(code, challenge.CodeHash);
        Assert.True(expiresAt > DateTime.UtcNow);
        Assert.All(audit, line => Assert.DoesNotContain(code, line.Metadata, StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- helpers

    private static PlatformAuthEmailService PlatformEmails(string? acceptLanguage, User storedAccount)
    {
        var context = new DefaultHttpContext();
        if (acceptLanguage is not null)
        {
            context.Request.Headers.AcceptLanguage = acceptLanguage;
        }

        return new PlatformAuthEmailService(
            Options.Create(new SmtpOptions { Host = "smtp.test", FromEmail = "noreply@di10.test" }),
            Options.Create(new PlatformServiceOptions { FrontendBaseUrl = "https://console.di10.test" }),
            Proxy<IUserRepository>.Answering((method, _) => method == nameof(IUserRepository.GetByEmailAndTenantAsync)
                ? Task.FromResult<User?>(storedAccount)
                : throw new NotSupportedException(method)),
            new PrefixHasher(),
            new HttpContextAccessor { HttpContext = context });
    }

    private static User StoredLink(string token, DateTime expiresAtUtc)
    {
        var user = new User("padmin@platform.test", "hash", "Platform", "Admin", Guid.Parse("00000000-0000-0000-0000-000000000001"));
        user.SetPasswordResetToken(new PrefixHasher().Hash(token), expiresAtUtc);
        return user;
    }

    private static string Html(MailMessage message)
    {
        var view = Assert.Single(message.AlternateViews);
        using var reader = new StreamReader(view.ContentStream);
        return reader.ReadToEnd();
    }

    private sealed record AuditLine(string EventName, string Metadata);

    private static (MfaChallengeService Service, List<(string Email, string Code, DateTime ExpiresAt)> Delivered, List<AuditLine> Audit, MfaChallenge Challenge)
        Mfa(DateTime expiresAtUtc, Guid? requestTenant = null)
    {
        CreatedList = [];
        var options = Options.Create(new MfaOptions { HashSecret = "stage-d-test-only-hmac-key", ExpiryMinutes = 5 });
        var tenantId = Guid.NewGuid();
        var user = new User("user@tenant.test", "hash", "User", "One", tenantId);
        var delivered = new List<(string, string, DateTime)>();
        var audit = new List<AuditLine>();
        MfaChallenge? challenge = null;

        var service = new MfaChallengeService(
            Proxy<IMfaChallengeRepository>.Answering((method, args) =>
            {
                if (method == nameof(IMfaChallengeRepository.CreateAsync))
                {
                    Created.Add((MfaChallenge)args[0]!);
                }

                return method == nameof(IMfaChallengeRepository.GetByChallengeIdHashAsync) ? Task.FromResult(challenge) : Task.CompletedTask;
            }),
            Recorder = new DeliveryRecorder(delivered),
            Proxy<IUserRepository>.Answering((method, _) => method == nameof(IUserRepository.GetByIdAndTenantAsync)
                ? Task.FromResult<User?>(user)
                : throw new NotSupportedException(method)),
            Proxy<IAuthAuditService>.Answering((method, args) =>
            {
                if (method == nameof(IAuthAuditService.WriteAsync))
                {
                    audit.Add(new AuditLine((string)args[0]!, (string)args[3]!));
                }

                return Task.CompletedTask;
            }),
            new ResolvedTenant(requestTenant ?? tenantId), // the request's tenant (a header) — may differ from the challenge's
            options);

        // The challenge as CreateEmailChallengeAsync stored it: hashes only, keyed like the service's own.
        var hash = typeof(MfaChallengeService).GetMethod("ComputeHash", BindingFlags.NonPublic | BindingFlags.Instance)!;
        string Keyed(string value) => (string)hash.Invoke(service, [value])!;
        challenge = new MfaChallenge(tenantId, user.Id, Keyed("challenge-1"), "email", Keyed(user.Email), Keyed("111111"),
            expiresAtUtc, 5, "127.0.0.1", null);
        return (service, delivered, audit, challenge);
    }

    [ThreadStatic] private static DeliveryRecorder? Recorder;
    [ThreadStatic] private static List<MfaChallenge>? CreatedList;
    private static List<MfaChallenge> Created => CreatedList ??= [];

    private sealed class DeliveryRecorder(List<(string, string, DateTime)> delivered) : IOtpDeliveryService
    {
        public List<Guid> Tenants { get; } = [];

        public Task SendEmailOtpAsync(Guid tenantId, string email, string code, DateTime expiresAtUtc, CancellationToken ct)
        {
            Tenants.Add(tenantId);
            delivered.Add((email, code, expiresAtUtc));
            return Task.CompletedTask;
        }
    }

    private sealed class PrefixHasher : IRefreshTokenHasher
    {
        public string Hash(string refreshToken) => "h:" + refreshToken.Length + ":" + refreshToken.GetHashCode(StringComparison.Ordinal);
    }

    private sealed class ResolvedTenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId => tenantId;
        public bool IsResolved => true;
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid id) => throw new NotSupportedException();
        public void SetPlatformContext(Guid targetTenantId) => throw new NotSupportedException();
    }

    /// <summary>Any interface, every member answered by one function.</summary>
    public class Proxy<T> : DispatchProxy where T : class
    {
        private Func<string, object?[], object?> _answer = (_, _) => null;

        public static T Answering(Func<string, object?[], object?> answer)
        {
            var proxy = Create<T, Proxy<T>>();
            ((Proxy<T>)(object)proxy)._answer = answer;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _answer(targetMethod!.Name, args ?? []);
    }
}
