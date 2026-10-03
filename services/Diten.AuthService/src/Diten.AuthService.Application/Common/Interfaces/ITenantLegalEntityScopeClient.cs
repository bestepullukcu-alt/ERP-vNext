namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>Resolves one server-authoritative legal-entity scope for a tenant actor.</summary>
public interface ITenantLegalEntityScopeClient
{
    /// <returns>The sole currently authorized LegalEntity id; null for zero, multiple, unavailable or invalid results.</returns>
    Task<Guid?> ResolveSingleAsync(Guid tenantId, Guid userId, CancellationToken ct);
}
