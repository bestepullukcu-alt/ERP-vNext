namespace Diten.Web.Models.CRM;

/// <summary>The page model for the MOD-0155 FU05 bespoke Day/Week SETUP console. Only the permission flags cross into the
/// view; every business call is a same-origin proxy to the Gateway.</summary>
public sealed class VisitPlanningIndexViewModel
{
    public bool CanGenerate { get; set; }
    public bool CanApply { get; set; }
}

/// <summary>Shell model for the Golden Compact session pages (Create / Edit / Details). The session itself is loaded
/// client-side through the same-origin /api/sessions proxy; only the id + permission flags cross into the view.</summary>
public sealed class VisitPlanningSessionPageViewModel
{
    public Guid? SessionId { get; set; }
    public bool CanGenerate { get; set; }
    public bool CanApply { get; set; }
}
