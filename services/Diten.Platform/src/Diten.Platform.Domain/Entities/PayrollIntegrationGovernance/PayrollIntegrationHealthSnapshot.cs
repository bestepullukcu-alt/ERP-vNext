using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;

public sealed class PayrollIntegrationHealthSnapshot : TenantScopedEntity
{
    public Guid? RunId { get; set; }
    public PayrollIntegrationHealthState HealthState { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public string? RedactedMessage { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
