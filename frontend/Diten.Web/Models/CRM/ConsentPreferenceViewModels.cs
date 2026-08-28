using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// MOD-0164-FU03 — Consent & Preference Admin UI view models. UI-only consumer of the FU02 (Diten.CrmService) contract.
// A TenantId is NEVER modeled here: it is server-resolved from the JWT claim and never accepted on a payload.
// The question dimensions (SubjectType/SubjectId, Channel, Purpose, ScopeType/ScopeId; PreferenceType for preferences)
// are immutable after create and are rendered read-only on Edit.

// ---------------- Consent ----------------

public sealed class ConsentEditViewModel : IValidatableObject
{
    public Guid? ConsentId { get; set; }

    // Immutable question dimensions (read-only on edit).
    [Required] public string SubjectType { get; set; } = string.Empty;
    [Required] public Guid? SubjectId { get; set; }
    [Required] public string Channel { get; set; } = string.Empty;
    [Required] public string Purpose { get; set; } = string.Empty;
    public string? ScopeType { get; set; }
    public Guid? ScopeId { get; set; }

    // Editable answer dimensions.
    [Required] public string LegalBasis { get; set; } = string.Empty;
    [Required] public string ConsentStatus { get; set; } = string.Empty;
    [Required] public string Source { get; set; } = string.Empty;
    [Required] public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? WithdrawalReason { get; set; }
    [StringLength(4000)] public string? Notes { get; set; }

    // Evidence pointer only — MOD-0164 never renders file content/URL.
    public string? EvidenceRefType { get; set; }
    public Guid? EvidenceRefId { get; set; }
    public string? EvidenceSourceModule { get; set; }
    public string? EvidenceRefCode { get; set; }

    public List<ConsentExternalReferenceViewModel> ExternalReferences { get; set; } = [];

    // Contract-supplied vocabulary (populated by the controller from GET /consents/contract).
    public IReadOnlyList<string> SubjectTypes { get; set; } = [];
    public IReadOnlyList<string> Channels { get; set; } = [];
    public IReadOnlyList<string> Purposes { get; set; } = [];
    public IReadOnlyList<string> ScopeTypes { get; set; } = [];
    public IReadOnlyList<string> LegalBases { get; set; } = [];
    public IReadOnlyList<string> ConsentStatuses { get; set; } = [];
    public IReadOnlyList<string> Sources { get; set; } = [];
    public IReadOnlyList<string> EvidenceRefTypes { get; set; } = [];
    public IReadOnlyList<string> EvidenceSourceModules { get; set; } = [];
    public string? ContractError { get; set; }
    public bool IsArchived { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveFrom.HasValue && EffectiveTo.HasValue && EffectiveTo < EffectiveFrom)
        {
            yield return new ValidationResult("EffectiveToBeforeFrom", [nameof(EffectiveTo)]);
        }
        if (ScopeId.HasValue && string.IsNullOrWhiteSpace(ScopeType))
        {
            yield return new ValidationResult("ScopeTypeRequiredWithScopeId", [nameof(ScopeType)]);
        }
    }
}

