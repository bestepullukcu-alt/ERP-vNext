namespace Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;

// WP-VW-W6 W6-BE-b — MOD-0156 §4.6.1: the in-process surface MOD-0168 (Order Capture) consumes. Pure reads: no write, no
// side effect, no permission check (the calling endpoint guards itself, §14). A change of these signatures is a change of
// BOTH packs (MOD-0156 §4.6.1 and MOD-0168 §4.2) — never one side alone.

/// <summary>The price mode (K-O4, K-W10 f): <c>priced</c> when a list is in force for the (legal entity, country) on the
/// day, else <c>quantity</c> (no price, no VAT, no discount; MF and quantity rules only).</summary>
public static class PricingModes
{
    public const string Priced = "priced";
    public const string Quantity = "quantity";
}

/// <summary>The outcome of a line / an order (§4.6.2): the worst finding; <c>ok</c> without one.</summary>
public static class PricingOutcomes
{
    public const string Ok = "ok";
    public const string Soft = "soft";
    public const string Hard = "hard";

    public static readonly IReadOnlyList<string> All = new[] { Ok, Soft, Hard };

    public static string Worst(string a, string b) => Rank(a) >= Rank(b) ? a : b;

    public static int Rank(string outcome) => outcome switch
    {
        Hard => 2,
        Soft => 1,
        _ => 0
    };
}

public static class PricingFindingSeverities
{
    public const string Soft = "soft";
    public const string Hard = "hard";
}

public sealed record PriceListRef(Guid Id, int Version, string Name, string Kind, string Currency, DateOnly ValidFrom);

public sealed record ResolvedPriceItem(Guid SkuId, decimal? UnitPrice, decimal? VatRate, bool Found);

public sealed record PriceResolution(string Mode, PriceListRef? PriceList, IReadOnlyList<ResolvedPriceItem> Items);

/// <summary>Which list is in force for the (legal entity, country) on <c>asOf</c>, and the tier price of each SKU in it.</summary>
public interface IPriceResolver
{
    Task<PriceResolution> ResolveAsync(Guid tenantId, Guid legalEntityId, string countryCode, DateOnly asOf, string tier,
        IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken = default);
}

public sealed record MfInput(int Buy, int Free);

public sealed record EvaluationLine(int LineNo, Guid SkuId, int Quantity, MfInput? Mf = null, decimal? DiscountPct = null);

public sealed record EvaluationRequest(
    Guid TenantId, Guid LegalEntityId, string CountryCode, DateOnly AsOf, string Tier, string? PharmacySegmentCode,
    IReadOnlyList<EvaluationLine> Lines);

/// <summary>A reason CODE with its parameters (§4.6.3) — never a sentence: the client words it in seven languages.</summary>
public sealed record PricingFinding(string Code, string Severity, IReadOnlyDictionary<string, object?> Params);

public sealed record AppliedRuleRef(Guid RuleKey, int RuleVersion, int Specificity);

public sealed record EvaluatedLine(
    int LineNo, Guid SkuId, string Outcome, decimal? UnitPrice, decimal? VatRate, decimal? NetAmount, decimal? VatAmount,
    int FreeQuantity, decimal? MfRatioPct, decimal? CombinedDiscountPct, IReadOnlyList<PricingFinding> Findings, AppliedRuleRef? Rule);

/// <summary><see cref="ListSubtotal"/> = Σ quantity × list unit price (VAT excluded); <see cref="DiscountAmount"/> = list
/// subtotal − net (W6-FIX-c, V-3).</summary>
public sealed record EvaluationTotals(decimal Net, decimal Vat, decimal Gross, decimal EffectiveDiscountPct, decimal ListSubtotal, decimal DiscountAmount);

/// <summary>
/// WP-VW-W6 W6-FIX-c — MOD-0156's completion of the totals of an order evaluated before the list subtotal existed: the same
/// arithmetic the evaluator uses (<see cref="Evaluation.PricingMath"/>), over the order's stored lines and net. The read side
/// of MOD-0168 asks here; it keeps no copy (AC-PRICE-2).
/// </summary>
public static class PricingTotals
{
    public static (decimal ListSubtotal, decimal DiscountAmount) ListAndDiscount(IEnumerable<(int Quantity, decimal? UnitPrice)> lines, decimal net,
        string? currency)
    {
        var subtotal = Evaluation.PricingMath.ListSubtotal(lines, currency);
        return (subtotal, Evaluation.PricingMath.DiscountAmount(subtotal, net, currency));
    }
}

public sealed record EvaluationResult(
    string Mode, string? Currency, PriceListRef? PriceList, string Outcome, IReadOnlyList<EvaluatedLine> Lines,
    EvaluationTotals? Totals, string RuleSetFingerprint);

/// <summary>Prices, VAT, free boxes, MF ratio, combined discount and the guardrail findings of an order's lines — the ONE
/// place this arithmetic lives (AC-EVAL-3). Deterministic: same input + same published data → same result and fingerprint.</summary>
public interface IPricingEvaluator
{
    Task<EvaluationResult> EvaluateAsync(EvaluationRequest request, CancellationToken cancellationToken = default);
}

public sealed record FxRateSnapshot(decimal Rate, DateOnly RateDate, string Source);

/// <summary>The latest rate with <c>RateDate ≤ asOf</c> (with its date and source), or null — the order is still taken,
/// without a local-currency equivalent (§4.5).</summary>
public interface IFxRateReader
{
    Task<FxRateSnapshot?> GetAsync(Guid tenantId, Guid legalEntityId, string from, string to, DateOnly asOf,
        CancellationToken cancellationToken = default);
}
