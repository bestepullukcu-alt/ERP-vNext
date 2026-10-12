namespace Diten.Web.Models.CRM;

/// <summary>
/// WP-VW-W3 (W3-WEB-c) — the objection library pages' flags (list / editor / record page). <see cref="CanRead"/> false ⇒
/// the page draws ONLY the access notice (UAS-001); the editor also needs <see cref="CanManage"/>. The other flags mirror
/// the CRM endpoints' keys — what a record itself allows comes from its <c>abilities</c> (CRM stays the authority).
/// </summary>
public sealed class ObjectionResponsesPageViewModel
{
    /// <summary>crm.objection-response.read.</summary>
    public bool CanRead { get; set; }

    /// <summary>Create / edit / retire / copy: crm.objection-response.manage.</summary>
    public bool CanManage { get; set; }

    /// <summary>Submit / withdraw: crm.objection-response.submit.</summary>
    public bool CanSubmit { get; set; }

    /// <summary>The claim picker (approved claims of the product): crm.claim.read.</summary>
    public bool CanReadClaims { get; set; }

    /// <summary>The MDM product picker: mdm.global-products.read.</summary>
    public bool CanReadProducts { get; set; }

    /// <summary>The record (editor: edit; record page: details). Null on the list and on "new".</summary>
    public Guid? Id { get; set; }

    /// <summary>"new": a product pre-selected by the Studio's "create in the library" link.</summary>
    public Guid? ProductId { get; set; }
}
