namespace Diten.CrmService.Application.Features.Campaign;

/// <summary>
/// MOD-0165 FU09 — the governed MOD-0048 reference sets the campaign scope validates against.
///
/// <para><b>Deliberately the SAME set codes MOD-0165 FU07 uses for a cycle period's scope.</b> The applicability rule
/// compares a campaign's address against a period's, so the two sides have to be spelled from one alphabet; reading
/// them from different sets would never raise an error, it would simply make the comparison match nothing — the
/// quietest way to kill a feature.</para>
///
/// <para><b>The country set is currently narrow.</b> <c>COUNTRY_CODES</c> holds only a handful of published codes
/// today (follow-up F-COUNTRY-SOT consolidates the repository's three country sources). The consequence is bounded and
/// intended: a country a tenant actually works in but which is not published cannot be used as a COUNTRY scope
/// (fail-closed, 400 — a hardcoded fallback list is forbidden), while the tenant, legal-entity and business-unit
/// levels are unaffected. Publishing more codes needs no code change.</para>
///
/// <para>Neither set is ever substituted by a hardcoded list: an unpublished set makes the picker empty and the write
/// fail closed, and the two cases are reported with different codes so an operator and an author each know whose
/// problem it is.</para>
/// </summary>
public static class CampaignScopeReferenceSets
{
    public const string CountrySet = "COUNTRY_CODES";
    public const string BusinessUnitSet = "business-unit";
}
