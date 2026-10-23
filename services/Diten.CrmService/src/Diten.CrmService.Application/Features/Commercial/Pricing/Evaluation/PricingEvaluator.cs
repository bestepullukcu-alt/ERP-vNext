using System.Security.Cryptography;
using System.Text;
using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.Evaluation;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.6: the evaluation of an order's lines. Pure and deterministic (§8): one list read, one item
/// read and one rule read for the SKU set; the same input on the same published data gives the same result and the same
/// <see cref="EvaluationResult.RuleSetFingerprint"/> (the list version + the rule versions used), which the order keeps.
/// <list type="number">
/// <item>Mode: a list in force → <c>priced</c> (unit price of the requested tier); none → <c>quantity</c>.</item>
/// <item>The rule of each line: the most specific active rule of (legal entity, SKU) on the day (<see cref="RuleSpecificity"/>).</item>
/// <item>Findings (code + parameters, never a sentence): discount and combined only in priced mode; MF and quantity in both.</item>
/// <item>A line is its worst finding; the order is its worst line. No rule = no limit = <c>ok</c>.</item>
/// </list>
/// </summary>
public sealed class PricingEvaluator : IPricingEvaluator
{
    private readonly PriceResolver _prices;
    private readonly IPricingRuleRepository _rules;
    private readonly IPricingSkuDirectory? _skus;
    private readonly ILogger<PricingEvaluator> _logger;

    public PricingEvaluator(PriceResolver prices, IPricingRuleRepository rules, IPricingSkuDirectory? skus = null,
        ILogger<PricingEvaluator>? logger = null)
    {
        _prices = prices;
        _rules = rules;
        _skus = skus;
        _logger = logger ?? NullLogger<PricingEvaluator>.Instance;
    }

    public async Task<EvaluationResult> EvaluateAsync(EvaluationRequest request, CancellationToken cancellationToken = default)
    {
        var lines = request.Lines ?? Array.Empty<EvaluationLine>();
        var skuIds = lines.Select(l => l.SkuId).Distinct().ToList();
        var tier = PriceTiers.All.Contains(request.Tier, StringComparer.Ordinal) ? request.Tier : PriceTiers.PharmacistPurchase;

        var (list, items) = await _prices.ReadAsync(request.TenantId, request.LegalEntityId, request.CountryCode, request.AsOf, skuIds,
            cancellationToken);
        IReadOnlyList<PricingRule> rules = skuIds.Count == 0
            ? Array.Empty<PricingRule>()
            : (await _rules.ListActiveAsync(request.TenantId, request.LegalEntityId, skuIds, cancellationToken))
                .Where(r => r.TenantId == request.TenantId && r.LegalEntityId == request.LegalEntityId).ToList();
        var retired = await RetiredAsync(skuIds, cancellationToken);

        var priced = list is not null;
        var currency = list?.Currency;
        var bySku = items.GroupBy(i => i.SkuId).ToDictionary(g => g.Key, g => g.First());
        var rulesBySku = rules.GroupBy(r => r.SkuId).ToDictionary(g => g.Key, g => g.ToList());

        var evaluated = new List<EvaluatedLine>(lines.Count);
        var usedRules = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var line in lines.OrderBy(l => l.LineNo))
        {
            var rule = rulesBySku.TryGetValue(line.SkuId, out var skuRules)
                ? RuleSpecificity.Pick(skuRules, request.AsOf, request.PharmacySegmentCode)
                : null;
            if (rule is not null)
            {
                usedRules.Add(rule.RuleKey.ToString("D") + ":" + rule.RuleVersion);
            }

            evaluated.Add(EvaluateLine(line, rule, priced, list, bySku, tier, currency, retired.Contains(line.SkuId)));
        }

