using System.Net;
using System.Net.Mail;
using System.Text;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Services.EmailTemplates;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class PlatformAuthEmailService : IPlatformAuthEmailService
{
    private readonly SmtpOptions _smtpOptions;
    private readonly PlatformServiceOptions _platformOptions;

    public PlatformAuthEmailService(IOptions<SmtpOptions> smtpOptions, IOptions<PlatformServiceOptions> platformOptions)
    {
        _smtpOptions = smtpOptions.Value;
        _platformOptions = platformOptions.Value;
    }

    public async Task SendPlatformPasswordResetAsync(string email, string resetToken, CancellationToken ct)
    {
        ValidateSmtpConfiguration();
        var resetUrl = BuildPlatformPasswordResetUrl(email, resetToken);

        using var message = new MailMessage
        {
            From = new MailAddress(_smtpOptions.FromEmail, _smtpOptions.FromName),
            Subject = PlatformPasswordResetEmailTemplate.Subject(),
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
            IsBodyHtml = true,
            Body = PlatformPasswordResetEmailTemplate.Render(resetUrl)
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

    public string BuildPlatformPasswordResetUrl(string email, string resetToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_platformOptions.FrontendBaseUrl)
            ? "http://localhost:5001"
            : _platformOptions.FrontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}/platform/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(resetToken)}";
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
