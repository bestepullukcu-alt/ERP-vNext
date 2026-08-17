using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Diten.Platform.Infrastructure.Services.Notifications;

public sealed class MailKitSmtpClientFactory : ISmtpClientFactory
{
    public ISmtpTransport Create() => new MailKitSmtpTransport(new SmtpClient());

    private sealed class MailKitSmtpTransport : ISmtpTransport
    {
        private readonly SmtpClient _client;

        public MailKitSmtpTransport(SmtpClient client)
        {
            _client = client;
        }

        public int Timeout
        {
            get => _client.Timeout;
            set => _client.Timeout = value;
        }

        public Task ConnectAsync(string host, int port, SecureSocketOptions socketOptions, CancellationToken ct) =>
            _client.ConnectAsync(host, port, socketOptions, ct);

        public Task AuthenticateAsync(string userName, string password, CancellationToken ct) =>
            _client.AuthenticateAsync(userName, password, ct);

        public async Task<string> SendAsync(MimeMessage message, CancellationToken ct)
        {
            var response = await _client.SendAsync(message, ct);
            if (!string.IsNullOrWhiteSpace(response))
            {
                return response;
            }

            return string.IsNullOrWhiteSpace(message.MessageId)
                ? string.Empty
                : message.MessageId;
        }

        public Task DisconnectAsync(bool quit, CancellationToken ct) =>
            _client.DisconnectAsync(quit, ct);

        public void Dispose() => _client.Dispose();
    }
}
