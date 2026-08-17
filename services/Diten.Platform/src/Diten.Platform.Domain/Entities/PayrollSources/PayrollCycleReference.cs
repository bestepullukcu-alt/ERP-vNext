using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollSources;

public sealed class PayrollCycleReference : TenantScopedEntity
{
    public required Guid PayrollExternalSystemProfileId { get; set; }
    public required string ExternalPayCycleId { get; set; }
    public required string CycleCode { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public PayrollCycleProcessingState ProcessingState { get; set; } = PayrollCycleProcessingState.Unknown;
    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
