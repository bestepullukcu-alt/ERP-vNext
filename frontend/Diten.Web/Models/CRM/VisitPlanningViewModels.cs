namespace Diten.Web.Models.CRM;

/// <summary>The page model for the MOD-0155 FU05 bespoke Day/Week SETUP console. Only the permission flags cross into the
/// view; every business call is a same-origin proxy to the Gateway.</summary>
public sealed class VisitPlanningIndexViewModel
{
    public bool CanGenerate { get; set; }
    public bool CanApply { get; set; }

    /// <summary>WP-VP-4J (4) — <c>crm.visit-plan.read-all</c>: the list's "Rep" filter is shown only then (a rep sees
    /// only their own plans, K-1).</summary>
    public bool CanReadAll { get; set; }
}

/// <summary>Shell model for the Golden Compact session pages (Create / Edit / Details). The session itself is loaded
/// client-side through the same-origin /api/sessions proxy; only the id + permission flags cross into the view.</summary>
public sealed class VisitPlanningSessionPageViewModel
{
    public Guid? SessionId { get; set; }
    public bool CanGenerate { get; set; }
    public bool CanApply { get; set; }

    /// <summary>WP-VP-FIX-1 — the plan is committed / archived: no write affordance is rendered (the flags above are
    /// already false) and the page states that it is read-only.</summary>
    public bool IsReadOnly { get; set; }
}