        var outcome = evaluated.Select(l => l.Outcome).Aggregate(PricingOutcomes.Ok, PricingOutcomes.Worst);
        var totals = priced ? Totals(evaluated, lines, currency) : null;
        return new EvaluationResult(priced ? PricingModes.Priced : PricingModes.Quantity, currency,
            list is null ? null : PriceResolver.Ref(list), outcome, evaluated, totals, Fingerprint(list, usedRules));
    }

    private static EvaluatedLine EvaluateLine(EvaluationLine line, PricingRule? rule, bool priced, PriceList? list,
        IReadOnlyDictionary<Guid, PriceListItem> items, string tier, string? currency, bool isRetired)
    {
        var findings = new List<PricingFinding>();
        var quantity = Math.Max(0, line.Quantity);
        var mf = line.Mf is { Buy: >= 1, Free: >= 0 } m ? m : null;
        var mfRatio = mf is null ? (decimal?)null : PricingMath.MfRatio(mf.Buy, mf.Free);
        var free = mf is null ? 0 : PricingMath.FreeBoxes(quantity, mf.Buy, mf.Free);
        var discountRatio = (line.DiscountPct ?? 0m) / 100m;

        if (isRetired)
        {
            findings.Add(Finding(PricingReasonCodes.SkuRetired, PricingFindingSeverities.Hard, ("skuId", line.SkuId)));
        }

        decimal? unitPrice = null, vatRate = null, net = null, vat = null, combinedPct = null;
        if (priced)
        {
            if (items.TryGetValue(line.SkuId, out var item) && item.PriceOf(tier) is { } price)
            {
                unitPrice = price;
                vatRate = item.VatRate;
                net = PricingMath.RoundMoney(quantity * price * (1m - discountRatio), currency);
                vat = PricingMath.RoundMoney(net.Value * item.VatRate / 100m, currency);
            }
            else
            {
                findings.Add(Finding(PricingReasonCodes.PriceSkuNotInList, PricingFindingSeverities.Hard,
                    ("skuId", line.SkuId), ("priceListName", list!.Name)));
            }

            combinedPct = PricingMath.Pct(PricingMath.Combined(discountRatio, mfRatio ?? 0m));
        }
        else if (line.DiscountPct is > 0m)
        {
            findings.Add(Finding(PricingReasonCodes.DiscountNotAllowedQuantityMode, PricingFindingSeverities.Hard));
        }

        if (rule is not null)
        {
            if (priced && line.DiscountPct is { } discount)
            {
                Limit(findings, discount, rule.MaxDiscountPct, PricingReasonCodes.DiscountSoftExceeded, PricingReasonCodes.DiscountHardExceeded,
                    v => ("value", (object?)v), l => ("limit", (object?)l));
            }

            if (mf is not null && mfRatio is { } ratio)
            {
                var valuePct = PricingMath.Pct(ratio);
                MfLimit(findings, mf, valuePct, rule.MaxMf.Hard, PricingReasonCodes.MfHardExceeded, PricingFindingSeverities.Hard);
                if (!findings.Any(f => f.Code == PricingReasonCodes.MfHardExceeded))
                {
                    MfLimit(findings, mf, valuePct, rule.MaxMf.Soft, PricingReasonCodes.MfSoftExceeded, PricingFindingSeverities.Soft);
                }
            }

            if (priced && combinedPct is { } combined && (line.DiscountPct is > 0m || mf is not null))
            {
                Limit(findings, combined, rule.MaxCombinedPct, PricingReasonCodes.CombinedSoftExceeded, PricingReasonCodes.CombinedHardExceeded,
                    v => ("valuePct", (object?)v), l => ("limitPct", (object?)l));
            }

            var q = rule.Quantity;
            if (q.Min is { } min && quantity < min)
            {
                findings.Add(Finding(PricingReasonCodes.QuantityBelowMin, PricingFindingSeverities.Hard, ("value", quantity), ("min", min)));
            }

            if (q.HardMax is { } hardMax && quantity > hardMax)
            {
                findings.Add(Finding(PricingReasonCodes.QuantityHardExceeded, PricingFindingSeverities.Hard, ("value", quantity), ("limit", hardMax)));
            }
            else if (q.SoftMax is { } softMax && quantity > softMax)
            {
                findings.Add(Finding(PricingReasonCodes.QuantitySoftExceeded, PricingFindingSeverities.Soft, ("value", quantity), ("limit", softMax)));
            }
        }

        var outcome = findings.Select(f => f.Severity == PricingFindingSeverities.Hard ? PricingOutcomes.Hard : PricingOutcomes.Soft)
            .Aggregate(PricingOutcomes.Ok, PricingOutcomes.Worst);
        return new EvaluatedLine(line.LineNo, line.SkuId, outcome, unitPrice, vatRate, net, vat, free,
            mfRatio is { } r2 ? PricingMath.Pct(r2) : null, combinedPct, findings,
            rule is null ? null : new AppliedRuleRef(rule.RuleKey, rule.RuleVersion, RuleSpecificity.Of(rule)));
    }

    // discount / combined: > hard → hard; else > soft → soft
    private static void Limit(List<PricingFinding> findings, decimal value, PricingPctLimit limit, string softCode, string hardCode,
        Func<decimal, (string, object?)> valueParam, Func<decimal, (string, object?)> limitParam)
    {
        if (limit.Hard is { } hard && value > hard)
        {
            findings.Add(Finding(hardCode, PricingFindingSeverities.Hard, valueParam(value), limitParam(hard)));
        }
        else if (limit.Soft is { } soft && value > soft)
        {
            findings.Add(Finding(softCode, PricingFindingSeverities.Soft, valueParam(value), limitParam(soft)));
        }
    }

    private static void MfLimit(List<PricingFinding> findings, MfInput mf, decimal valuePct, PricingMfTerm? limit, string code, string severity)
    {
        if (limit is null || PricingMath.MfRatio(limit) is not { } limitRatio)
        {
            return;
        }

        var limitPct = PricingMath.Pct(limitRatio);
        if (valuePct > limitPct)
        {
            findings.Add(Finding(code, severity, ("value", PricingMath.MfText(mf.Buy, mf.Free)), ("valuePct", valuePct),
                ("limit", PricingMath.MfText(limit.Buy, limit.Free)), ("limitPct", limitPct)));
        }
    }

    private static EvaluationTotals Totals(IReadOnlyList<EvaluatedLine> evaluated, IReadOnlyList<EvaluationLine> lines, string? currency)
    {
        var net = evaluated.Sum(l => l.NetAmount ?? 0m);
        var vat = evaluated.Sum(l => l.VatAmount ?? 0m);
        var quantities = lines.GroupBy(l => l.LineNo).ToDictionary(g => g.Key, g => Math.Max(0, g.First().Quantity));
        // Σ unit price × (quantity + free boxes): what the boxes would cost at list price
        var list = evaluated.Sum(l => (l.UnitPrice ?? 0m) * ((quantities.TryGetValue(l.LineNo, out var q) ? q : 0) + l.FreeQuantity));
        var effective = list > 0m ? PricingMath.Pct(1m - net / list) : 0m;
        var roundedNet = PricingMath.RoundMoney(net, currency);
        var subtotal = PricingMath.ListSubtotal(evaluated.Select(l => (quantities.TryGetValue(l.LineNo, out var q) ? q : 0, l.UnitPrice)), currency);
        return new EvaluationTotals(roundedNet, PricingMath.RoundMoney(vat, currency),
            PricingMath.RoundMoney(net + vat, currency), effective, subtotal, PricingMath.DiscountAmount(subtotal, roundedNet, currency));
    }

    private async Task<HashSet<Guid>> RetiredAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
    {
        if (_skus is null || skuIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var read = await _skus.ReadAsync(skuIds, ct);
        if (read is null)
        {
            // A pure read never fails on MDM: the retired check is skipped and said so in the log (lists and rules never
            // carry a Retired SKU in the first place — they are refused when written).
            _logger.LogWarning("pricing.evaluation.sku_status_unavailable Count={Count}", skuIds.Count);
            return new HashSet<Guid>();
        }

        return read.Values.Where(s => s.IsRetired).Select(s => s.SkuId).ToHashSet();
    }

    private static PricingFinding Finding(string code, string severity, params (string Key, object? Value)[] parameters)
        => new(code, severity, parameters.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));

    /// <summary>SHA-256 over the list version and the rule versions used, in a stable order.</summary>
    public static string Fingerprint(PriceList? list, IEnumerable<string> ruleVersions)
    {
        var text = "list:" + (list is null ? "none" : list.Id.ToString("D") + ":" + list.ListVersion)
                   + ";rules:" + string.Join(",", ruleVersions.OrderBy(x => x, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}

/// <summary>WP-VW-W6 W6-BE-b — MOD-0156 §4.5: the interim FX table's reader (MOD-0277 replaces its source later).</summary>
public sealed class FxRateReader : IFxRateReader
{
    private readonly IFxRateRepository _rates;

    public FxRateReader(IFxRateRepository rates) => _rates = rates;

    public async Task<FxRateSnapshot?> GetAsync(Guid tenantId, Guid legalEntityId, string from, string to, DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var rate = await _rates.GetLatestAsync(tenantId, legalEntityId, PricingCommon.NormalizeCurrency(from),
            PricingCommon.NormalizeCurrency(to), asOf, cancellationToken);
        return rate is null || rate.TenantId != tenantId || rate.IsDeleted ? null : new FxRateSnapshot(rate.Rate, rate.RateDate, rate.Source);
    }
}
