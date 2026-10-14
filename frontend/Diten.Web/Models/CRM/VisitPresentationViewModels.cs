namespace Diten.Web.Models.CRM;

/// <summary>
/// WP-VW-W3 (W3-WEB-b) — the Visit Presentation page flags. <see cref="CanRead"/> false ⇒ the page draws ONLY the access
/// notice (UAS-001). The other flags mirror the CRM endpoints' keys; the CRM stays the authority.
/// </summary>
public sealed class VisitPresentationIndexViewModel
{
    /// <summary>crm.visit-report.read (the presentation) AND crm.visit-execution.read (the execution, the brief).</summary>
    public bool CanRead { get; set; }

    /// <summary>Capture / finish / signature: crm.visit-execution.record.</summary>
    public bool CanRecord { get; set; }

    /// <summary>The visit's name and institution (the planned visit read): crm.planned-visit.read.</summary>
    public bool CanReadVisit { get; set; }

    /// <summary>The co-visitor picker (the Meetings attendee lookup): platform.meetings.create.</summary>
    public bool CanPickPeople { get; set; }

    public Guid? PlannedVisitId { get; set; }

    /// <summary><c>&amp;phase=summary</c>: open the finish summary (a started execution is finished first).</summary>
    public bool Summary { get; set; }
}
