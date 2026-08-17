using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollSources;

public sealed class PayrollSourceHealthSnapshot : TenantScopedEntity
{
    public required Guid PayrollExternalSystemProfileId { get; set; }
    public PayrollSourceHealthState HealthState { get; set; } = PayrollSourceHealthState.Unknown;
    public DateTimeOffset CheckedAt { get; set; }
    public string? RedactedMessage { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
