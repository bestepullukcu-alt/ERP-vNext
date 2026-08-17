using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;

public sealed class PayrollIntegrationException : TenantScopedEntity
{
    public Guid RunId { get; set; }
    public required string ExceptionCode { get; set; }
    public PayrollIntegrationSeverity Severity { get; set; }
    public PayrollIntegrationExceptionState ExceptionState { get; set; } = PayrollIntegrationExceptionState.Open;
    public Guid? SourceReferenceId { get; set; }
    public Guid? AssignedToActorId { get; set; }
    public Guid? ResolutionWorkflowId { get; set; }
    public required string RedactedMessage { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
