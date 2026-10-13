namespace Diten.Web.Models.CRM;

/// <summary>WP-VW-W4 (W4-WEB-a) — the visit report wizard v2. Each flag is exactly the key(s) of the endpoint behind it.</summary>
public sealed class VisitReportIndexViewModel
{
    /// <summary>The page and the report reads: crm.visit-report.read (UAS-001: without it only the access notice).</summary>
    public bool CanRead { get; set; }

    /// <summary>Draft + submit: crm.visit-report.record AND crm.planned-visit.manage.</summary>
    public bool CanRecord { get; set; }

    /// <summary>The reasoned amendment after the 60 minutes: crm.visit-report.amend.</summary>
    public bool CanAmend { get; set; }

    /// <summary>The doctor's name, the institution, the products' names, the deadline: crm.planned-visit.read.</summary>
    public bool CanReadVisit { get; set; }

    /// <summary>The co-visitors' names and the presentation's note: crm.visit-execution.read.</summary>
    public bool CanReadExecution { get; set; }

    /// <summary>The profile card's history, availability and channel: crm.doctor-insights.read (or .read-all).</summary>
    public bool CanReadInsights { get; set; }

    /// <summary>The "is it in the list?" search before a proposal: mdm.competitor-products.read.</summary>
    public bool CanLookupCompetitors { get; set; }

    /// <summary>"Not in the list, add a competitor": mdm.competitor-products.propose (else the submit forwards it).</summary>
    public bool CanProposeCompetitors { get; set; }

    /// <summary>The sample's GSKU list: mdm.gskus.read.</summary>
    public bool CanReadGskus { get; set; }

    /// <summary>The "unplanned product presented" picker: mdm.global-products.read.</summary>
    public bool CanPickProducts { get; set; }

    public Guid? PlannedVisitId { get; set; }
}
