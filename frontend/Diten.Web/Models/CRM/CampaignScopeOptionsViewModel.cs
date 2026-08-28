namespace Diten.Web.Models.CRM;

/// <summary>
/// MOD-0165 FU09 — the cascading scope selector's option source, as the API projects it.
///
/// <para><b>It lives in its own file deliberately.</b> The DataTable contract verifier resolves a form field's type by
/// the LAST property of that name in the file; a non-form view model parked beside <c>CampaignEditViewModel</c> can
/// shadow a form field and make the optional-date rule report a defect that does not exist. That happened once during
/// FU08 and is not repeated here.</para>
///
/// <para>Each list carries its own readiness flag, because "the set is not published", "the dependency is unreachable"
/// and "no territory plan matches" are three different empty lists, and an author needs to know which one they are
/// looking at. A hardcoded fallback list is never substituted for any of them.</para>
/// </summary>
public sealed class CampaignScopeOptionsViewModel
{
    public List<string> ScopeTypes { get; set; } = [];

    public List<CampaignScopeOptionViewModel> Countries { get; set; } = [];

    /// <summary>False when the governed country set is not published — the country level cannot be authored at all.</summary>
    public bool CountrySetPublished { get; set; }

    public List<CampaignScopeOptionViewModel> LegalEntities { get; set; } = [];

    /// <summary>False when the master-data lookup did not answer. Distinct from "there are none".</summary>
    public bool LegalEntityLookupAvailable { get; set; }

    public List<CampaignScopeOptionViewModel> BusinessUnits { get; set; } = [];

    public bool BusinessUnitSetPublished { get; set; }

    /// <summary>
    /// True when the business-unit list is the Territory-derived narrowing; false when it fell back to the full
    /// published vocabulary because no plan matched. The fallback is what keeps a business-unit campaign authorable
    /// before its field plan exists.
    /// </summary>
    public bool BusinessUnitFromTerritory { get; set; }
}

/// <summary>One selectable value for a scope level.</summary>
public sealed class CampaignScopeOptionViewModel
{
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}
