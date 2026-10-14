namespace Diten.Web.Models.CRM;

/// <summary>WP-VW-W4 (W4-WEB-b) — the pharmacy report page's flags. <see cref="CanRead"/> false ⇒ only the access notice
/// (UAS-001). What a report allows comes from the CRM (kind, completeness, the 60-minute rule).</summary>
public sealed class PharmacyReportViewModel
{
    public Guid PlannedVisitId { get; set; }

    /// <summary>crm.visit-report.read.</summary>
    public bool CanRead { get; set; }

    /// <summary>crm.visit-report.record + crm.planned-visit.manage (draft, submit, shelf photo).</summary>
    public bool CanRecord { get; set; }

    /// <summary>crm.visit-report.amend (the reasoned correction after 60 minutes).</summary>
    public bool CanAmend { get; set; }

    /// <summary>mdm.gskus.read (the stock / sales rows).</summary>
    public bool CanReadGskus { get; set; }

    /// <summary>crm.contact.read (the linked doctors).</summary>
    public bool CanReadContacts { get; set; }

    /// <summary>mdm.competitor-products.read (the competitors of an added GSKU's product, W4-FIX-d G-9).</summary>
    public bool CanReadCompetitors { get; set; }

    public string? Me { get; set; }
}

/// <summary>WP-VW-W4 (W4-WEB-b) — the potential thresholds page's flags.</summary>
public sealed class PotentialThresholdsViewModel
{
    /// <summary>crm.doctor-insights.read | .read-all | .thresholds.manage.</summary>
    public bool CanRead { get; set; }

    /// <summary>crm.doctor-insights.thresholds.manage.</summary>
    public bool CanManage { get; set; }

    /// <summary>mdm.global-products.read (the product picker).</summary>
    public bool CanReadProducts { get; set; }

    public string? Me { get; set; }
}

/// <summary>WP-VW-W3E (W3E-WEB-a) — the institution QR codes bulk page (Q5).</summary>
public sealed class VisitQrCodesViewModel
{
    /// <summary>crm.visit-qr.read | .manage.</summary>
    public bool CanRead { get; set; }

    /// <summary>crm.visit-qr.manage (selection, bulk generate, label print, row print).</summary>
    public bool CanManage { get; set; }

    /// <summary>crm.account.read (the region filter reads the territory nodes through the Accounts lookups).</summary>
    public bool CanReadAccounts { get; set; }
}

/// <summary>WP-VW-W3E (W3E-WEB-a) — the QR print page (Q2).</summary>
public sealed class VisitQrPrintViewModel
{
    /// <summary>crm.visit-qr.manage — the payload is printed with it only.</summary>
    public bool CanPrint { get; set; }

    public Guid? AccountId { get; set; }

    public IReadOnlyList<Guid> AccountIds { get; set; } = [];

    public bool Labels { get; set; }

    public bool CanReadAccounts { get; set; }

    /// <summary>The single sticker's institution, parent and address (crm.account.read; else null).</summary>
    public string? AccountName { get; set; }

    public string? ParentName { get; set; }

    public string? Address { get; set; }
}
