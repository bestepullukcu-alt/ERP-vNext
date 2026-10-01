namespace Diten.AuthService.Domain.Repositories;

public interface IServiceClientTenantGrantRepository
{
    Task<bool> HasEnabledGrantAsync(Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct);
}
