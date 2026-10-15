using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Repositories;

public interface IServiceClientTenantGrantRepository
{
    Task<bool> HasEnabledGrantAsync(Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct);

    /// <summary>MOD-0033-FU02 §F — the grant for the exact triple whether enabled, disabled or soft-deleted.</summary>
    Task<ServiceClientTenantGrant?> FindIncludingDeletedAsync(
        Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct) =>
        throw new NotSupportedException("Service client registry lookup is unavailable.");

    /// <summary>MOD-0033-FU02 §F — inserts a declared grant; never updates. False when it already exists.</summary>
    Task<bool> CreateIfAbsentAsync(ServiceClientTenantGrant grant, CancellationToken ct) =>
        throw new NotSupportedException("Service client registry creation is unavailable.");
}
