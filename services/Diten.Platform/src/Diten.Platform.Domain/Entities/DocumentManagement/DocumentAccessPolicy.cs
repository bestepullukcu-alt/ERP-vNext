using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — embedded Layer 2 resource access policy on a <see cref="ControlledDocument"/> /
/// <see cref="TemplateDocument"/>. Source indicates whether the effective access is folder-inherited or
/// document-explicit. An override may only narrow / make explicit; it can never widen across tenant/company
/// isolation. First version may carry ROLE/COMPANY-level grants only.
/// </summary>
public sealed class DocumentAccessPolicy
{
    public AccessPolicySource Source { get; set; } = AccessPolicySource.Inherited;
    public List<DocumentAccessGrant> Grants { get; set; } = [];
}

public sealed class DocumentAccessGrant
{
    public DocumentAccessAction Action { get; set; }
    public AccessTargetType TargetType { get; set; }

    /// <summary>Typed grantee value (e.g. a user/role/company GUID, or position/group id later).</summary>
    public required string TargetId { get; set; }
}
