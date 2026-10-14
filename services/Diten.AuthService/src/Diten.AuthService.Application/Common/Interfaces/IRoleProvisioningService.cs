namespace Diten.AuthService.Application.Common.Interfaces;

public interface IRoleProvisioningService
{
    /// <summary>
    /// WP-0018-ROLE1 — creates each default role the tenant LACKS, from the template, and audits it; a role the tenant already has
    /// is never changed (no key added, none removed). <paramref name="trigger"/> names the caller (<c>DefaultRoleProvisioningTriggers</c>).
    /// </summary>
    Task EnsureDefaultRolesAsync(Guid tenantId, string trigger, CancellationToken ct = default);
}
