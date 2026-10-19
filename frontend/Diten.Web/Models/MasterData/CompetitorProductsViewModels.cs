namespace Diten.Web.Models.MasterData;

/// <summary>WP-VW-W4 (W4-WEB-c) — every competitor products page. Each flag is exactly the key(s) of the endpoint behind it.</summary>
public sealed class CompetitorProductsPageViewModel
{
    /// <summary>mdm.competitor-products.read (UAS-001: without it only the access notice).</summary>
    public bool CanRead { get; set; }

    /// <summary>mdm.competitor-products.manage: new / edit / deactivate / merge, the proposals tab and the decisions.</summary>
    public bool CanManage { get; set; }

    /// <summary>mdm.global-products.read: "our products" (picker, chips, filter).</summary>
    public bool CanReadProducts { get; set; }

    /// <summary>mdm.brands.read: the MDM dosage form vocabulary (the brand/product contract, G4).</summary>
    public bool CanReadForms { get; set; }

    /// <summary>crm.doctor-insights.read | .read-all | .thresholds.manage: the indication picker (G3).</summary>
    public bool CanReadIndications { get; set; }

    /// <summary>crm.visit-report.read-aggregate: the report usage counts (G2).</summary>
    public bool CanReadUsage { get; set; }

    /// <summary>platform.meetings.create: the co-workers' names (the Meetings attendee lookup).</summary>
    public bool CanReadPeople { get; set; }

    public Guid? Id { get; set; }
    public Guid? ProposalId { get; set; }

    /// <summary>records | proposals | create | edit | details | merge | proposal | complete | proposal-merge.</summary>
    public string Mode { get; set; } = "records";

    /// <summary>The signed-in user's id (the MDM's actor: sub) — the SoD check of the decision buttons.</summary>
    public string? Actor { get; set; }
}
