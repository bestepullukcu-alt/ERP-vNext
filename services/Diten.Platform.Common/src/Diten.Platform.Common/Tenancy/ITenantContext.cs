namespace Diten.Platform.Common.Tenancy;

/// <summary>
/// Provides access to the current tenant ID across all layers.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    bool IsResolved { get; }
    bool IsPlatformContext { get; }
    Guid? TargetTenantId { get; }

    void SetTenant(Guid tenantId);
    void SetPlatformContext(Guid targetTenantId);

    /// <summary>
    /// Resets the context to the unresolved state. Used to restore the previous tenant
    /// state when no previous tenant was set (see <c>TenantScope</c>).
    /// </summary>
    void ClearTenant();
}
