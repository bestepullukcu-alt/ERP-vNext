using Diten.CrmService.Application.Features.Commercial.Pricing.Evaluation;
using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Validators;

/// <summary>MOD-0156 §4.3 / §13.1 — the limits of a rule: at least one criterion; each soft ≤ its hard; Min ≤ SoftMax ≤
/// HardMax; percentages 0–100 with one decimal; MF a, b ≥ 1 and ratio(soft) ≤ ratio(hard); the period's end not before its
/// start; no segment in the first release (§20.3 AÇIK-3).</summary>
public static class PricingRuleValidator
{
    public static (string Code, string Message)? Check(PricingRuleInput? input, bool create)
    {
        if (input is null)
        {
            return (PricingErrors.Invalid, "A body is required.");
        }

        if (create && (input.LegalEntityId is null || input.LegalEntityId == Guid.Empty))
        {
            return (PricingErrors.LegalEntityInvalid, "The legal entity is required.");
        }

        if (create && (input.SkuId is null || input.SkuId == Guid.Empty))
        {
            return (PricingErrors.SkuInvalid, "The SKU is required.");
        }

        if (PricingCommon.Clean(input.SegmentCode) is not null && !RuleSpecificity.SegmentsEnabled)
        {
            return (PricingErrors.SegmentNotEnabled, "The pharmacy segment is not a rule criterion yet; leave it empty.");
        }

        if (input.ValidFrom is { } from && input.ValidTo is { } to && to < from)
        {
            return (PricingErrors.Invalid, "validTo cannot be before validFrom.");
        }

        var discount = input.MaxDiscountPct;
        var mf = input.MaxMf;
        var combined = input.MaxCombinedPct;
        var quantity = input.Quantity;
        var any = discount is { Soft: not null } or { Hard: not null } || mf is { Soft: not null } or { Hard: not null }
                  || combined is { Soft: not null } or { Hard: not null }
                  || quantity is { Min: not null } or { SoftMax: not null } or { HardMax: not null };
        if (!any)
        {
            return (PricingErrors.RuleLimitsInvalid, "Set at least one limit (discount, MF, combined or quantity).");
        }

        if ((Pct(discount) ?? Pct(combined)) is { } pctProblem)
        {
            return (PricingErrors.RuleLimitsInvalid, pctProblem);
        }

        if (mf is not null)
        {
            foreach (var term in new[] { mf.Soft, mf.Hard })
            {
                if (term is not null && (term.Buy < 1 || term.Free < 1))
                {
                    return (PricingErrors.RuleLimitsInvalid, "An MF limit is a + b with a, b ≥ 1 (e.g. 10+2).");
                }
            }

            if (mf is { Soft: { } s, Hard: { } h } && PricingMath.MfRatio(s.Buy, s.Free) > PricingMath.MfRatio(h.Buy, h.Free))
            {
                return (PricingErrors.RuleLimitsInvalid, "The soft MF limit cannot be more generous than the hard one.");
            }
        }

        if (quantity is not null)
        {
            if (quantity.Min is < 0 || quantity.SoftMax is < 0 || quantity.HardMax is < 0)
            {
                return (PricingErrors.RuleLimitsInvalid, "Quantities cannot be negative.");
            }

            var ordered = new[] { quantity.Min, quantity.SoftMax, quantity.HardMax }.OfType<int>().ToList();
            if (!ordered.SequenceEqual(ordered.OrderBy(x => x)))
            {
                return (PricingErrors.RuleLimitsInvalid, "Min ≤ soft max ≤ hard max.");
            }
        }

        return null;
    }

    private static string? Pct(PricingPctLimitInput? limit)
    {
        if (limit is null)
        {
            return null;
        }

        foreach (var value in new[] { limit.Soft, limit.Hard })
        {
            if (value is { } v && (v < 0m || v > 100m || PricingCommon.Decimals(v) > 1))
            {
                return "A percentage is 0–100 with at most one decimal.";
            }
        }

        return limit is { Soft: { } soft, Hard: { } hard } && soft > hard ? "A soft limit cannot be above its hard limit." : null;
    }

    public static void Apply(PricingRule rule, PricingRuleInput input)
    {
        rule.SegmentCode = PricingCommon.Clean(input.SegmentCode);
        rule.ValidFrom = input.ValidFrom;
        rule.ValidTo = input.ValidTo;
        rule.MaxDiscountPct = new PricingPctLimit { Soft = input.MaxDiscountPct?.Soft, Hard = input.MaxDiscountPct?.Hard };
        rule.MaxMf = new PricingMfLimit { Soft = Term(input.MaxMf?.Soft), Hard = Term(input.MaxMf?.Hard) };
        rule.MaxCombinedPct = new PricingPctLimit { Soft = input.MaxCombinedPct?.Soft, Hard = input.MaxCombinedPct?.Hard };
        rule.Quantity = new PricingQuantityLimit { Min = input.Quantity?.Min, SoftMax = input.Quantity?.SoftMax, HardMax = input.Quantity?.HardMax };
    }

    private static PricingMfTerm? Term(PricingMfTermInput? term) => term is null ? null : new PricingMfTerm { Buy = term.Buy, Free = term.Free };

    /// <summary>The change-log view of a rule's limits.</summary>
    public static (string, object?)[] Fields(PricingRule r)
        =>
        [
            ("segment", r.SegmentCode), ("validFrom", r.ValidFrom), ("validTo", r.ValidTo),
            ("discountSoft", r.MaxDiscountPct.Soft), ("discountHard", r.MaxDiscountPct.Hard),
            ("mfSoft", r.MaxMf.Soft is { } ms ? PricingMath.MfText(ms.Buy, ms.Free) : null), ("mfHard", r.MaxMf.Hard is { } mh ? PricingMath.MfText(mh.Buy, mh.Free) : null),
            ("combinedSoft", r.MaxCombinedPct.Soft), ("combinedHard", r.MaxCombinedPct.Hard),
            ("quantityMin", r.Quantity.Min), ("quantitySoftMax", r.Quantity.SoftMax), ("quantityHardMax", r.Quantity.HardMax)
        ];
}
