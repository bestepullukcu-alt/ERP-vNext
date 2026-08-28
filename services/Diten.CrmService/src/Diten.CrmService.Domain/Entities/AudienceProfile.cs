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

    /// <summary>Optional <see cref="AudienceProfileTypes"/> classification (person-shaped kind of audience).</summary>
    public string? ProfileType { get; set; }

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
