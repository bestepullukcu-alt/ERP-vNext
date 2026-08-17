using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.TimeAttendanceProviders;

public sealed class TimeAttendanceExternalProviderProfile : TenantScopedEntity
{
    public required string Code { get; set; }
    public required string DisplayName { get; set; }
    public TimeAttendanceProviderFamily ProviderFamily { get; set; }
    public required string ExternalProviderAccountId { get; set; }
    public TimeAttendanceProviderLifecycleState LifecycleState { get; set; } = TimeAttendanceProviderLifecycleState.Draft;
    public string? ConnectionProfileReference { get; set; }
    public string? SupportOwner { get; set; }
    public string? Notes { get; set; }
    public Guid? ContractProfileId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
