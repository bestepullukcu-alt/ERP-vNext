using Diten.Platform.Domain.Enums.DocumentManagement;
using A = Diten.Platform.Domain.Enums.DocumentManagement.DocumentAccessMatrixAction;

namespace Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates;

/// <summary>
/// MOD-0029-FU05 — pure, deterministic mapping from a register <c>access_profile</c> (plus GQMS status-folder
/// context) to a set of <see cref="TemplatePolicySpec"/>. No I/O, no tenant, no persistence. "General users get no
/// default grant" is expressed simply as the absence of an Allow rule — the resolver default-denies, so read-only /
/// restricted outcomes need no explicit broad principal.
/// </summary>
public static class AccessProfileTemplateCatalog
{
    public const string GqmsStatusFolderType = "GQMS Status Folder";

    // Status folder names that flip a controlled folder to read-only (no in-place edits, even for QA).
    private static readonly string[] ReadOnlyStatusFolders = ["Effective", "Superseded_Retired"];
    private static readonly string[] RestrictedStatusFolders = ["In_Review", "Approved_Pending_Effective"];

    // Write actions denied on read-only status folders (View/Download/ManageAccess/Archive are retained).
    private static readonly DocumentAccessMatrixAction[] WriteActions =
        [A.CreateDocument, A.CreateTemplate, A.EditMetadata, A.UploadVersion, A.Publish, A.Share];

    private static readonly DocumentAccessMatrixAction[] RestrictedWriteActions =
        [A.CreateDocument, A.CreateTemplate, A.UploadVersion, A.Publish];

    /// <summary>All access profiles this catalog understands (used to flag unknown profiles).</summary>
    public static readonly IReadOnlyList<string> KnownProfiles =
    [
        "Enterprise-Restricted", "GQMS-Controlled", "Business-Controlled", "Country-Controlled",
        "Archive-Restricted", "Confidential", "Controlled-Where-Regulated", "Site-Controlled"
    ];

    public static bool IsKnown(string? accessProfile) =>
        accessProfile is not null && KnownProfiles.Any(p => p.Equals(accessProfile.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns the template rules for a node. <paramref name="known"/> is false for an unrecognized profile, in which
    /// case NO specs are produced (fail-safe: unknown profile never gets a fallback grant).
    /// </summary>
    public static IReadOnlyList<TemplatePolicySpec> Build(
        string? accessProfile,
        string? folderType,
        string? folderName,
        bool applyStatusRules,
        out bool known)
    {
        known = IsKnown(accessProfile);
        if (!known)
        {
            return [];
        }

        var specs = BaseSpecs(accessProfile!.Trim());

        if (applyStatusRules && IsStatusFolder(folderType))
        {
            specs = ApplyStatusFolderRules(specs, folderName);
        }

        return specs;
    }

    public static bool IsStatusFolder(string? folderType) =>
        !string.IsNullOrWhiteSpace(folderType)
        && folderType.Trim().Equals(GqmsStatusFolderType, StringComparison.OrdinalIgnoreCase);

    private static List<TemplatePolicySpec> BaseSpecs(string profile) => profile.ToLowerInvariant() switch
    {
        "gqms-controlled" =>
        [
            Allow(LogicalTemplateRole.QaDocumentation, A.View, A.Download, A.CreateDocument, A.CreateTemplate, A.EditMetadata, A.UploadVersion, A.Publish, A.Archive, A.Share, A.ManageAccess),
            Allow(LogicalTemplateRole.Gqd, A.View, A.Download, A.CreateDocument, A.CreateTemplate, A.EditMetadata, A.UploadVersion, A.Publish, A.Archive, A.Share, A.ManageAccess),
            Allow(LogicalTemplateRole.LocalQa, A.View, A.Download, A.CreateDocument, A.EditMetadata, A.UploadVersion)
        ],
        "enterprise-restricted" =>
        [
            Allow(LogicalTemplateRole.DepartmentOwner, A.View, A.Download, A.CreateDocument, A.EditMetadata, A.UploadVersion)
        ],
        "business-controlled" =>
        [
            Allow(LogicalTemplateRole.DepartmentOwner, A.View, A.Download, A.CreateDocument, A.EditMetadata, A.UploadVersion)
        ],
        "country-controlled" =>
        [
            Allow(LogicalTemplateRole.CountryManager, A.View, A.Download, A.CreateDocument, A.EditMetadata, A.UploadVersion),
            Allow(LogicalTemplateRole.LocalQa, A.View, A.Download, A.EditMetadata, A.UploadVersion)
        ],
        "archive-restricted" =>
        [
            Allow(LogicalTemplateRole.RecordsOwner, A.View, A.Download),
            Allow(LogicalTemplateRole.QaDocumentation, A.View, A.Download)
        ],
        "confidential" =>
        [
            Allow(LogicalTemplateRole.Hr, A.View, A.Download, A.CreateDocument, A.EditMetadata, A.UploadVersion),
            Allow(LogicalTemplateRole.Legal, A.View, A.Download)
        ],
        "controlled-where-regulated" =>
        [
            Allow(LogicalTemplateRole.Gqd, A.View, A.Download, A.EditMetadata, A.UploadVersion),
            Allow(LogicalTemplateRole.OwnerFunction, A.View, A.Download)
        ],
        "site-controlled" =>
        [
            Allow(LogicalTemplateRole.SiteHead, A.View, A.Download, A.CreateDocument, A.EditMetadata, A.UploadVersion),
            Allow(LogicalTemplateRole.SiteQa, A.View, A.Download, A.CreateDocument, A.EditMetadata, A.UploadVersion),
            Allow(LogicalTemplateRole.Gqd, A.View, A.Download)
        ],
        _ => []
    };

    /// <summary>
    /// GQMS status folder model: Effective / Superseded_Retired become read-only (write actions Denied for the edit
    /// roles, even QA); In_Review / Approved_Pending_Effective restrict create/upload/publish; Draft stays writable.
    /// Deny rules sit on the same target as the base Allow, so deny-precedence removes exactly the write actions.
    /// </summary>
    private static List<TemplatePolicySpec> ApplyStatusFolderRules(List<TemplatePolicySpec> baseSpecs, string? folderName)
    {
        var name = folderName?.Trim() ?? string.Empty;
        var editRoles = baseSpecs
            .Where(s => s.Effect == DocumentAccessEffect.Allow && s.Actions.Any(WriteActions.Contains))
            .Select(s => s.Role)
            .Distinct()
            .ToList();

        if (ReadOnlyStatusFolders.Any(f => f.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            baseSpecs.AddRange(editRoles.Select(r => Deny(r, WriteActions)));
        }
        else if (RestrictedStatusFolders.Any(f => f.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            baseSpecs.AddRange(editRoles.Select(r => Deny(r, RestrictedWriteActions)));
        }
        // "Draft" (and any other status folder name) keeps the base writable template.

        return baseSpecs;
    }

    private static TemplatePolicySpec Allow(LogicalTemplateRole role, params DocumentAccessMatrixAction[] actions) =>
        new(role, actions, DocumentAccessEffect.Allow);

    private static TemplatePolicySpec Deny(LogicalTemplateRole role, IReadOnlyList<DocumentAccessMatrixAction> actions) =>
        new(role, actions, DocumentAccessEffect.Deny);
}
