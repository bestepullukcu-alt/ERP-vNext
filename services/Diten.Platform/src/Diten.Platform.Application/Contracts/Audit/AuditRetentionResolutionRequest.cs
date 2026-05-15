using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Contracts.Audit;

public sealed class AuditRetentionResolutionRequest
{
    public AuditCategory Category { get; init; }
    public string PlanTierCode { get; init; } = AuditEventRetentionPolicy.DefaultPlanTierCode;
    public Guid? TenantId { get; init; }
}
