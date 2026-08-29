namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// Tenant + year scoped monotonic sequence backing AccountCode auto-generation (ACC-{YYYY}-{sequence}).
/// One document per (TenantId, Year). Atomic increment via repository. No magic numbers / hardcoded tenants.
/// </summary>
public sealed class AccountCodeSequence : EntityBase
{
    public int Year { get; set; }
    public long Current { get; set; }
}
