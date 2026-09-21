namespace Diten.CrmService.Application.Features.StrategyTemplate;

/// <summary>
/// MOD-0167 FU04 (WP-ST-SCOPE) — the governed MOD-0048 reference sets the play scope validates against.
///
/// <para><b>Deliberately the SAME set codes the campaign and the cycle period use for their scope.</b> A play, a
/// campaign and a period must be spelled from one alphabet so any future scope-consistency comparison across the chain
/// (Segment → StrategyTemplate → CyclePeriod → Campaign) can match at all; reading them from different sets would never
/// raise an error, it would simply make the comparison match nothing — the quietest way to kill a feature.</para>
///
/// <para><b>The country set is currently narrow.</b> <c>COUNTRY_CODES</c> holds only a handful of published codes today
/// (follow-up F-COUNTRY-SOT consolidates the repository's three country sources). The consequence is bounded and
/// intended: a country a tenant works in but which is not published cannot be used as a COUNTRY scope (fail-closed, 400 —
/// a hardcoded fallback list is forbidden), while the tenant, legal-entity and business-unit levels are unaffected.</para>
///
/// <para>Neither set is ever substituted by a hardcoded list: an unpublished set makes the picker empty and the write
/// fail closed, and the two cases are reported with different codes so an operator and an author each know whose problem
/// it is.</para>
/// </summary>
public static class StrategyTemplateScopeReferenceSets
{
    public const string CountrySet = "COUNTRY_CODES";
    public const string BusinessUnitSet = "business-unit";
}
