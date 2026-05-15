namespace Diten.Platform.Common.Tenancy;

/// <summary>
/// Scoped TenantContext implementation.
/// Populated by TenantResolutionMiddleware via SetTenant().
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid _tenantId;
    private bool _isResolved;
    private bool _isPlatformContext;
    private Guid? _targetTenantId;

    public Guid TenantId => _isResolved
        ? _tenantId
        : throw new InvalidOperationException("TenantId has not been resolved yet. Is the TenantResolutionMiddleware enabled?");

    public bool IsResolved => _isResolved;
    public bool IsPlatformContext => _isPlatformContext;
    public Guid? TargetTenantId => _targetTenantId;

    public void SetTenant(Guid tenantId)
    {
        _tenantId = tenantId;
        _isResolved = true;
        _isPlatformContext = false;
        _targetTenantId = null;
    }

    public void SetPlatformContext(Guid targetTenantId)
    {
        _tenantId = targetTenantId;
        _isResolved = true;
        _isPlatformContext = true;
        _targetTenantId = targetTenantId;
    }

    public void ClearTenant()
    {
        _tenantId = Guid.Empty;
        _isResolved = false;
        _isPlatformContext = false;
        _targetTenantId = null;
    }
}
