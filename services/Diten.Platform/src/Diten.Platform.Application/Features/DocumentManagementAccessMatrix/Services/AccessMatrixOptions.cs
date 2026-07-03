namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;

/// <summary>
/// MOD-0029-FU04 — rollout controls for the access matrix. The secure target model for normal users is default
/// deny, but flipping immediately would break existing UX. In <see cref="EnforcementMode.Compatibility"/> (default)
/// the matrix resolver is fully functional and previewable, but the existing FU01 DocumentAccessEvaluator /
/// FolderDocumentAccessPolicy behavior stays authoritative — the matrix does not tighten access. Switching to
/// <see cref="EnforcementMode.Enforce"/> makes the matrix authoritative with default-deny for normal users.
/// </summary>
public sealed class AccessMatrixOptions
{
    public const string SectionName = "DocumentManagement:AccessMatrix";

    public AccessMatrixEnforcementMode Mode { get; set; } = AccessMatrixEnforcementMode.Compatibility;

    /// <summary>Owner-company members keep transitional View during Compatibility rollout even with no explicit policy.</summary>
    public bool OwnerCompanyTransitionalView { get; set; } = true;
}

public enum AccessMatrixEnforcementMode
{
    /// <summary>Matrix computed & previewable; existing FU01 behavior authoritative; matrix never tightens access.</summary>
    Compatibility = 0,

    /// <summary>Matrix authoritative; default deny for normal users.</summary>
    Enforce = 1
}
