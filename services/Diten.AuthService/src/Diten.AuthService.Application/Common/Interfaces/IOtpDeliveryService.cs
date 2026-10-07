namespace Diten.AuthService.Application.Common.Interfaces;

public interface IOtpDeliveryService
{
    /// <param name="tenantId">The tenant the code is FOR — the challenge's tenant, never the request's header (BL-454 stage D
    /// FIX1 K1): it decides the name on the mail and its language.</param>
    Task SendEmailOtpAsync(Guid tenantId, string email, string code, DateTime expiresAtUtc, CancellationToken ct);
}
