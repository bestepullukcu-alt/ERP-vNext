namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU18 — variant localization API request payloads (JSON from the TenantShell proxy). TenantId is never
// accepted from the client; it is server-side resolved. No payload carries document content — every evidence
// field is a REFERENCE string, never bytes, and no translated text is transmitted or stored.

public sealed class VariantLocalizationProfileApiRequest
{
    public string? VariantIdentifier { get; set; }
    public string? VariantLanguageCode { get; set; }
    public string? VariantLanguageName { get; set; }
    public string? SourceLanguageCode { get; set; }
    public string? CountryCode { get; set; }
    public string? SiteCode { get; set; }
    public bool IsTranslationVariant { get; set; }
    public bool IsSiteAdoptedVariant { get; set; }
    public bool IsLocalLanguageMandatory { get; set; }
    public Guid? ParentTemplateMasterId { get; set; }
    public Guid? ParentTemplateMasterVersionId { get; set; }
    public Guid? ParentRegisterEntryId { get; set; }
    public string? ParentDocumentUid { get; set; }
    public string? ParentDocumentCode { get; set; }
    public string? ParentVersionLabel { get; set; }

    /// <summary>Pointer to a separately-registered local document, if one exists. FU18 does not create one.</summary>
    public Guid? LocalDocumentRegisterEntryId { get; set; }

    public Guid? AuthorUserId { get; set; }
    public Guid? BilingualReviewerUserId { get; set; }
    public string? BilingualReviewerRole { get; set; }
    public Guid? LocalApproverUserId { get; set; }
    public string? LocalApproverRole { get; set; }
    public DateTimeOffset? LocalEffectiveDate { get; set; }
}

public sealed class RecordBilingualReviewApiRequest
{
    public Guid? ReviewerUserId { get; set; }
    public string? ReviewerRole { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public string? Comment { get; set; }
}

public sealed class RecordLocalApprovalApiRequest
{
    public Guid? ApproverUserId { get; set; }
    public string? ApproverRole { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public string? Comment { get; set; }
}

public sealed class RejectVariantReviewApiRequest
{
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceReference { get; set; }
}

public sealed class AllowTemporaryEnglishMasterApiRequest
{
    public string Justification { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
}

public sealed class EvaluateVariantParentChangeApiRequest
{
    public string? EvidenceReference { get; set; }
}
