namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0162 FU02 — AudienceProfile. A GENERIC target profile (cardiology A-segment doctor · pharmacist · new employee ·
/// A1 German learner · medical representative · manager · …). It is deliberately one object for two contexts: a doctor
/// profile in pharma and a learner profile in training are the same aggregate. A <c>DoctorProfile</c> is NOT a separate
/// entity. Profile ↔ contact/segment/persona mapping is NOT written in FU02 (consumer boundary). Closing a profile is
/// the soft <see cref="ArchivedAt"/> lifecycle; there is no hard delete, and an archived profile accepts no new content.
/// </summary>
public sealed class AudienceProfile : EntityBase
{
    /// <summary>Stable business key, unique per tenant among non-archived rows. Never renamed.</summary>
    public string ProfileCode { get; set; } = string.Empty;

    public string ProfileName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>SCMM-11 (AUD) — optional owning subject (MOD-0162 FU02). NULLABLE and additive: a legacy profile keeps
    /// <c>SubjectId == null</c> and stays tenant-global; a new profile is subject-scoped. Making it required would break
    /// every existing profile, so the migration stance is nullable + optional later backfill (DEC-SCMM-03 AUD).</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>Optional <see cref="AudienceProfileTypes"/> classification (person-shaped kind of audience). RETAINED for
    /// backward compatibility — the single-axis legacy view. New profiles express their targeting through
    /// <see cref="Dimensions"/>; this field is never removed.</summary>
    public string? ProfileType { get; set; }

    /// <summary>SCMM-11 (AUD, RM3) — the multi-axis targeting model: a profile carries values on several open axes
    /// (specialty · seniority · setting · channel · …). The axis names are CONFIG-DRIVEN strings, never a hardcoded
    /// sector-specific enum (sector-neutral). This is DATA ONLY — no eligibility / membership is computed here (D8;
    /// that is SCMM-11).</summary>
    public List<AudienceDimensionAssignment> Dimensions { get; set; } = new();

    /// <summary><see cref="TaxonomyStatuses"/> — draft / active / inactive / archived.</summary>
    public string Status { get; set; } = TaxonomyStatuses.Draft;

    public int SortOrder { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public List<string> Alias { get; set; } = new();

    public List<KnowledgeExternalReference> ExternalReferences { get; set; } = new();

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;
}

/// <summary>
/// SCMM-11 (AUD, RM3) — one axis of a profile's multi-axis targeting: an open <see cref="AxisCode"/> plus the
/// <see cref="Values"/> the profile carries on it. Embedded value object (no <c>TenantId</c>, no <c>Version</c>, no
/// repository). The axis code and values are CONFIG strings — the model is sector-neutral, so nothing medical (or any
/// other domain) is hardcoded. Purely descriptive data; no engine reads it (D8).
/// </summary>
public sealed class AudienceDimensionAssignment
{
    /// <summary>Open axis key (e.g. <c>specialty</c>, <c>seniority</c>, <c>setting</c>). Config-driven; not an enum.</summary>
    public string AxisCode { get; set; } = string.Empty;

    /// <summary>The values carried on this axis (at least one). Opaque config strings.</summary>
    public List<string> Values { get; set; } = new();
}

/// <summary>Optional kind of audience profile. In-domain (structural); optional on the aggregate, but when supplied it
/// must be a known value.</summary>
public static class AudienceProfileTypes
{
    public const string HealthcareProfessional = "healthcare-professional";
    public const string Pharmacist = "pharmacist";
    public const string Patient = "patient";
    public const string Learner = "learner";
    public const string Employee = "employee";
    public const string SalesRepresentative = "sales-representative";
    public const string Manager = "manager";
    public const string Administrator = "administrator";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        HealthcareProfessional, Pharmacist, Patient, Learner, Employee, SalesRepresentative, Manager, Administrator,
        Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}
