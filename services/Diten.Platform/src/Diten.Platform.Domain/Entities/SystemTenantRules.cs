namespace Diten.Platform.Domain.Entities;

// Single source of truth for "is this the platform system tenant?". The seeded Internal tenant
// (Id 00000000-...0001, Code "PLATFORM") is the home tenant of platform admins; suspending or deleting
// it would break the platform, and it must never appear in the customer tenant list.
public static class SystemTenantRules
{
    /// <summary>
    /// The seeded platform system tenant Id (Code "PLATFORM") — the home tenant of platform admins. Distinct from
    /// <c>AuditTenantIds.PlatformSystemTenantId</c> (…2121), which is a separate audit-only concept.
    /// </summary>
    public static readonly Guid PlatformSystemTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static bool IsSystemTenant(Tenant tenant) => tenant.TenantType == TenantType.Internal;

    /// <summary>
    /// Id-based system-tenant test used by the customer tenant LIST/stats so operator-created Internal tenants
    /// stay visible — only the specific platform system tenant is hidden. Type-based <see cref="IsSystemTenant"/>
    /// is intentionally left unchanged; it guards deletes/suspends where the broader rule still applies.
    /// </summary>
    public static bool IsSystemTenantId(Guid tenantId) => tenantId == PlatformSystemTenantId;
}
