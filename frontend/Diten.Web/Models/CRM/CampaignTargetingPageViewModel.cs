namespace Diten.Web.Models.CRM;

/// <summary>
/// MOD-0165 FU10 — the read-only targeting page. It shows the ACTIVE mode's targets and, separately, how much data
/// the passive mode is still holding.
///
/// <para><b>The dormant counts are shown, not hidden.</b> Switching the targeting mode never deletes the other mode's
/// data, so a campaign can be carrying segments it no longer uses (or manual rows it no longer uses). Hiding that
/// would let it accumulate invisibly until nobody remembers why it is there.</para>
///
/// <para>Its own file, like every other non-form view model here: the DataTable contract verifier resolves a form
/// field's type by the last property of that name in a file, and a projection parked beside the edit model can shadow
/// one.</para>
/// </summary>
public sealed class CampaignTargetingPageViewModel
{
    public CampaignDetailViewModel Campaign { get; set; } = new();

    /// <summary>Populated only in manual mode — the active audience rows.</summary>
    public List<CampaignTargetViewModel> ManualTargets { get; set; } = [];

    /// <summary>Segments kept from an earlier segment-mode run, while the campaign is now manual.</summary>
    public int DormantSegmentCount { get; set; }

    /// <summary>Manual rows kept from an earlier manual-mode run, while the campaign is now segment-targeted.</summary>
    public int DormantManualTargetCount { get; set; }
}
