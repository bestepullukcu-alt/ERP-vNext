using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class EmployeeProfileProjection : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid HrisSourceProfileId { get; set; }
    public Guid PersonReferenceId { get; set; }
    public string ExternalEmployeeReference { get; set; } = string.Empty;
    public string EmploymentRecordReferenceKey { get; set; } = string.Empty;
    public string EmploymentStatusCode { get; set; } = string.Empty;
    public string WorkerTypeCode { get; set; } = string.Empty;
    public string SourceContractVersion { get; set; } = string.Empty;
    public EmployeeProjectionState ProjectionState { get; set; }
    public EmployeeVisibilityClassification VisibilityClassification { get; set; }
    public DateTimeOffset? SourceLastSyncedAt { get; set; }
    public long ProjectionVersion { get; set; } = 1;
    public DateTimeOffset? LastValidatedAt { get; set; }
    public string? DeferredReason { get; set; }
}
