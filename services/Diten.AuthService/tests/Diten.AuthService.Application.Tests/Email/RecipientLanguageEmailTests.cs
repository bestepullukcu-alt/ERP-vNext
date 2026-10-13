using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Users;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Services.EmailTemplates;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.AuthService.Application.Tests.Email;

/// <summary>
/// WP-EMAIL-LANG-01 — AuthService's own mails to tenant users (the invitation / reset link, the sign-in code) are written
/// in the recipient's language: their own choice, else the tenant's, else en.
/// </summary>
public sealed class RecipientLanguageEmailTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private const string Recipient = "jean@ditenpharma.test";

    // ---------------------------------------------------------------- the field and the rule

    [Theory]
    [InlineData("fr", "fr")]
    [InlineData(" FR ", "fr")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void A_preference_is_stored_in_its_plain_form_and_blank_clears_it(string? given, string? stored)
    {
        var user = new User(Recipient, "hash", "Jean", "Dupont", Tenant);
        user.SetPreferredLanguage("ru");

        user.SetPreferredLanguage(given);

        Assert.Equal(stored, user.PreferredLanguage);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("fr-FR")]
    [InlineData("english")]
    public void A_language_outside_the_seven_is_never_stored(string language)
    {
        var user = new User(Recipient, "hash", "Jean", "Dupont", Tenant);

        Assert.Throws<ArgumentException>(() => user.SetPreferredLanguage(language));
        Assert.Null(user.PreferredLanguage);
    }

    [Theory]
    [InlineData("fr", "tr", "fr")]   // the person's own choice first
    [InlineData(null, "tr", "tr")]   // else the tenant's
    [InlineData(null, "tr-TR", "tr")]
    [InlineData(null, null, "en")]   // else en
    [InlineData(null, "de", "en")]   // a tenant language outside the seven is not sent as is
    public void The_language_of_a_mail_is_the_persons_then_the_tenants_then_english(string? preferred, string? tenant, string expected) =>
        Assert.Equal(expected, UserLanguages.ForRecipient(preferred, tenant));

    // ---------------------------------------------------------------- the invitation / reset link

    [Theory]
    [InlineData("fr", "tr", "fr")]
    [InlineData(null, "tr", "tr")]
    public async Task The_invitation_is_in_the_invited_persons_language_when_they_have_one(string? preferred, string tenant, string expected)
    {
        var user = new User(Recipient, "hash", "Jean", "Dupont", Tenant);
        user.SetPreferredLanguage(preferred);
        var service = Invitations(new InMemoryUserRepository([user]), tenant);

        using var message = await service.BuildMessageAsync(Recipient, "t", CancellationToken.None);

        Assert.Equal(TenantUserInvitationEmailTemplate.Subject(expected, "Diten Pharma", isPasswordReset: false), message.Subject);
    }

    // ---------------------------------------------------------------- the sign-in code

    [Fact]
    public async Task The_sign_in_code_is_in_the_persons_language_when_they_have_one()
    {
        var user = new User(Recipient, "hash", "Jean", "Dupont", Tenant);
        user.SetPreferredLanguage("es");
        var service = Codes(new InMemoryUserRepository([user]), "tr");

        using var message = await service.BuildMessageAsync(Tenant, Recipient, "482915", DateTime.UtcNow.AddMinutes(5), CancellationToken.None);

        Assert.Equal(VerificationCodeEmailTemplate.Subject("es"), message.Subject);
    }

    [Fact]
    public async Task A_user_store_that_cannot_be_read_still_sends_the_code_in_the_tenants_language()
    {
        // The language is a nicety; the code is the sign-in. A failed read of the preference never stops the mail.
        var service = Codes(System.Reflection.DispatchProxy.Create<IUserRepository, TenantUserEmailFixRoundOneTests.BrokenUsersProxy>(), "tr");

        using var message = await service.BuildMessageAsync(Tenant, Recipient, "482915", DateTime.UtcNow.AddMinutes(5), CancellationToken.None);

        Assert.Equal(VerificationCodeEmailTemplate.Subject("tr"), message.Subject);
        Assert.Contains("482915", message.Body);
    }

    // ---------------------------------------------------------------- every language a person can choose has its words

    [Fact]
    public void Every_tenant_mail_AuthService_sends_has_its_own_words_in_each_of_the_seven_languages()
    {
        var english = (
            TenantUserInvitationEmailTemplate.TextsFor("en").ToString(),
            TenantUserInvitationEmailTemplate.ResetTextsFor("en").ToString(),
            VerificationCodeEmailTemplate.TextsFor("en").ToString());

        Assert.Equal(UserLanguages.Supported.OrderBy(x => x), VerificationCodeEmailTemplate.Languages.OrderBy(x => x));
        foreach (var language in UserLanguages.Supported.Where(l => l != "en"))
        {
            // Not the English words under another name: a missing language would fall back to (or throw for) English.
            Assert.NotEqual(english.Item1, TenantUserInvitationEmailTemplate.TextsFor(language).ToString());
            Assert.NotEqual(english.Item2, TenantUserInvitationEmailTemplate.ResetTextsFor(language).ToString());
            Assert.NotEqual(english.Item3, VerificationCodeEmailTemplate.TextsFor(language).ToString());
        }
    }

    // ---------------------------------------------------------------- FIX1

    [Fact]
    public async Task A_write_that_finds_no_account_answers_404_and_writes_no_audit_row()
    {
        var user = new User(Recipient, "hash", "Jean", "Dupont", Tenant);
        var audit = new RecordingAudit();
        var handler = new Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers.UpdateMyPreferencesCommandHandler(
            new WriteFindsNothing(user), audit);

        var result = await handler.Handle(
            new Diten.AuthService.Application.Features.Users.Commands.UpdateMyPreferencesCommand(user.Id, Tenant, "fr"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Empty(audit.Events);
    }

    [Fact]
    public async Task An_unreadable_preference_is_a_warning_by_tenant_with_no_address_and_no_code()
    {
        var logger = new Lines();
        var service = new SmtpOtpDeliveryService(
            Options.Create(new SmtpOptions { Host = "smtp.test", FromEmail = "noreply@di10.test" }),
            new FixedIdentity(new TenantEmailIdentity("Diten Pharma", "tr", null, null)),
            System.Reflection.DispatchProxy.Create<IUserRepository, TenantUserEmailFixRoundOneTests.BrokenUsersProxy>(),
            logger);

        using var message = await service.BuildMessageAsync(Tenant, Recipient, "482915", DateTime.UtcNow.AddMinutes(5), CancellationToken.None);

        var line = Assert.Single(logger.Entries, e => e.Line.Contains("preferred_language_unread", StringComparison.Ordinal));
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, line.Level);
        Assert.Contains(Tenant.ToString(), line.Line);
        Assert.DoesNotContain(Recipient, line.Line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("482915", line.Line);
    }

    [Fact]
    public async Task An_invitation_to_an_address_written_in_capitals_still_finds_the_persons_language()
    {
        var user = new User(Recipient, "hash", "Jean", "Dupont", Tenant);
        user.SetPreferredLanguage("fr");
        var service = Invitations(new InMemoryUserRepository([user]), "tr");

        using var message = await service.BuildMessageAsync(" " + Recipient.ToUpperInvariant() + " ", "t", CancellationToken.None);

        Assert.Equal(TenantUserInvitationEmailTemplate.Subject("fr", "Diten Pharma", isPasswordReset: false), message.Subject);
    }

    private sealed class RecordingAudit : IUserAuditRecorder
    {
        public List<string> Events { get; } = [];

        public Task RecordAsync(string eventName, Guid tenantId, Guid targetUserId, IReadOnlyDictionary<string, object?> metadata, CancellationToken ct = default)
        {
            Events.Add(eventName);
            return Task.CompletedTask;
        }

    }

    /// <summary>The account is read, then gone before the write (deleted meanwhile): the write matches nothing.</summary>
    private sealed class WriteFindsNothing(User user) : IUserRepository
    {
        private readonly InMemoryUserRepository _inner = new([user]);
        public Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct) => _inner.GetByEmailAndTenantAsync(email, tenantId, ct);
        public Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct) => _inner.GetByUserNameAndTenantAsync(normalizedUserName, tenantId, ct);
        public Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => _inner.GetByIdAndTenantAsync(id, tenantId, ct);
        public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct) => _inner.GetByPasswordResetTokenHashAsync(tokenHash, ct);
        public Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct) => _inner.GetAllByTenantAsync(tenantId, page, pageSize, ct);
        public Task<IReadOnlyList<User>> SearchActiveAsync(Guid tenantId, string? term, int limit, CancellationToken ct) => _inner.SearchActiveAsync(tenantId, term, limit, ct);
        public Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct) => _inner.GetCountByTenantAsync(tenantId, ct);
        public Task<User> CreateAsync(User u, CancellationToken ct) => _inner.CreateAsync(u, ct);
        public Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => _inner.SoftDeleteAsync(id, tenantId, ct);
        public Task RecordLoginOutcomeAsync(User u, Guid tenantId, CancellationToken ct) => _inner.RecordLoginOutcomeAsync(u, tenantId, ct);
        public object CaptureState(User u) => _inner.CaptureState(u);
        public Task<bool> TryWriteChangesAsync(User u, object capturedState, Guid tenantId, UserWriteCondition condition, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> SetPasswordResetTokenAsync(Guid userId, Guid tenantId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct) => _inner.SetPasswordResetTokenAsync(userId, tenantId, tokenHash, expiresAtUtc, ct);
        public Task<LoginFailureOutcome> RecordLoginFailureAsync(Guid userId, Guid tenantId, int maxFailedAttempts, int lockoutDurationMinutes, CancellationToken ct) => _inner.RecordLoginFailureAsync(userId, tenantId, maxFailedAttempts, lockoutDurationMinutes, ct);
    }

    private sealed class Lines : Microsoft.Extensions.Logging.ILogger<SmtpOtpDeliveryService>
    {
        public List<(Microsoft.Extensions.Logging.LogLevel Level, string Line)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    // ---------------------------------------------------------------- helpers

    private static TenantUserInvitationEmailService Invitations(IUserRepository users, string tenantLanguage) => new(
        Options.Create(new SmtpOptions { Host = "smtp.invalid", Port = 25, FromEmail = "no-reply@di10.test" }),
        Options.Create(new PlatformServiceOptions { FrontendBaseUrl = "https://app.di10.test/" }),
        new Context { TenantId = Tenant },
        users,
        new FixedIdentity(new TenantEmailIdentity("Diten Pharma", tenantLanguage, null, null)),
        NullLogger<TenantUserInvitationEmailService>.Instance);

    private static SmtpOtpDeliveryService Codes(IUserRepository users, string tenantLanguage) => new(
        Options.Create(new SmtpOptions { Host = "smtp.test", FromEmail = "noreply@di10.test" }),
        new FixedIdentity(new TenantEmailIdentity("Diten Pharma", tenantLanguage, null, null)),
        users);

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
}
