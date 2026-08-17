using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollSources;

public sealed class PayrollResultReference : TenantScopedEntity
{
    public required Guid PayrollExternalSystemProfileId { get; set; }
    public required Guid PayrollCycleReferenceId { get; set; }
    public required string ExternalPayrollResultId { get; set; }
    public required string ResultVersion { get; set; }
    public PayrollResultState ResultState { get; set; } = PayrollResultState.Unknown;
    public DateTimeOffset? PublishedAt { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
