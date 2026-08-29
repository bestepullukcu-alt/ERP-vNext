namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0165 FU10 — tenant + year scoped monotonic sequence backing CampaignCode auto-generation
/// (CMP-{YYYY}-{sequence}). One document per (TenantId, Year), incremented atomically by the repository.
/// <para>Deliberately the same shape as the account sequence: one aggregate, two fields, no magic numbers and no
/// hardcoded tenants. Gaps in the sequence are acceptable and expected — a number is only ever taken when a campaign
/// is actually being written, never when a form is opened.</para>
/// </summary>
public sealed class CampaignCodeSequence : EntityBase
{
    public int Year { get; set; }

    public long Current { get; set; }
}
