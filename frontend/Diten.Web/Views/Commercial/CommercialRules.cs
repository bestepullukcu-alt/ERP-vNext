namespace Diten.Web.Views.Commercial;

/// <summary>
/// WP-VW-W6 (W6-WEB-a) — the ONE source of the commercial rule messages (MOD-0156 §11: "tek kaynak tercih edilir"), shared
/// by the pre-order screens (W6-WEB-a) and the price list / rule screens (W6-WEB-b). The server never sends a sentence
/// (AC-PRICE-4): a finding is a CODE + parameters, a refusal is a CODE; the words live here as <c>Rule.{code}</c> in seven
/// languages with the contract's parameter names as placeholders (<c>{value}</c>, <c>{limit}</c>, <c>{valuePct}</c>,
/// <c>{limitPct}</c>, <c>{min}</c>, <c>{skuId}</c>, <c>{priceListName}</c>). The key list is the resx itself — every
/// MOD-0156 reason code (<c>PricingReasonCodes</c>) and every MOD-0168 refusal code (<c>PreOrderErrors</c>); a CRM code
/// without its message is a red test (the test reads the CRM constants). <c>_CommercialRulesL10n.cshtml</c> serializes
/// every entry; <c>commercial-rules.js</c> merges them into <c>window.L10n</c> and words a finding / a refusal.
/// </summary>
public sealed class CommercialRules
{
    /// <summary>The key prefix of every message.</summary>
    public const string Prefix = "Rule.";

    /// <summary>The guardrail outcomes (MOD-0156 PricingOutcomes) — the badge words are <c>Rule.Outcome.{outcome}</c>.</summary>
    public static readonly IReadOnlyList<string> Outcomes = ["ok", "soft", "hard"];

    /// <summary>The script element the payload travels in.</summary>
    public const string PayloadId = "commercial-rules-l10n";
}
