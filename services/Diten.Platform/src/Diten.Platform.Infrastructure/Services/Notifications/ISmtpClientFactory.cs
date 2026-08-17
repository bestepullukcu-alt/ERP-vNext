using MailKit;
using MailKit.Security;

namespace Diten.Platform.Infrastructure.Services.Notifications;

public interface ISmtpTransport : IDisposable
{
    Task ConnectAsync(string host, int port, SecureSocketOptions socketOptions, CancellationToken ct);
    Task AuthenticateAsync(string userName, string password, CancellationToken ct);
    Task<string> SendAsync(MimeKit.MimeMessage message, CancellationToken ct);
    Task DisconnectAsync(bool quit, CancellationToken ct);
    int Timeout { get; set; }
}

public interface ISmtpClientFactory
{
    ISmtpTransport Create();
}
