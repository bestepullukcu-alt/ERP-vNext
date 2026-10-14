namespace Diten.Web.Models.CRM;

/// <summary>
/// WP-VW-W3 (W3-WEB-d) — the visit evidence settings page flags. <see cref="CanRead"/> false ⇒ the page draws ONLY the
/// access notice (UAS-001). The flags mirror the CRM endpoints' keys; the CRM stays the authority (a hidden button is a
/// display decision, not the boundary).
/// </summary>
public sealed class VisitEvidenceSettingsIndexViewModel
{
    /// <summary>crm.visit-evidence-policy.read OR crm.visit-evidence-policy.manage (K-C4).</summary>
    public bool CanRead { get; set; }

    /// <summary>The tenant policy form is editable (PUT): crm.visit-evidence-policy.manage.</summary>
    public bool CanManagePolicy { get; set; }

    /// <summary>The exceptions tab (list, detail, the reviewer's decision): crm.visit-evidence-exception.read.</summary>
    public bool CanReadExceptions { get; set; }

    /// <summary>Write / submit / withdraw / cancel / end early an exception: crm.visit-evidence-exception.manage.</summary>
    public bool CanManageExceptions { get; set; }

    /// <summary>The session user as CRM names the actor (sub) — the page marks its own records (SoD hint).</summary>
    public string? Me { get; set; }
}
