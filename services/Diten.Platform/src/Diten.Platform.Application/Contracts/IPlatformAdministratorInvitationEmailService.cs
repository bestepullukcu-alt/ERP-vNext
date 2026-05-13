namespace Diten.Platform.Application.Contracts;

public interface IPlatformAdministratorInvitationEmailService
{
    Task SendInvitationAsync(string email, string userName, string displayName, string temporaryPassword, CancellationToken ct);
}
