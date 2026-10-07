using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Services.EmailTemplates;
using Diten.AuthService.Infrastructure.Settings;
using Diten.BuildingBlocks.Email;
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

    public SmtpOtpDeliveryService(IOptions<SmtpOptions> options, ITenantEmailIdentityClient? identity = null)
    {
        _options = options.Value;
        _identity = identity;
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
        var language = identity?.Language;
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
}
