namespace Diten.AuthService.Application.Common.Interfaces;

public interface IPlatformAuthEmailService
{
    string BuildPlatformPasswordResetUrl(string email, string resetToken);
    Task SendPlatformPasswordResetAsync(string email, string resetToken, CancellationToken ct);
}
