using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — embedded value object describing how a document/template may be accessed/used/copied when
/// shared. COMPANY visibility targets a MOD-0220 LegalEntity GUID.
/// </summary>
public sealed class DocumentSharePolicy
{
    public DocumentShareMode ShareMode { get; set; } = DocumentShareMode.Reference;
    public bool CanUse { get; set; } = true;
    public bool CanCopy { get; set; }
    public ShareVisibilityScope VisibilityScope { get; set; } = ShareVisibilityScope.Company;
    public bool SourceVisibleOnUpdate { get; set; } = true;
}

/// <summary>
/// MOD-0029-FU01 — template behaviour flags. A non-shareable / reference-only template cannot be
/// <c>COPY_ON_ADOPT</c>/shared (controlled VALIDATION_FAILED).
/// </summary>
public sealed class TemplateFlags
{
    public bool Reusable { get; set; } = true;
    public bool Shareable { get; set; } = true;
    public bool CopyableOnAdopt { get; set; }
    public bool ReferenceOnly { get; set; }
}
