using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MailKitAuthenticationException = MailKit.Security.AuthenticationException;
using TlsAuthenticationException = System.Security.Authentication.AuthenticationException;

namespace Diten.Platform.Infrastructure.Services.Notifications;

internal static class MessagingProviderErrorMapper
{
    private static readonly HashSet<int> StableSmtpCodes =
    [
        421, 450, 451, 452, 530, 535, 550, 552, 553
    ];

    public static (string ErrorCode, string ErrorMessage) Map(Exception exception)
    {
        switch (exception)
        {
            case OperationCanceledException:
            case TimeoutException:
                return (MessagingProviderErrorCodes.ProviderTimeout, "Operation timed out.");

            case SmtpCommandException smtpCommand:
                return MapSmtpCommand(smtpCommand);

            case SslHandshakeException:
                return (MessagingProviderErrorCodes.ProviderTlsFailed, "TLS handshake failed.");

            case MailKitAuthenticationException:
            case TlsAuthenticationException:
                return (MessagingProviderErrorCodes.ProviderAuthFailed, "Authentication failed.");

            case SmtpProtocolException:
            case ServiceNotConnectedException:
            case ServiceNotAuthenticatedException:
            case SocketException:
                return (MessagingProviderErrorCodes.ProviderConnectivityFailed, "Connectivity failed.");

            case System.IO.IOException ioException when HasTlsInnerException(ioException):
                return (MessagingProviderErrorCodes.ProviderTlsFailed, "TLS handshake failed.");

            case System.IO.IOException:
                return (MessagingProviderErrorCodes.ProviderConnectivityFailed, "Connectivity failed.");

            default:
                return (MessagingProviderErrorCodes.ProviderUnknown, "Provider failure.");
        }
    }

    private static (string ErrorCode, string ErrorMessage) MapSmtpCommand(SmtpCommandException exception)
    {
        var statusCode = (int)exception.StatusCode;

        if (statusCode == 535 || statusCode == 530)
        {
            return (MessagingProviderErrorCodes.ProviderAuthFailed, "Authentication failed.");
        }

        if (StableSmtpCodes.Contains(statusCode))
        {
            return ($"{MessagingProviderErrorCodes.ProviderRejected}:{statusCode}", "Server rejected the message.");
        }

        return (MessagingProviderErrorCodes.ProviderRejected, "Server rejected the message.");
    }

    private static bool HasTlsInnerException(Exception exception)
    {
        var current = exception.InnerException;
        while (current is not null)
        {
            if (current is TlsAuthenticationException or SslHandshakeException)
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
