using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Repositories;

public interface IServiceClientTenantGrantRepository
{
    Task<bool> HasEnabledGrantAsync(Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct);
    Task<ServiceClientTenantGrant?> GetAsync(
        Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct) =>
        throw new NotSupportedException("Operational grant lookup is not supported by this repository.");
    Task<OperationalMutationResult<ServiceClientTenantGrant>> SetEnabledOperationalAsync(
        ServiceClientTenantGrant grant, bool enabled, long expectedVersion, Guid commandId,
        string fingerprint, DateTimeOffset nowUtc, string actorId, CancellationToken ct) =>
        throw new NotSupportedException("Operational grant mutation is not supported by this repository.");
}
