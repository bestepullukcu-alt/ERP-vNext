namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU04 — generalized resource access matrix enums (sidecar policy model). Kept in a dedicated file so
// MOD-0029-FU04 ownership never edits the FU01 ControlledDocumentEnums.cs surface.
//
// Naming note: the action enum is named `DocumentAccessMatrixAction` (not `DocumentAccessAction`) on purpose — the
// FU01 `DocumentAccessAction` already exists with a narrower 6-value set and is consumed by DocumentAccessEvaluator.
// Reusing that name would collide; the compatibility adapter maps FU01 FolderPermissionSet → this richer action set.

/// <summary>MOD-0029-FU04 — resource the access policy targets.</summary>
public enum DocumentAccessTargetType
{
    Tenant = 0,
    Company = 1,
    CollectionDefinition = 2,
    CollectionInstance = 3,
    TemplateDocument = 4,
    ControlledDocument = 5,
    TemplateMaster = 6,
    TemplateVariant = 7
}

/// <summary>MOD-0029-FU04 — principal the access policy grants/denies. `Group` is a placeholder until a group source exists.</summary>
public enum DocumentAccessPrincipalType
{
    User = 0,
    Role = 1,
    Group = 2,
    Company = 3
}

/// <summary>
/// MOD-0029-FU04 — matrix action set. Approval-family actions (RequestApproval/Approve/Reject/Review) are INERT
/// placeholders in this FU: they may be stored on a policy but drive no approval workflow, queue, or endpoint.
/// </summary>
public enum DocumentAccessMatrixAction
{
    View = 0,
    Download = 1,
    CreateDocument = 2,
    CreateTemplate = 3,
    EditMetadata = 4,
    UploadVersion = 5,
    Publish = 6,
    Archive = 7,
    Share = 8,
    ManageAccess = 9,
    // Inert approval placeholders — no runtime effect in this FU.
    RequestApproval = 10,
    Approve = 11,
    Reject = 12,
    Review = 13
}

public enum DocumentAccessEffect
{
    Allow = 0,
    Deny = 1
}

public enum DocumentAccessPolicyStatus
{
    Active = 0,
    Disabled = 1,
    Archived = 2
}

/// <summary>
/// MOD-0029-FU05 — provenance of an access policy row. Legacy/manually authored rows have no stored value and thus
/// read as <see cref="Manual"/> (0), so existing policies are never mistaken for generated ones and are never
/// overwritten by the access-profile template engine.
/// </summary>
public enum DocumentAccessPolicySource
{
    Manual = 0,
    AccessProfileTemplate = 1,
    SystemGenerated = 2
}
