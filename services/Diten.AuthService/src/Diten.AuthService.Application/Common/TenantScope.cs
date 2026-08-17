namespace Diten.AuthService.Application.Common;

public static class TenantScope
{
    public static IDisposable Begin(ITenantContext tenantContext, Guid tenantId)
    {
        tenantContext.SetTenant(tenantId);
        return TenantScopeHandle.Instance;
    }

    private sealed class TenantScopeHandle : IDisposable
    {
        public static readonly TenantScopeHandle Instance = new();
        public void Dispose() { }
    }
}
