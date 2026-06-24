namespace Diten.Platform.Domain.Entities;

// Single source of truth for "is this the platform system tenant?". The seeded Internal tenant
// (Id 00000000-...0001, Code "PLATFORM") is the home tenant of platform admins; suspending or deleting
// it would break the platform, and it must never appear in the customer tenant list.
public static class SystemTenantRules
{
    public static bool IsSystemTenant(Tenant tenant) => tenant.TenantType == TenantType.Internal;
}
