namespace Diten.MdmService.Application.Common;

/// <summary>
/// Scoped TenantContext implementasyonu.
/// TenantResolutionMiddleware tarafından SetTenant() ile doldurulur.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid _tenantId;
    private bool _isResolved;

    public Guid TenantId => _isResolved
        ? _tenantId
        : throw new InvalidOperationException("TenantId henüz çözümlenmedi. TenantResolutionMiddleware devrede mi?");

    public bool IsResolved => _isResolved;

    public void SetTenant(Guid tenantId)
    {
        _tenantId = tenantId;
        _isResolved = true;
    }
}
