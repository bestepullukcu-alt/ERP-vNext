using System.Net;
using System.Net.Mail;
using System.Text;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Services.EmailTemplates;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

// Tenant-user invitation email (mirrors PlatformAuthEmailService). Sends a "set your password"
// link pointing at the tenant set-password page on the frontend shell.
public sealed class TenantUserInvitationEmailService : ITenantUserInvitationEmailService
{
    private readonly SmtpOptions _smtpOptions;
    private readonly PlatformServiceOptions _platformOptions;

    public TenantUserInvitationEmailService(IOptions<SmtpOptions> smtpOptions, IOptions<PlatformServiceOptions> platformOptions)
    {
        _smtpOptions = smtpOptions.Value;
        _platformOptions = platformOptions.Value;
    }

    public async Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct)
    {
        ValidateSmtpConfiguration();
        var setPasswordUrl = BuildTenantSetPasswordUrl(email, setupToken);

        using var message = new MailMessage
        {
            From = new MailAddress(_smtpOptions.FromEmail, _smtpOptions.FromName),
            Subject = TenantUserInvitationEmailTemplate.Subject(),
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
            IsBodyHtml = true,
            Body = TenantUserInvitationEmailTemplate.Render(setPasswordUrl)
        };
        message.To.Add(email);

        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl,
            Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password)
        };

        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, ct);
    }

    public string BuildTenantSetPasswordUrl(string email, string setupToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_platformOptions.FrontendBaseUrl)
            ? "http://localhost:5001"
            : _platformOptions.FrontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/account/set-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(setupToken)}";
    }

    private void ValidateSmtpConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host) ||
            string.IsNullOrWhiteSpace(_smtpOptions.FromEmail))
        {
            throw new InvalidOperationException("SMTP configuration is incomplete.");
        }
    }
}
