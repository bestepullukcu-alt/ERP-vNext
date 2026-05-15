using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

public interface IAuditRetentionPolicyRepository
{
    Task<AuditEventRetentionPolicy?> GetActivePolicyByIdAsync(Guid id, CancellationToken ct = default);
    Task<AuditEventRetentionPolicy?> GetActivePolicyAsync(AuditCategory category, string planTierCode, CancellationToken ct = default);
    Task<AuditEventRetentionPolicy?> GetDefaultPolicyAsync(AuditCategory category, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEventRetentionPolicy>> GetActivePoliciesAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(AuditEventRetentionPolicy policy, CancellationToken ct = default);
}
