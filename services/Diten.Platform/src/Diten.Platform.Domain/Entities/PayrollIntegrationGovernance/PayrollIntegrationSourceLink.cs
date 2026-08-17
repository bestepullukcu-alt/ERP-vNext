using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;

public sealed class PayrollIntegrationSourceLink : TenantScopedEntity
{
    public Guid RunId { get; set; }
    public PayrollIntegrationSourceType SourceType { get; set; }
    public Guid SourceReferenceId { get; set; }
    public required string SourceContractVersion { get; set; }
    public PayrollIntegrationLinkState LinkState { get; set; } = PayrollIntegrationLinkState.PendingValidation;
    public string? ValidationMessage { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
