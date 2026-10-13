using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Services.EmailTemplates;
using Diten.AuthService.Infrastructure.Settings;
using Diten.BuildingBlocks.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

/// <summary>
/// The sign-in verification code. BL-454 slice 2 stage D: in the shared shell, under the tenant's name and in the
/// tenant's language (seven languages), the code in the body only.
///
/// <para>Sent once, synchronously, by the login that created the code: there is no dispatch record and no retry — a
/// failed send fails that login step, and "resend" (MfaChallengeService) makes a NEW code and refuses a challenge that
/// has expired. So an expired code is never sent again, and the code is stored only as a keyed hash.</para>
/// </summary>
public sealed class SmtpOtpDeliveryService : IOtpDeliveryService
{
    private readonly SmtpOptions _options;
    private readonly ITenantEmailIdentityClient? _identity;
    private readonly IUserRepository? _users;
    private readonly ILogger<SmtpOtpDeliveryService> _logger;

    /// <summary>FIX1 — the longest the sign-in code waits for the recipient's language; past it, the tenant's applies.</summary>
    public static readonly TimeSpan PreferredLanguageReadLimit = TimeSpan.FromSeconds(2);

    public SmtpOtpDeliveryService(
        IOptions<SmtpOptions> options,
        ITenantEmailIdentityClient? identity = null,
        IUserRepository? users = null,
        ILogger<SmtpOtpDeliveryService>? logger = null)
    {
        _options = options.Value;
        _identity = identity;
        _users = users;
        _logger = logger ?? NullLogger<SmtpOtpDeliveryService>.Instance;
    }

    public async Task SendEmailOtpAsync(Guid tenantId, string email, string code, DateTime expiresAtUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("Email OTP delivery is not configured.");
        }

        using var message = await BuildMessageAsync(tenantId, email, code, expiresAtUtc, ct);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        await client.SendMailAsync(message, ct);
    }

    /// <summary>The whole message, short of sending it — what a test reads to see what would leave.</summary>
    /// <param name="tenantId">The challenge's tenant (BL-454 stage D FIX1 K1): a request header that names another tenant
    /// does not put that tenant's name on someone else's code.</param>
    public async Task<MailMessage> BuildMessageAsync(Guid tenantId, string email, string code, DateTime expiresAtUtc, CancellationToken ct)
    {
        var identity = tenantId == Guid.Empty || _identity is null ? null : await _identity.GetAsync(tenantId, ct);
        // WP-EMAIL-LANG-01 — the recipient's own language first, then the tenant's, then en.
        var language = Diten.AuthService.Domain.Entities.UserLanguages.ForRecipient(
            await PreferredLanguageOfAsync(tenantId, email, ct), identity?.Language);
        var rendered = VerificationCodeEmailTemplate.Render(language, identity?.DisplayName, code, expiresAtUtc);

        var message = new MailMessage
        {
            From = new MailAddress(
                _options.FromEmail,
                EmailSender.ComposeDisplayName(identity?.SenderName, identity?.DisplayName, language),
                Encoding.UTF8),
            Subject = VerificationCodeEmailTemplate.Subject(language),
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
            Body = rendered.Text
        };
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(rendered.Html, Encoding.UTF8, MediaTypeNames.Text.Html));
        message.To.Add(email);
        return message;
    }

    // The recipient's own choice, when it can be read. A read that fails or takes longer than PreferredLanguageReadLimit never
    // stops or delays the code (the tenant's language applies) — and is said in the log by tenant, never with the address or
    // the code (FIX1).
    private async Task<string?> PreferredLanguageOfAsync(Guid tenantId, string email, CancellationToken ct)
    {
        if (_users is null || tenantId == Guid.Empty || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(PreferredLanguageReadLimit);
        try
        {
            return (await _users.GetByEmailAndTenantAsync(email.Trim().ToLowerInvariant(), tenantId, bounded.Token))?.PreferredLanguage;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "auth.otp.preferred_language_unread TenantId={TenantId} ExceptionType={ExceptionType}. The tenant's language is used.",
                tenantId, ex.GetType().Name);
            return null;
        }
    }
}
