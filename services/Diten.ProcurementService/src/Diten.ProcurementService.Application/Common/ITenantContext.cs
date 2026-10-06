namespace Diten.ProcurementService.Application.Common;

/// <summary>
/// Scoped tenant + legal-entity bilgisi — TenantResolutionMiddleware tarafından populate edilir.
/// Her ikisi de server-resolved (JWT claim / header); asla request payload'dan alınmaz.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }

    /// <summary>Server-resolved legal-entity kimliği; her tenant-owned sorguda TenantId ile birlikte filtrelenir.</summary>
    Guid LegalEntityId { get; }

    bool IsResolved { get; }
    bool IsPlatformContext { get; }
    Guid? TargetTenantId { get; }

    void SetTenant(Guid tenantId, Guid legalEntityId);
    void SetPlatformContext(Guid targetTenantId, Guid legalEntityId);
}
