using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CaseIntakeTriage;

/// <summary>
/// MOD-0230 case intake form (Create / Edit). One property per form input; a red-asterisk field is [Required], an
/// optional date is nullable. The browser posts the fields as a form to the same-origin proxy, which sends the record
/// version (<see cref="ExpectedVersion"/>) back to the API on update.
/// </summary>
public sealed class PvgCaseIntakeFormViewModel
{
    public string? IntakeDraftId { get; set; }

    public long? ExpectedVersion { get; set; }

    [Required]
    public string? IntakeChannel { get; set; }

    [Required]
    public string? SourceType { get; set; }

    [Required]
    public string? SourceReference { get; set; }

    [Required]
    public DateTime? ReceivedAtUtc { get; set; }

    [Required]
    public string? ReporterType { get; set; }

    public string? ReporterContactSummary { get; set; }

    [Required]
    public string? PatientSubjectCode { get; set; }

    public DateOnly? EventOnsetDate { get; set; }

    [Required]
    public string? AdverseEventNarrative { get; set; }

    public List<PvgCaseIntakeProductLineViewModel> ProductLines { get; set; } = [];

    [Required]
    public string? Seriousness { get; set; }

    [Required]
    public string? IntakePriority { get; set; }

    public string? EvidenceLinkReferencesRaw { get; set; }

    [Required]
    public DateTime? AwarenessAtUtc { get; set; }

    [Required]
    public string? EventCountry { get; set; }

    [Required]
    public string? ReportType { get; set; }

    public string? ReporterNameOrInitials { get; set; }

    public string? ReporterProfession { get; set; }

    public string? ReporterCountry { get; set; }
}

/// <summary>One line of the product list: the role and the name are required on every line, the lot is optional.</summary>
public sealed class PvgCaseIntakeProductLineViewModel
{
    [Required]
    public string? ProductRole { get; set; }

    [Required]
    public string? ProductNameAsReported { get; set; }

    public string? LotNumber { get; set; }
}
