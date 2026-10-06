namespace Diten.ProcurementService.Application.Common;

/// <summary>
/// Scoped TenantContext implementasyonu.
/// TenantResolutionMiddleware tarafından SetTenant() ile doldurulur (TenantId + LegalEntityId).
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid _tenantId;
    private Guid _legalEntityId;
    private bool _isResolved;
    private bool _isPlatformContext;
    private Guid? _targetTenantId;

    public Guid TenantId => _isResolved
        ? _tenantId
        : throw new InvalidOperationException("TenantId henüz çözümlenmedi. TenantResolutionMiddleware devrede mi?");

    public Guid LegalEntityId => _isResolved
        ? _legalEntityId
        : throw new InvalidOperationException("LegalEntityId henüz çözümlenmedi. TenantResolutionMiddleware devrede mi?");

    public bool IsResolved => _isResolved;
    public bool IsPlatformContext => _isPlatformContext;
    public Guid? TargetTenantId => _targetTenantId;

    public void SetTenant(Guid tenantId, Guid legalEntityId)
    {
        _tenantId = tenantId;
        _legalEntityId = legalEntityId;
        _isResolved = true;
        _isPlatformContext = false;
        _targetTenantId = null;
    }

    public void SetPlatformContext(Guid targetTenantId, Guid legalEntityId)
    {
        _tenantId = targetTenantId;
        _legalEntityId = legalEntityId;
        _isResolved = true;
        _isPlatformContext = true;
        _targetTenantId = targetTenantId;
    }
}
