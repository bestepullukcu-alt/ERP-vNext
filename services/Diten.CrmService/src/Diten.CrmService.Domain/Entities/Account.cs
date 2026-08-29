namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0149 Account / Customer / WorkPlace master (CRM view). Location foundation only:
/// address + geo are persisted here; coverage assignment (Zone/MicroZone/Territory) is owned by
/// MOD-0151 and is NEVER persisted on this entity (boundary rule §3.1). No ZoneId/MicroZoneId/TerritoryId/SalesRepId.
/// </summary>
public sealed class Account : EntityBase
{
    public string AccountName { get; set; } = string.Empty;

    /// <summary>CRM's own human-readable identifier. Optional on input; auto-generated (ACC-{YYYY}-{sequence}) when blank; always populated after persistence. Unique per (TenantId, AccountCode).</summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>MOD-0048 published account-type value code (required).</summary>
    public string AccountType { get; set; } = string.Empty;

    /// <summary>MOD-0048 published account-category value code (optional).</summary>
    public string? AccountCategory { get; set; }

    /// <summary>Self-referencing hierarchy parent (tenant-scoped). Cycles forbidden.</summary>
    public Guid? ParentAccountId { get; set; }

    /// <summary>MOD-0048 published account-status value code (required).</summary>
    public string Status { get; set; } = string.Empty;

    public string? CountryRef { get; set; }
    public string? CityRef { get; set; }
    public string? DistrictRef { get; set; }
    public string? AddressLine { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string? ResponsiblePersonName { get; set; }
    public string? ResponsiblePersonPhone { get; set; }
    public string? ResponsiblePersonEmail { get; set; }

    public string? Notes { get; set; }

    /// <summary>Optional account logo, stored inline as a base64 data URI (e.g. <c>data:image/png;base64,...</c>).
    /// Kept small on the frontend (single logo, ~256&#160;KB cap); never an external file reference.</summary>
    public string? LogoDataUri { get; set; }
}
