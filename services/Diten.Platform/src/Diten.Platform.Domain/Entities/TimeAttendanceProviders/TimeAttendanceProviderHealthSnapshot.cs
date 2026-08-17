using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.TimeAttendanceProviders;

public sealed class TimeAttendanceProviderHealthSnapshot : TenantScopedEntity
{
    public required Guid ProviderProfileId { get; set; }
    public TimeAttendanceProviderHealthState HealthState { get; set; } = TimeAttendanceProviderHealthState.Unknown;
    public DateTimeOffset CheckedAt { get; set; }
    public string? RedactedMessage { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
