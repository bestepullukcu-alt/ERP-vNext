namespace Diten.CrmService.Application.Features.CyclePeriod;

/// <summary>
/// MOD-0165 FU07 — the governed MOD-0048 reference sets this feature validates its scope references against.
/// <para><b>Country = <c>COUNTRY_CODES</c> (D-COUNTRY-SET, user decision 2026-08-28).</b> The repository currently holds
/// three country sources — the <c>country</c> set (which MOD-0149 Accounts and MOD-0151 Territory read today), the
/// platform <c>countries</c> provisioning lookup, and <c>COUNTRY_CODES</c> (the governed set the working-calendar
/// override surface reads). FU07 uses <c>COUNTRY_CODES</c> because that is where the platform is heading and Territory
/// follows next; the codes are ISO alpha-2 on both sides, so the Territory-derived business-unit narrowing keeps
/// matching ("TR" = "TR") while the two sets coexist. Should they ever diverge, the consequence is bounded and
/// designed for: the business-unit picker goes empty and falls back to the published <c>business-unit</c> vocabulary
/// (D-BU-SOURCE is a soft gate), so business-unit periods are never blocked. Consolidating the three sources is
/// follow-up F-COUNTRY-SOT.</para>
/// <para><b>Business unit = <c>business-unit</c></b> — deliberately the SAME set code MOD-0151 Territory validates
/// against, so a business-unit code means one thing across CRM rather than two.</para>
/// <para>Neither set is ever substituted by a hardcoded list: an unpublished set makes the picker empty and the write
/// fail closed (PSS-LOOKUPS-001).</para>
/// </summary>
public static class CyclePeriodReferenceSets
{
    public const string CountrySet = "COUNTRY_CODES";
    public const string BusinessUnitSet = "business-unit";
}
