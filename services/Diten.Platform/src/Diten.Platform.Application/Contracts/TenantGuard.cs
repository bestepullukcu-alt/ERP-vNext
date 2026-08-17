using Diten.Platform.Common.Tenancy;

namespace Diten.Platform.Application.Contracts;

public static class TenantGuard
{
    public static Guid RequireTenant(ITenantContext tenantContext)
    {
        if (!tenantContext.IsResolved)
        {
            throw new InvalidOperationException("Tenant context is not resolved.");
        }

        return tenantContext.TenantId;
    }
}