public sealed class ConsentExternalReferenceViewModel
{
    [StringLength(120)] public string SourceSystem { get; set; } = string.Empty;
    [StringLength(240)] public string ExternalId { get; set; } = string.Empty;
    [StringLength(240)] public string? ExternalCode { get; set; }
    [StringLength(400)] public string? ExternalName { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class ConsentEvidenceRefViewModel
{
    public string RefType { get; set; } = string.Empty;
    public Guid RefId { get; set; }
    public string SourceModule { get; set; } = string.Empty;
    public string? RefCode { get; set; }
}

public sealed class ConsentDetailViewModel
{
    public Guid ConsentId { get; set; }
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string? ScopeType { get; set; }
    public Guid? ScopeId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public string ConsentStatus { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string Source { get; set; } = string.Empty;
    public ConsentEvidenceRefViewModel? EvidenceRef { get; set; }
    public string? WithdrawalReason { get; set; }
    public string? Notes { get; set; }
    public List<ConsentExternalReferenceViewModel> ExternalReferences { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public bool IsArchived { get; set; }
    // Best-effort display names resolved from the audit user GUIDs (null → view falls back to the raw GUID).
    public string? CreatedByName { get; set; }
    public string? UpdatedByName { get; set; }
    public string? ArchivedByName { get; set; }
}

// Read-only embed of a subject's consent & preference records on another module's detail page (e.g. Account/Contact
// 360). Carries only the subject coordinates; the records are fetched client-side from the FU03 proxy endpoints.
public sealed class ConsentPreferenceEmbedViewModel
{
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
}

public sealed class ConsentDetailPageViewModel
{
    public ConsentDetailViewModel Consent { get; set; } = new();
    public ConsentPreferenceContractViewModel Contract { get; set; } = new();
    public bool CanManage { get; set; }
    public bool CanEvaluate { get; set; }
}

// ---------------- Preference ----------------
// NOTE: FU02 PreferenceRecordDto has NO ScopeType/ScopeId and NO stored IsRestrictive field. The "restrictive" concept
// is derived at evaluation time (CandidatePreference.Restrictive). Modeling scope/isRestrictive here would be fake, so
// they are intentionally absent (documented limitation in the evidence report).

public sealed class PreferenceEditViewModel : IValidatableObject
{
    public Guid? PreferenceId { get; set; }

    // Immutable question dimensions (read-only on edit).
    [Required] public string SubjectType { get; set; } = string.Empty;
    [Required] public Guid? SubjectId { get; set; }
    [Required] public string Channel { get; set; } = string.Empty;
    [Required] public string PreferenceType { get; set; } = string.Empty;

    // Best-effort resolved display name for SubjectId (contact/account) — used to seed the read-only picker on edit.
    public string? SubjectName { get; set; }

    // Editable dimensions.
    [Required] public string PreferenceValue { get; set; } = string.Empty;
    [Required, Range(1, int.MaxValue)] public int Priority { get; set; } = 1;
    [Required] public string Source { get; set; } = string.Empty;
    [Required] public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    [StringLength(4000)] public string? Notes { get; set; }

    public List<ConsentExternalReferenceViewModel> ExternalReferences { get; set; } = [];

    public IReadOnlyList<string> SubjectTypes { get; set; } = [];
    public IReadOnlyList<string> PreferenceChannels { get; set; } = [];
    public IReadOnlyList<string> PreferenceTypes { get; set; } = [];
    public IReadOnlyList<string> Sources { get; set; } = [];
    public string? ContractError { get; set; }
    public bool IsArchived { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveFrom.HasValue && EffectiveTo.HasValue && EffectiveTo < EffectiveFrom)
        {
            yield return new ValidationResult("EffectiveToBeforeFrom", [nameof(EffectiveTo)]);
        }
    }
}

public sealed class PreferenceDetailViewModel
{
    public Guid PreferenceId { get; set; }
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string PreferenceType { get; set; } = string.Empty;
    public string PreferenceValue { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<ConsentExternalReferenceViewModel> ExternalReferences { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public bool IsArchived { get; set; }
    // Best-effort display names resolved from the audit user GUIDs (null when resolution is not permitted/available;
    // the view falls back to the raw GUID).
    public string? CreatedByName { get; set; }
    public string? UpdatedByName { get; set; }
    public string? ArchivedByName { get; set; }
}

public sealed class PreferenceDetailPageViewModel
{
    public PreferenceDetailViewModel Preference { get; set; } = new();
    public ConsentPreferenceContractViewModel Contract { get; set; } = new();
    public bool CanManage { get; set; }
}

// ---------------- Shell / Contract ----------------

public sealed class ConsentPreferenceIndexViewModel
{
    public ConsentPreferenceContractViewModel Contract { get; set; } = new();
    public bool CanReadConsent { get; set; }
    public bool CanManageConsent { get; set; }
    public bool CanEvaluate { get; set; }
    public bool CanReadPreference { get; set; }
    public bool CanManagePreference { get; set; }
    public string ActiveTab { get; set; } = "consents";
}

public sealed class ConsentPreferenceContractViewModel
{
    public string ModuleId { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public ConsentFeatureFlagsViewModel Features { get; set; } = new();
    public ConsentVocabularyViewModel Vocabulary { get; set; } = new();
    public ConsentEvaluationVocabularyViewModel EvaluationVocabulary { get; set; } = new();
    public List<string> Permissions { get; set; } = [];
    public List<string> Limitations { get; set; } = [];
}

public sealed class ConsentFeatureFlagsViewModel
{
    public bool SupportsConsentManagement { get; set; }
    public bool SupportsPreferenceManagement { get; set; }
    public bool SupportsConsentEvaluation { get; set; }
    public bool SupportsConsentPurposeChannelScope { get; set; }
    public bool SupportsConsentEvidenceReference { get; set; }
    public bool SupportsConsentFilterProvider { get; set; }
}

public sealed class ConsentVocabularyViewModel
{
    public List<string> SubjectTypes { get; set; } = [];
    public List<string> Channels { get; set; } = [];
    public List<string> PreferenceChannels { get; set; } = [];
    public List<string> Purposes { get; set; } = [];
    public List<string> LegalBases { get; set; } = [];
    public List<string> ConsentStatuses { get; set; } = [];
    public List<string> ScopeTypes { get; set; } = [];
    public List<string> Sources { get; set; } = [];
    public List<string> PreferenceTypes { get; set; } = [];
    public List<string> EvidenceRefTypes { get; set; } = [];
    public List<string> EvidenceSourceModules { get; set; } = [];
}

public sealed class ConsentEvaluationVocabularyViewModel
{
    public List<string> EligibilityStatuses { get; set; } = [];
    public List<string> Decisions { get; set; } = [];
    public string EvaluatorVersion { get; set; } = string.Empty;
}

public sealed class ConsentPreferenceGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
