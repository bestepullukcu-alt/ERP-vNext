namespace Diten.AuthService.Application.Common.Interfaces;

public interface IPlatformAdministratorStatusClient
{
    Task<bool> IsActiveAsync(string email, CancellationToken ct);
    Task MarkLoginAcceptedAsync(string email, CancellationToken ct);
}
