namespace Diten.AuthService.Application.Common.Interfaces;

public interface IOtpDeliveryService
{
    Task SendEmailOtpAsync(string email, string code, DateTime expiresAtUtc, CancellationToken ct);
}
