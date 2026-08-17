using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;

public sealed class PayrollIntegrationMappingControl : TenantScopedEntity
{
    public Guid RunId { get; set; }
    public PayrollIntegrationMappingScope MappingScope { get; set; }
    public Guid SourceReferenceId { get; set; }
    public Guid? TargetReferenceId { get; set; }
    public PayrollIntegrationControlState ControlState { get; set; } = PayrollIntegrationControlState.Pending;
    public string? MismatchCode { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
