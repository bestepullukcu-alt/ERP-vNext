using System.Globalization;
using System.Numerics;

namespace Diten.SafetyStockService.Calculation;

public sealed class MethodACalculator
{
    public MethodAResult Calculate(MethodARequest? request)
    {
        if (request is null)
            return Fail(MethodAError.MissingRequest, "A calculation request is required.");

        var fixture = request.Fixture;
        if (fixture is null)
            return Fail(MethodAError.MissingFixture, "A daily demand fixture is required.");

        if (string.IsNullOrWhiteSpace(fixture.Version))
            return Fail(MethodAError.MissingFixtureVersion, "The fixture version is required.");

        if (!HasCompleteScope(fixture.Scope))
            return Fail(MethodAError.InvalidScope, "The fixture must name a tenant, legal entity, SKU, and warehouse.");

        if (string.IsNullOrWhiteSpace(fixture.BaseUom))
            return Fail(MethodAError.MissingBaseUom, "The fixture base unit of measure is required.");

        if (fixture.StartDate == default || fixture.EndDate == default || fixture.EndDate < fixture.StartDate)
            return Fail(MethodAError.InvalidDateRange, "The fixture requires a nonempty, ordered calendar date range.");

        if (request.CoverageDays is null)
            return Fail(MethodAError.MissingCoverageDays, "Coverage days must be supplied with the request.");

        if (request.CoverageDays <= 0m)
            return Fail(MethodAError.InvalidCoverageDays, "Coverage days must be greater than zero.");

        if (fixture.Days is null)
            return Fail(MethodAError.MissingDays, "Daily demand records are required.");

        var dailyInputs = fixture.Days.ToArray();
        var calendarDayCount = fixture.EndDate.DayNumber - fixture.StartDate.DayNumber + 1;
        var seenDates = new HashSet<DateOnly>();

        foreach (var day in dailyInputs)
        {
            if (day is null)
                return Fail(MethodAError.InvalidDay, "A daily demand record is null.");

            if (day.Date < fixture.StartDate || day.Date > fixture.EndDate)
                return Fail(MethodAError.DayOutsideRange, $"Day {FormatDate(day.Date)} is outside the fixture window.");

            if (!seenDates.Add(day.Date))
                return Fail(MethodAError.DuplicateDay, $"Day {FormatDate(day.Date)} occurs more than once.");

            if (day.Scope != fixture.Scope)
                return Fail(MethodAError.ScopeMismatch, $"Day {FormatDate(day.Date)} has a different tenant, legal entity, SKU, or warehouse.");

            if (!string.Equals(day.BaseUom, fixture.BaseUom, StringComparison.Ordinal))
                return Fail(MethodAError.BaseUomMismatch, $"Day {FormatDate(day.Date)} has a different base unit of measure.");

            if (day.DemandQuantity is null)
                return Fail(MethodAError.UnknownDemand, $"Demand for day {FormatDate(day.Date)} is unknown.");

            if (day.DemandQuantity < 0m)
                return Fail(MethodAError.NegativeDemand, $"Demand for day {FormatDate(day.Date)} is negative.");
        }

        if (seenDates.Count != calendarDayCount)
        {
            for (var offset = 0; offset < calendarDayCount; offset++)
            {
                var date = fixture.StartDate.AddDays(offset);
                if (!seenDates.Contains(date))
                    return Fail(MethodAError.MissingDay, $"Demand for calendar day {FormatDate(date)} is missing.");
            }
        }

        Array.Sort(dailyInputs, static (left, right) => left.Date.CompareTo(right.Date));

        try
        {
            var totalDemand = 0m;
            foreach (var day in dailyInputs)
                totalDemand = checked(totalDemand + day.DemandQuantity!.Value);

            var averageDailyDemand = checked(totalDemand / calendarDayCount);
            if (totalDemand > 0m && averageDailyDemand == 0m)
                return Fail(MethodAError.ArithmeticOverflow, "The daily average cannot be represented as a nonzero decimal.");

            // The average is a finite decimal trace value; calculate the candidate from the
            // aggregate ratio so rounding the repeating average cannot change the raw result.
            if (!TryCalculateRawCandidate(totalDemand, request.CoverageDays.Value, calendarDayCount, out var rawCandidateQuantity))
                return Fail(MethodAError.ArithmeticOverflow, "The raw candidate cannot be represented as a nonzero decimal.");

            var trace = new MethodATrace(
                fixture.Version,
                fixture.Scope!,
                fixture.BaseUom,
                fixture.StartDate,
                fixture.EndDate,
                Array.AsReadOnly(dailyInputs),
                totalDemand,
                calendarDayCount,
                averageDailyDemand,
                request.CoverageDays.Value,
                rawCandidateQuantity);

            return MethodAResult.Success(trace);
        }
        catch (OverflowException)
        {
            return Fail(MethodAError.ArithmeticOverflow, "Decimal arithmetic exceeded its representable range.");
        }
    }

    private static bool HasCompleteScope(CalculationScope? scope) =>
        scope is not null
        && !string.IsNullOrWhiteSpace(scope.TenantId)
        && !string.IsNullOrWhiteSpace(scope.LegalEntityId)
        && !string.IsNullOrWhiteSpace(scope.SkuId)
        && !string.IsNullOrWhiteSpace(scope.WarehouseId);

    private static bool TryCalculateRawCandidate(
        decimal totalDemand,
        decimal coverageDays,
        int calendarDayCount,
        out decimal rawCandidateQuantity)
    {
        rawCandidateQuantity = 0m;
        if (totalDemand == 0m)
            return true;

        var (demandCoefficient, demandScale) = GetDecimalParts(totalDemand);
        var (coverageCoefficient, coverageScale) = GetDecimalParts(coverageDays);
        var numerator = demandCoefficient * coverageCoefficient;
        var denominator = calendarDayCount * BigInteger.Pow(10, demandScale + coverageScale);
        var maximumCoefficient = (BigInteger.One << 96) - 1;
        if (numerator > maximumCoefficient * denominator)
            return false;

        for (var scale = 28; scale >= 0; scale--)
        {
            var coefficient = BigInteger.DivRem(numerator * BigInteger.Pow(10, scale), denominator, out var remainder);
            var twiceRemainder = remainder * 2;
            if (twiceRemainder > denominator || (twiceRemainder == denominator && !coefficient.IsEven))
                coefficient++;

            if (coefficient > maximumCoefficient)
                continue;

            if (coefficient.IsZero)
                return false;

            while (scale > 0 && coefficient % 10 == 0)
            {
                coefficient /= 10;
                scale--;
            }

            var low = unchecked((int)(uint)(coefficient & uint.MaxValue));
            var middle = unchecked((int)(uint)((coefficient >> 32) & uint.MaxValue));
            var high = unchecked((int)(uint)((coefficient >> 64) & uint.MaxValue));
            rawCandidateQuantity = new decimal(low, middle, high, false, (byte)scale);
            return true;
        }

        return false;
    }

    private static (BigInteger Coefficient, int Scale) GetDecimalParts(decimal value)
    {
        var bits = decimal.GetBits(value);
        var coefficient = ((BigInteger)(uint)bits[2] << 64)
            + ((BigInteger)(uint)bits[1] << 32)
            + (uint)bits[0];
        var scale = (bits[3] >> 16) & 0xFF;
        return (coefficient, scale);
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static MethodAResult Fail(MethodAError error, string detail) =>
        MethodAResult.Failure(error, detail);
}
