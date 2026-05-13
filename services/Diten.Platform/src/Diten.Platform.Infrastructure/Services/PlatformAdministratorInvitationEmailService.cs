using System.Net;
using System.Net.Mail;
using System.Text;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Infrastructure.Services.EmailTemplates;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

public sealed class PlatformAdministratorInvitationEmailService : IPlatformAdministratorInvitationEmailService
{
    private readonly SmtpOptions _smtpOptions;
    private readonly AuthServiceOptions _authServiceOptions;

    public PlatformAdministratorInvitationEmailService(IOptions<SmtpOptions> smtpOptions, IOptions<AuthServiceOptions> authServiceOptions)
    {
        _smtpOptions = smtpOptions.Value;
        _authServiceOptions = authServiceOptions.Value;
    }

    public async Task SendInvitationAsync(string email, string userName, string displayName, string temporaryPassword, CancellationToken ct)
    {
        ValidateSmtpConfiguration();
        var loginUrl = $"{_authServiceOptions.FrontendBaseUrl.TrimEnd('/')}/platform/login";

        using var message = new MailMessage
        {
            From = new MailAddress(_smtpOptions.FromEmail, _smtpOptions.FromName),
            Subject = PlatformAdministratorInvitationEmailTemplate.Subject(),
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
            IsBodyHtml = true,
            Body = PlatformAdministratorInvitationEmailTemplate.Render(displayName, userName, email, loginUrl, temporaryPassword)
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

    private void ValidateSmtpConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host) ||
            string.IsNullOrWhiteSpace(_smtpOptions.FromEmail))
        {
            throw new InvalidOperationException("SMTP configuration is incomplete.");
        }
    }
}
