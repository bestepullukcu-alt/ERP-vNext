namespace Diten.Web.Models.CRM;

/// <summary>
/// WP-VW-W2 (WEB-a) — the Visit Workspace page flags. <see cref="CanRead"/> false ⇒ the page draws ONLY the access
/// notice (UAS-001: no heading, no calendar, no buttons). The action flags mirror the CRM endpoints' keys; the CRM
/// stays the authority (a hidden button is a display decision, not the boundary).
/// </summary>
public sealed class VisitWorkspaceIndexViewModel
{
    public bool CanRead { get; set; }

    /// <summary>Cancel a visit / create an unplanned one: crm.planned-visit.manage.</summary>
    public bool CanManageVisits { get; set; }

    /// <summary>Not done / reschedule (outcome + submit): crm.visit-report.record AND crm.planned-visit.manage.</summary>
    public bool CanRecord { get; set; }

    /// <summary>Approve / reopen a week: crm.visit-plan.apply AND crm.planned-visit.manage.</summary>
    public bool CanApplyWeek { get; set; }

    /// <summary>The doctor search of the unplanned-visit dialog: crm.contact.read.</summary>
    public bool CanSearchContacts { get; set; }
}
