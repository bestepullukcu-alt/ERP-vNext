namespace Diten.DevEnablementService.Application.Common;

/// <summary>
/// Scoped TenantContext implementasyonu.
/// TenantResolutionMiddleware tarafından SetTenant() ile doldurulur.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid _tenantId;
    private bool _isResolved;
    private bool _isPlatformContext;
    private Guid? _targetTenantId;

    public Guid TenantId => _isResolved
        ? _tenantId
        : throw new InvalidOperationException("TenantId henüz çözümlenmedi. TenantResolutionMiddleware devrede mi?");

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
}
