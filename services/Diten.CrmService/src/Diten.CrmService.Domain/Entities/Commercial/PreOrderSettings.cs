namespace Diten.CrmService.Domain.Entities.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-a — MOD-0168 §4.3: the tenant's pre-order settings (one per tenant; absent = the defaults). The
/// company prefixes are the ORD number prefix of each selling legal entity (user decision 2026-10-21: MDM's legal entity
/// has no short-code field yet — the map lives here until it does; a prefix that numbered an order is locked, RCS-001 §5.4).
/// </summary>
public sealed class PreOrderSettings : EntityBase
{
    public string ApprovalMode { get; set; } = PreOrderApprovalModes.Always;
    public int ApprovalLinkTtlHours { get; set; } = PreOrderSettingsDefaults.ApprovalLinkTtlHours;
    public List<PreOrderCountryTier> PriceTierByCountry { get; set; } = new();
    public int TransmissionRetryMax { get; set; } = PreOrderSettingsDefaults.TransmissionRetryMax;
    public List<PreOrderCompanyPrefix> CompanyPrefixes { get; set; } = new();
    public string? UpdatedBy { get; set; }
}

public sealed class PreOrderCountryTier
{
    public string CountryCode { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
}

public sealed class PreOrderCompanyPrefix
{
    public Guid LegalEntityId { get; set; }

    /// <summary>2–5 letters A–Z (RCS-001 §5.4 / §5.10).</summary>
    public string Prefix { get; set; } = string.Empty;
}

/// <summary>
/// WP-CRM-SYNC-S0B B0a (F8-b, CT decision D-1) — MOD-0168 §21 "commercial.pre-order-settings.updated — Before/After: every
/// field": one row per successful settings save — who (the caller's user id, the source of the order events), when, and
/// the fields whose value changed (old → new). Append only: no update, no delete (the repository has neither). The
/// MOD-0156 <see cref="PricingChangeLog"/> pattern (AC-HIST-1) in MOD-0168's own collection.
/// </summary>
public sealed class PreOrderSettingsChange : EntityBase
{
    /// <summary>The settings document's <c>Version</c> after this save.</summary>
    public int SettingsVersion { get; set; }

    public string? ByUserId { get; set; }
    public DateTimeOffset At { get; set; }

    /// <summary>Only the fields that changed; empty when the save changed nothing.</summary>
    public List<PreOrderSettingsFieldChange> Changes { get; set; } = new();
}

public sealed class PreOrderSettingsFieldChange
{
    public string Field { get; set; } = string.Empty;
    public string? Old { get; set; }
    public string? New { get; set; }
}

public static class PreOrderSettingsDefaults
{
    public const int ApprovalLinkTtlHours = 48;
    public const int ApprovalLinkTtlMin = 12;
    public const int ApprovalLinkTtlMax = 168;
    public const int TransmissionRetryMax = 5;
    public const int TransmissionRetryLimit = 10;

    /// <summary>D11: the pharmacist purchase tier unless the country says otherwise.</summary>
    public const string DefaultTier = PriceTiers.PharmacistPurchase;
}
