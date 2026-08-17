using System.Net;
using System.Net.Mail;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class SmtpOtpDeliveryService : IOtpDeliveryService
{
    private readonly SmtpOptions _options;

    public SmtpOtpDeliveryService(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendEmailOtpAsync(string email, string code, DateTime expiresAtUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("Email OTP delivery is not configured.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = "Diten ERP verification code",
            Body = $"Your Diten ERP verification code is {code}. It expires at {expiresAtUtc:HH:mm} UTC.",
            IsBodyHtml = false
        };
        message.To.Add(email);

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
}
