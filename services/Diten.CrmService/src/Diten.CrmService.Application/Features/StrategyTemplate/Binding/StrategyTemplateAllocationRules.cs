using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Binding;

/// <summary>
/// MOD-0167 FU04 — the percentage arithmetic, as a pure function set (no I/O, no state).
/// <para><b>No tolerance and no normalisation.</b> A sku-allocated line totals exactly 100.00 or the write is refused
/// with the computed total in the response. A tolerance band would silently decide which row absorbs the rounding, and
/// an auto-normaliser would store numbers the author never wrote — the legacy CrmV2 screen already showed a
/// <c>TotalPercentage</c>, and its whole value was that the author could see their own arithmetic.</para>
/// <para>Everything is <see cref="decimal"/>. <c>double</c> is never used: comparing binary floating point against
/// 100 IS a tolerance decision, taken implicitly and inconsistently.</para>
/// </summary>
public static class StrategyTemplateAllocationRules
{
    /// <summary>One share: greater than zero, at most 100, at most two decimals.</summary>
    public static StrategyTemplateValidation.Failure? ValidatePercentage(decimal percentage, Guid lineId)
    {
        if (percentage <= 0m || percentage > StrategyTemplateLimits.RequiredAllocationTotal)
        {
            return new StrategyTemplateValidation.Failure(
                $"Percentage on line '{lineId}' must be greater than 0 and at most 100.",
                StrategyTemplateErrorCodes.SkuAllocationTotalInvalid);
        }

        return HasTooManyDecimals(percentage)
            ? new StrategyTemplateValidation.Failure(
                $"Percentage on line '{lineId}' may carry at most "
                + $"{StrategyTemplateLimits.PercentageScale} decimals.",
                StrategyTemplateErrorCodes.SkuAllocationTotalInvalid)
            : null;
    }

    /// <summary>The line total. A product-only line has no total to check.</summary>
    public static StrategyTemplateValidation.Failure? ValidateLineTotal(StrategyTemplateProductLine line)
    {
        if (!line.IsSkuAllocated())
        {
            return null;
        }

        var total = TotalOf(line);
        return total == StrategyTemplateLimits.RequiredAllocationTotal
            ? null
            : new StrategyTemplateValidation.Failure(
                $"SKU allocations on line '{line.LineId}' total {total:0.##}; they must total exactly 100.00. "
                + "Nothing is normalised automatically.",
                StrategyTemplateErrorCodes.SkuAllocationTotalInvalid);
    }

    /// <summary>Line weights are all-or-nothing. A half-specified weighting is refused because the unspecified half
    /// reads as zero to every consumer, which is a number the author never chose.</summary>
    public static StrategyTemplateValidation.Failure? ValidateLineWeights(
        IReadOnlyList<StrategyTemplateProductLine> lines)
    {
        if (lines.Count == 0)
        {
            return null;
        }

        var weighted = lines.Where(l => l.LineWeightPercentage is not null).ToList();
        if (weighted.Count == 0)
        {
            return null;
        }

        if (weighted.Count != lines.Count)
        {
            return new StrategyTemplateValidation.Failure(
                "LineWeightPercentage is set on some product lines but not on all of them: weight every line or none.",
                StrategyTemplateErrorCodes.LineWeightPartiallySpecified);
        }

        foreach (var line in weighted)
        {
            var value = line.LineWeightPercentage!.Value;
            if (value <= 0m || value > StrategyTemplateLimits.RequiredAllocationTotal || HasTooManyDecimals(value))
            {
                return new StrategyTemplateValidation.Failure(
                    $"LineWeightPercentage on line '{line.LineId}' must be greater than 0, at most 100 and carry at "
                    + $"most {StrategyTemplateLimits.PercentageScale} decimals.",
                    StrategyTemplateErrorCodes.LineWeightTotalInvalid);
            }
        }

        var total = weighted.Sum(l => l.LineWeightPercentage!.Value);
        return total == StrategyTemplateLimits.RequiredAllocationTotal
            ? null
            : new StrategyTemplateValidation.Failure(
                $"Product line weights total {total:0.##}; they must total exactly 100.00.",
                StrategyTemplateErrorCodes.LineWeightTotalInvalid);
    }

    /// <summary>The computed total of a line, exposed so the API can SHOW it (in the error and in the read models)
    /// instead of only judging it.</summary>
    public static decimal TotalOf(StrategyTemplateProductLine line)
        => line.SkuAllocations.Aggregate(0m, (sum, allocation) => sum + allocation.Percentage);

    private static bool HasTooManyDecimals(decimal value)
        => decimal.Round(value, StrategyTemplateLimits.PercentageScale) != value;
}
