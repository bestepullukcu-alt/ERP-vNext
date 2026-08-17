using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.TimeAttendanceProviders;

public sealed class AttendanceSummaryReference : TenantScopedEntity
{
    public required Guid ProviderProfileId { get; set; }
    public required string ExternalSummaryId { get; set; }
    public required string ExternalEmployeeReference { get; set; }
    public DateOnly SummaryPeriodStart { get; set; }
    public DateOnly SummaryPeriodEnd { get; set; }
    public TimeAttendanceProcessingState SummaryState { get; set; } = TimeAttendanceProcessingState.Pending;
    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
