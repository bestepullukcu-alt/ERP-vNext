using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.TimeAttendanceProviders;

public sealed class TimeAttendanceContractProfile : TenantScopedEntity
{
    public required Guid ProviderProfileId { get; set; }
    public required string ContractVersion { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public IReadOnlyList<TimeAttendanceSupportedObjectType> SupportedObjectTypes { get; set; } = [];
    public IReadOnlyList<string> StatusVocabulary { get; set; } = [];
    public IReadOnlyList<string> ErrorVocabulary { get; set; } = [];
    public string? CorrelationIdPattern { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
