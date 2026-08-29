namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0150 Contact master (person who works at / relates to Accounts — doctor, pharmacist, responsible person,
/// decision-maker, …). Standalone CRM master; links to Accounts are owned by AccountContactLink (FU03, NOT here).
/// ContactType/Status are MOD-0048 published value codes. No Account fields, no Zone/Territory — those are other modules.
/// </summary>
public sealed class Contact : EntityBase
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>Optional on input; auto-derived from "FirstName LastName" when blank; always populated after persistence.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>MOD-0048 published contact-type value code (required).</summary>
    public string ContactType { get; set; } = string.Empty;

    /// <summary>MOD-0048 published contact-status value code (required).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>MOD-0048 published gender value code (optional). Personal data — see PII/KVKK handling.</summary>
    public string? Gender { get; set; }

    public string? ProfessionalTitle { get; set; }
    public string? Specialty { get; set; }
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }

    /// <summary>Optional contact avatar as a small base64 data-URI ("data:image/…"). PERSONAL DATA — intentionally
    /// EXCLUDED from list projections, export and audit (only flows through create/update → detail/overview). Kept small
    /// by client-side resize. A dedicated blob store is a future optimization; never write this raw to audit/log/export.</summary>
    public string? PhotoDataUri { get; set; }

    // MOD-0150 Contact Location Hardening (2026-07-21). Minimal, optional location seam so a Contact can carry its own
    // country/coverage context for cross-country link/relationship checks and future MOD-0151 Territory / MOD-0155 Visit
    // availability. Country is NOT required (an incomplete-location Contact is allowed). These are MOD-0048 published
    // location codes (country/city/district), NOT a new reference set and NOT Account master — Account still owns its own
    // location, and Zone/MicroZone/Territory/SalesRep remain forbidden here (that is MOD-0151).
    public string? CountryRef { get; set; }
    public string? CityRef { get; set; }
    public string? DistrictRef { get; set; }
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>Optional preferred language (BCP-47-ish code, e.g. "tr", "en"); UI/communication hint only.</summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>Optional dialing country code (e.g. "+90"); paired with Phone for normalization.</summary>
    public string? PhoneCountryCode { get; set; }
}
