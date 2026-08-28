using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Repositories;

public interface IServiceClientIdentityRepository
{
    Task<ServiceClientIdentity?> GetByClientCodeAsync(string clientCode, CancellationToken ct);
}
