using System.Globalization;
using System.Numerics;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public static class HistoryUnitConversion
{
    public const int QuantityScale = 4;

    public static decimal ToBaseQuantity(decimal quantity, decimal numerator, decimal denominator)
    {
        if (quantity <= 0 || numerator <= 0 || denominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        var (quantityValue, quantityScale) = Parts(quantity);
        var (numeratorValue, numeratorScale) = Parts(numerator);
        var (denominatorValue, denominatorScale) = Parts(denominator);
        var scaledNumerator = quantityValue * numeratorValue *
            BigInteger.Pow(10, denominatorScale + QuantityScale);
        var scaledDenominator = denominatorValue *
            BigInteger.Pow(10, quantityScale + numeratorScale);
        var rounded = BigInteger.DivRem(scaledNumerator, scaledDenominator, out var remainder);
        if (remainder * 2 >= scaledDenominator) rounded++;

        var whole = BigInteger.DivRem(rounded, 10000, out var fraction);
        if (!decimal.TryParse(whole.ToString(CultureInfo.InvariantCulture),
                NumberStyles.None, CultureInfo.InvariantCulture, out var wholeDecimal))
            throw new OverflowException("Converted quantity exceeds decimal range.");
        return checked(wholeDecimal + (decimal)fraction / 10000m);
    }

    private static (BigInteger Value, int Scale) Parts(decimal value)
    {
        var bits = decimal.GetBits(value);
        var mantissa = ((BigInteger)(uint)bits[2] << 64) |
            ((BigInteger)(uint)bits[1] << 32) | (uint)bits[0];
        return (mantissa, (bits[3] >> 16) & 0xFF);
    }
}
