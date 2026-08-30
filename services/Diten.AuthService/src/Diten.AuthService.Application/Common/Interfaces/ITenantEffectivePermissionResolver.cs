namespace Diten.AuthService.Application.Common.Interfaces;

public interface ITenantEffectivePermissionResolver
{
    Task<IReadOnlyList<string>> ResolveAsync(
        Guid tenantId,
        IEnumerable<string>? rolePermissions,
        CancellationToken cancellationToken);
}
