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
