using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// MOD-0162-FU02 Knowledge / Content Taxonomy UI view models. TenantId is NEVER carried here — it is server-resolved.
// Reference ids (Subject/Topic/AudienceProfile/Concept/Brand/Product/Campaign/Segment) are format-level only; no master
// is resolved. The business version is ContentVersion (never "Version").

public sealed class KnowledgeContentEditViewModel : IValidatableObject
{
    public Guid? ContentId { get; set; }

    [Required, StringLength(100)]
    public string ContentCode { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string ContentTitle { get; set; } = string.Empty;

    [Required]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public string ContentStatus { get; set; } = "draft";

    [Required]
    public Guid? SubjectId { get; set; }

    public Guid? TopicId { get; set; }
    public Guid? AudienceProfileId { get; set; }
    public Guid? ConceptNodeId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? SegmentId { get; set; }

    [Required, StringLength(35)]
    public string LanguageCode { get; set; } = "en";

    [StringLength(2000)] public string? Summary { get; set; }
    [StringLength(400)] public string? ContentBodyRef { get; set; }
    [StringLength(400)] public string? ContentAssetRef { get; set; }
    [StringLength(400)] public string? FileRef { get; set; }
    [StringLength(1000)] public string? Url { get; set; }

    [Required, StringLength(60)]
    public string ContentVersion { get; set; } = "1.0";

    [Required]
    public DateTimeOffset? EffectiveFrom { get; set; }

    public DateTimeOffset? EffectiveTo { get; set; }

    [Required]
    public string Source { get; set; } = "manual";

    [StringLength(1000)] public string? Tags { get; set; }
    public List<KnowledgeExternalReferenceViewModel> ExternalReferences { get; set; } = [];

    public IReadOnlyList<string> ContentTypes { get; set; } = [];
    public IReadOnlyList<string> ContentStatuses { get; set; } = [];
    public IReadOnlyList<string> ContentSources { get; set; } = [];

    // Reference picker option lists (server-populated from the gateway). Segment still has no runtime source, so it
    // renders as a disabled select whose value is preserved through a hidden field. ConceptNode DOES have one now
    // (MOD-0162-FU03) and is a live Subject → ConceptType → ConceptNode chain — AC-UI-3.
    public IReadOnlyList<KnowledgeOptionViewModel> SubjectOptions { get; set; } = [];
    public IReadOnlyList<KnowledgeOptionViewModel> TopicOptions { get; set; } = [];
    public IReadOnlyList<KnowledgeOptionViewModel> AudienceProfileOptions { get; set; } = [];

    // ConceptType is grouped by its SubjectId, ConceptNode by its ConceptTypeId — the two groupings drive the chain.
    // Only the node id is persisted (KnowledgeContent.ConceptNodeId); the type picker is a narrowing control and is
    // never sent to the backend, so no FU02 field is added.
    public IReadOnlyList<KnowledgeOptionViewModel> ConceptTypeOptions { get; set; } = [];
    public IReadOnlyList<KnowledgeOptionViewModel> ConceptNodeOptions { get; set; } = [];
    public IReadOnlyList<KnowledgeOptionViewModel> BrandOptions { get; set; } = [];
    public IReadOnlyList<KnowledgeOptionViewModel> ProductOptions { get; set; } = [];
    public IReadOnlyList<KnowledgeOptionViewModel> CampaignOptions { get; set; } = [];
    public IReadOnlyList<KnowledgeOptionViewModel> DocumentOptions { get; set; } = [];
    public IReadOnlyList<string> LanguageOptions { get; set; } = new[] { "en", "tr", "ar", "es", "fr", "ru", "zh" };

    public string? ContractError { get; set; }
    public bool IsArchived { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveFrom.HasValue && EffectiveTo.HasValue && EffectiveTo < EffectiveFrom)
        {
            yield return new ValidationResult("EffectiveToBeforeFrom", [nameof(EffectiveTo)]);
        }

        if (string.IsNullOrWhiteSpace(ContentBodyRef) && string.IsNullOrWhiteSpace(ContentAssetRef)
            && string.IsNullOrWhiteSpace(FileRef) && string.IsNullOrWhiteSpace(Url))
        {
            yield return new ValidationResult("ContentPointerRequired",
                [nameof(ContentBodyRef), nameof(ContentAssetRef), nameof(FileRef), nameof(Url)]);
        }
    }
}

public sealed class KnowledgeOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    // Parent grouping key (for Topic = its SubjectId, enabling the Subject→Topic cascade). Empty for flat lists.
    public string Group { get; set; } = string.Empty;

    // True when this option is an archived/invalid value kept only so an existing record's saved reference stays visible.
    public bool IsInactive { get; set; }
}

public sealed class KnowledgeExternalReferenceViewModel
{
    [StringLength(120)] public string SourceSystem { get; set; } = string.Empty;
    [StringLength(240)] public string ExternalId { get; set; } = string.Empty;
    [StringLength(240)] public string? ExternalCode { get; set; }
    [StringLength(400)] public string? ExternalName { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class KnowledgeContentDetailViewModel
{
    public Guid ContentId { get; set; }
    public string ContentCode { get; set; } = string.Empty;
    public string ContentTitle { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ContentStatus { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public Guid? TopicId { get; set; }
    public Guid? AudienceProfileId { get; set; }
    public Guid? ConceptNodeId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? SegmentId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ContentBodyRef { get; set; }
    public string? ContentAssetRef { get; set; }
    public string? FileRef { get; set; }
    public string? Url { get; set; }
    public string ContentVersion { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string Source { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public List<KnowledgeExternalReferenceViewModel> ExternalReferences { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class KnowledgeContentPageViewModel
{
    public KnowledgeContentDetailViewModel Content { get; set; } = new();
    public bool CanManage { get; set; }

    // Resolved when FileRef points to a Document Management controlled document, so Details can show its title and a
    // preview link instead of a raw id. Null for legacy/free-text FileRef values.
    public KnowledgeDocumentRefViewModel? DocumentRef { get; set; }
}

public sealed class KnowledgeDocumentRefViewModel
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public bool HasFile { get; set; }
}

public sealed class KnowledgeContentListViewModel
{
    public List<KnowledgeContentDetailViewModel> Items { get; set; } = [];
    public int Total { get; set; }
}

// --- Taxonomy read models (subject / topic / audience-profile) used by the taxonomy admin page ---

public sealed class KnowledgeTaxonomyRowViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class KnowledgeContractViewModel
{
    public string ModuleId { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public KnowledgeFeatureFlagsViewModel Features { get; set; } = new();
    public KnowledgeVocabularyViewModel Vocabularies { get; set; } = new();
    public List<string> Permissions { get; set; } = [];
    public List<string> Limitations { get; set; } = [];
}

public sealed class KnowledgeFeatureFlagsViewModel
{
    public bool SupportsKnowledgeContentManagement { get; set; }
    public bool SupportsSubjectTaxonomyManagement { get; set; }
    public bool SupportsConceptGraphReference { get; set; }
    public bool SupportsBrandProductReference { get; set; }
    public bool SupportsArchiveLifecycle { get; set; }
    public bool SupportsEffectiveDating { get; set; }
    public bool SupportsContractDrivenUi { get; set; }
}

public sealed class KnowledgeVocabularyViewModel
{
    public List<string> ContentTypes { get; set; } = [];
    public List<string> ContentStatuses { get; set; } = [];
    public List<string> ContentSources { get; set; } = [];
    public List<string> AudienceProfileTypes { get; set; } = [];
    public List<string> TaxonomyStatuses { get; set; } = [];
}

public sealed class KnowledgeGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
