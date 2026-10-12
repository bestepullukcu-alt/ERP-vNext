using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// ---------------------------------------------------------------------------------------------------------------
// WP-KP-5a-UI — the Create/Edit form models of the two Regulatory master-data screens (WP-KP-5a contract). The forms
// are submitted as JSON by RegulatoryTexts/form.js through the same-origin proxy; these models carry the server-rendered
// initial values and the required contract (Razor asp-for → label star + required + data-val, which the golden verifier
// matches against these attributes). Identity (product / country / language) is fixed after create.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>Safety text (product × country × language), Compact form.</summary>
public sealed class SafetyTextEditViewModel
{
    public const int BodyMaxLength = 20000;
    public const int ShortBodyMaxLength = 2000;

    public Guid? Id { get; set; }
    public string? SafetyTextCode { get; set; }
    public int? Version { get; set; }

    [Required]
    public Guid? GlobalProductId { get; set; }

    public string? GlobalProductCodeDisplay { get; set; }

    [Required]
    public string? CountryCode { get; set; }

    [Required]
    public string? LanguageCode { get; set; }

    [Required]
    [StringLength(BodyMaxLength)]
    public string? Body { get; set; }

    [StringLength(ShortBodyMaxLength)]
    public string? ShortBody { get; set; }

    [StringLength(500)]
    public string? SourceDocumentRef { get; set; }

    public DateTime? SourceDate { get; set; }

    [StringLength(200)]
    public string? ApprovalReference { get; set; }
}

/// <summary>Country legal profile (country × language), Compact form.</summary>
public sealed class LegalProfileEditViewModel
{
    public Guid? Id { get; set; }
    public string? ProfileCode { get; set; }
    public int? Version { get; set; }

    [Required]
    public string? CountryCode { get; set; }

    [Required]
    public string? LanguageCode { get; set; }

    [Required]
    public string? LegalFooterText { get; set; }

    /// <summary>Name + address of the marketing authorisation holder (one free text).</summary>
    public string? MarketingAuthorizationHolder { get; set; }

    public string? AdverseEventReportingText { get; set; }

    public string? PromotionalNotice { get; set; }

    /// <summary>The page approval code format the page designer (KP-UI-3) stamps, e.g. <c>{CC}-{YYYY}-{SEQ}</c>.</summary>
    [StringLength(100)]
    public string? PageApprovalCodeFormat { get; set; }
}
