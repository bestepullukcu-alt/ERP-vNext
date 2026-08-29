using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IServiceCredentialDescriptorRepository
{
    Task<bool> TryCreateAsync(ServiceCredentialDescriptor descriptor, CancellationToken cancellationToken);
    Task<IReadOnlyList<ServiceCredentialDescriptor>> GetAcceptedAsync(Guid servicePrincipalId, DateTimeOffset atUtc, CancellationToken cancellationToken);
}
