using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029 (Faz 2a) — document-centric variant link. Records that one controlled document (the variant) is a
/// translation / site adoption of another (the parent), together with the locale metadata and the content-change
/// evidence (a variant's content MUST differ from its parent). This is the anchor the Faz 2b localization governance
/// aggregate attaches to; it is deliberately independent of the FU03 <c>TemplateVariant</c> model. Never hard-deleted.
/// </summary>
public sealed class DocumentVariant : TenantScopedEntity
{
    /// <summary>The register entry of the variant controlled document.</summary>
    public required Guid VariantRegisterEntryId { get; set; }
    public Guid? VariantControlledDocumentId { get; set; }

    /// <summary>The register entry of the parent controlled document this variant is derived from.</summary>
    public required Guid ParentRegisterEntryId { get; set; }
    public Guid? ParentControlledDocumentId { get; set; }

    public DocumentVariantType VariantType { get; set; }

    public string? LanguageCode { get; set; }
    public string? CountryCode { get; set; }
    public string? SiteCode { get; set; }

    /// <summary>Checksums proving the variant content differs from the parent (SOP: a variant must change something).</summary>
    public string? ParentChecksum { get; set; }
    public string? VariantChecksum { get; set; }
    public bool ContentChangeVerified { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
