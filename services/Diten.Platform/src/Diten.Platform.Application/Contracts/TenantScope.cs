using Diten.Platform.Common.Tenancy;

namespace Diten.Platform.Application.Contracts;

/// <summary>
/// Provides a disposable tenant scope that restores the previous tenant state on dispose.
/// Supports nested scopes (TenantA → TenantB → restore to TenantA → restore to unresolved).
/// </summary>
public static class TenantScope
{
    public static IDisposable Begin(ITenantContext tenantContext, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        var snapshot = CaptureSnapshot(tenantContext);
        tenantContext.SetTenant(tenantId);
        return new TenantScopeHandle(tenantContext, snapshot);
    }

    public static IDisposable BeginPlatform(ITenantContext tenantContext, Guid targetTenantId)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        var snapshot = CaptureSnapshot(tenantContext);
        tenantContext.SetPlatformContext(targetTenantId);
        return new TenantScopeHandle(tenantContext, snapshot);
    }

    private static TenantSnapshot CaptureSnapshot(ITenantContext tenantContext)
    {
        if (!tenantContext.IsResolved)
        {
            return TenantSnapshot.Unresolved;
        }

        return tenantContext.IsPlatformContext
            ? TenantSnapshot.Platform(tenantContext.TargetTenantId ?? tenantContext.TenantId)
            : TenantSnapshot.Tenant(tenantContext.TenantId);
    }

    private readonly record struct TenantSnapshot(bool WasResolved, bool WasPlatformContext, Guid TenantId)
    {
        public static readonly TenantSnapshot Unresolved = new(false, false, Guid.Empty);

        public static TenantSnapshot Tenant(Guid tenantId) => new(true, false, tenantId);

        public static TenantSnapshot Platform(Guid targetTenantId) => new(true, true, targetTenantId);

        public void RestoreTo(ITenantContext tenantContext)
        {
            if (!WasResolved)
            {
                tenantContext.ClearTenant();
                return;
            }

            if (WasPlatformContext)
            {
                tenantContext.SetPlatformContext(TenantId);
                return;
            }

            tenantContext.SetTenant(TenantId);
        }
    }

    private sealed class TenantScopeHandle : IDisposable
    {
        private readonly ITenantContext _tenantContext;
        private readonly TenantSnapshot _snapshot;
        private bool _disposed;

        public TenantScopeHandle(ITenantContext tenantContext, TenantSnapshot snapshot)
        {
            _tenantContext = tenantContext;
            _snapshot = snapshot;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _snapshot.RestoreTo(_tenantContext);
            _disposed = true;
        }
    }
}
