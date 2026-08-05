using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IServicePrincipalRepository
{
    Task<bool> TryCreateAsync(ServicePrincipal principal, CancellationToken cancellationToken);
    Task<ServicePrincipal?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken);
    Task<bool> TryReplaceAsync(ServicePrincipal principal, long expectedVersion, CancellationToken cancellationToken);
}
