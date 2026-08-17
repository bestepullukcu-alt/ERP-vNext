using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollSources;

public sealed class PayrollExternalSystemProfile : TenantScopedEntity
{
    public required string Code { get; set; }
    public required string DisplayName { get; set; }
    public PayrollProviderFamily ProviderFamily { get; set; }
    public required string ExternalPayrollSystemId { get; set; }
    public PayrollSourceLifecycleState LifecycleState { get; set; } = PayrollSourceLifecycleState.Draft;
    public string? ConnectionProfileReference { get; set; }
    public string? SupportOwner { get; set; }
    public string? Notes { get; set; }
    public Guid? ContractProfileId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
