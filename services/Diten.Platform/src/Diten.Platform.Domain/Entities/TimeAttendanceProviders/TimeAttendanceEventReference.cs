using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.TimeAttendanceProviders;

public sealed class TimeAttendanceEventReference : TenantScopedEntity
{
    public required Guid ProviderProfileId { get; set; }
    public required string ExternalEventId { get; set; }
    public TimeAttendanceEventType EventType { get; set; }
    public required string ExternalEmployeeReference { get; set; }
    public DateTimeOffset ProviderTimestamp { get; set; }
    public string? ProviderTimeZoneId { get; set; }
    public TimeAttendanceProcessingState ProcessingState { get; set; } = TimeAttendanceProcessingState.Pending;
    public required string IdempotencyKey { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
