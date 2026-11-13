namespace Diten.Web.Models.CRM;

/// <summary>WP-VW-W3E (W3E-WEB-b) — the evidence flags page's flags (UAS-001: without <see cref="CanRead"/> only the notice).</summary>
public sealed class VisitEvidenceFlagsViewModel
{
    /// <summary>crm.visit-evidence-flags.read (the team) or .read-all (the tenant).</summary>
    public bool CanRead { get; set; }

    /// <summary>crm.visit-evidence-flags.read-all: the rep filter is "all reps" instead of "my team".</summary>
    public bool CanReadAll { get; set; }

    /// <summary>The photo thumbnail's own keys (the existing evidence read path).</summary>
    public bool CanSeePhotos { get; set; }
}
