using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

public interface ITenantAuditPreferenceRepository
{
    Task<TenantAuditPreference?> GetByTenantAndCategoryAsync(Guid tenantId, AuditCategory category, CancellationToken ct = default);
    Task UpsertAsync(TenantAuditPreference preference, AuditEventRetentionPolicy policy, CancellationToken ct = default);
}
