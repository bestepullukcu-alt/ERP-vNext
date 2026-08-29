namespace Diten.Web.Models.CRM;

/// <summary>
/// MOD-0150 FU07 — contact availability / visit preference view models. Availability is always read and written in
/// the AccountContactLink (location) context; there is no contact-level availability field anywhere in this model.
/// These surfaces show WHEN someone can be visited at a location — never who to visit, in what order (MOD-0155).
/// </summary>
public sealed class ContactAvailabilityPageViewModel
{
    public Guid ContactId { get; set; }
    public string ContactDisplayName { get; set; } = string.Empty;
    public bool CanManage { get; set; }
    public List<LinkAvailabilityViewModel> Links { get; set; } = [];

    /// <summary>Optional "check a date" result (read-only lookup preview of the readiness seam).</summary>
    public ContactAvailabilityLookupViewModel? Lookup { get; set; }

    public string? LookupDate { get; set; }

    /// <summary>MOD-0048 published values for the availability type / source dropdowns (never a local list).</summary>
    public List<ReferenceOptionViewModel> AvailabilityTypes { get; set; } = [];

    public List<ReferenceOptionViewModel> AvailabilitySources { get; set; } = [];
}

public sealed class LinkAvailabilityViewModel
{
    public Guid AccountContactLinkId { get; set; }
    public Guid ContactId { get; set; }
    public string? ContactDisplayName { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountDisplayName { get; set; }
    public string? AccountCode { get; set; }
    public string? RoleCode { get; set; }
    public bool IsPrimary { get; set; }
    public bool LinkIsActive { get; set; }
    public List<ContactAvailabilityRowViewModel> Availability { get; set; } = [];
    public List<ContactAvailabilityExceptionRowViewModel> Exceptions { get; set; } = [];
}

public sealed class ContactAvailabilityRowViewModel
{
    public Guid Id { get; set; }
    public Guid AccountContactLinkId { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountDisplayName { get; set; }
    public string Weekday { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public VisitPreferenceViewModel Preference { get; set; } = new();
    public bool AppointmentRequired { get; set; }
    public int? AppointmentLeadTimeDays { get; set; }
    public int? AverageVisitDurationMinutes { get; set; }
    public string AvailabilityType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? Notes { get; set; }
}

public sealed class VisitPreferenceViewModel
{
    public int? PreferredVisitDurationMinutes { get; set; }
    public string? PreferredVisitStartTime { get; set; }
    public string? PreferredVisitEndTime { get; set; }
    public string? AvoidVisitStartTime { get; set; }
    public string? AvoidVisitEndTime { get; set; }
    public bool AppointmentRequired { get; set; }
    public int? AppointmentLeadTimeDays { get; set; }
    public string? PreferredContactMethod { get; set; }
    public string? Notes { get; set; }
}

public sealed class ContactAvailabilityExceptionRowViewModel
{
    public Guid Id { get; set; }
    public Guid AccountContactLinkId { get; set; }
    public string Date { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class ContactAvailabilityLookupViewModel
{
    public string Date { get; set; } = string.Empty;
    public string Weekday { get; set; } = string.Empty;
    public List<ContactAvailabilityLookupRowViewModel> Rows { get; set; } = [];
}

public sealed class ContactAvailabilityLookupRowViewModel
{
    public Guid AccountContactLinkId { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountDisplayName { get; set; }
    public string Weekday { get; set; } = string.Empty;
    public string? AvailableWindow { get; set; }
    public string? PreferredWindow { get; set; }
    public string? AvoidWindow { get; set; }
    public bool AppointmentRequired { get; set; }
    public int? AppointmentLeadTimeDays { get; set; }
    public int? AverageVisitDurationMinutes { get; set; }
    public string AvailabilityStatus { get; set; } = string.Empty;
    public bool ExceptionApplied { get; set; }
    public string? ExceptionReason { get; set; }
    public List<string> ReasonCodes { get; set; } = [];
}

/// <summary>Create/update form payload. The link (and therefore the contact/account pair) comes from the route.</summary>
public sealed class ContactAvailabilityFormViewModel
{
    public Guid AccountContactLinkId { get; set; }
    public string Weekday { get; set; } = "monday";
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string AvailabilityType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? PreferredVisitStartTime { get; set; }
    public string? PreferredVisitEndTime { get; set; }
    public string? AvoidVisitStartTime { get; set; }
    public string? AvoidVisitEndTime { get; set; }
    public bool AppointmentRequired { get; set; }
    public int? AppointmentLeadTimeDays { get; set; }
    public int? AverageVisitDurationMinutes { get; set; }
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Date-specific exception form payload (leave, congress, surgery day, temporary relocation).</summary>
public sealed class ContactAvailabilityExceptionFormViewModel
{
    public Guid AccountContactLinkId { get; set; }
    public string Date { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}
