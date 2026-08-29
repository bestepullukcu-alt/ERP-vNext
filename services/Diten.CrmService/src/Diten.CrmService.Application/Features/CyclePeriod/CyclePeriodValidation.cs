using System.Text.RegularExpressions;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.CyclePeriod;

/// <summary>
/// MOD-0165 FU06 shared write-path validation. Kept in ONE place so create / update / activate / close can never drift
/// apart. Everything here is <b>structural and in-domain</b> (D-VOCAB = A) and performs <b>no I/O</b>: the set rules
/// (code uniqueness, sequence uniqueness, the active-overlap ban) need other rows and therefore live in
/// <see cref="Rules.CyclePeriodOverlapRules"/>, called from the handler.
/// </summary>
public static class CyclePeriodValidation
{
    /// <summary>A rejected write: a message for the human, a machine code for the UI/smoke script, and the status the
    /// handler must answer with. Nested so this file still declares a single top-level public type.</summary>
    public sealed record Failure(string Message, string? Code, int StatusCode = 400);

    private static readonly Regex CodePattern = new("^[a-z0-9][a-z0-9._-]*$", RegexOptions.Compiled);

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Normalises a date to UTC midnight. A period is a run of DAYS: keeping the caller's clock time would
    /// make "1 March" mean a different instant per client, and the inclusive end date would silently exclude most of
    /// its own last day.</summary>
    public static DateTimeOffset ToDay(DateTimeOffset value)
        => new(value.UtcDateTime.Date, TimeSpan.Zero);

    public static Failure? ValidateCycleCode(string? cycleCode)
    {
        var code = Trim(cycleCode);
        if (code is null)
        {
            return new Failure("CycleCode is required.", "cycle_period_code_required");
        }

        if (code.Length > CyclePeriodLimits.MaxCycleCodeLength)
        {
            return new Failure(
                $"CycleCode must be at most {CyclePeriodLimits.MaxCycleCodeLength} characters.",
                "cycle_period_code_invalid");
        }

        return CodePattern.IsMatch(code.ToLowerInvariant())
            ? null
            : new Failure(
                "CycleCode must be lowercase and may contain only letters, digits, dot, underscore and hyphen.",
                "cycle_period_code_invalid");
    }

    public static Failure? ValidateCycleName(string? cycleName)
    {
        var name = Trim(cycleName);
        if (name is null)
        {
            return new Failure("CycleName is required.", "cycle_period_name_required");
        }

        return name.Length <= CyclePeriodLimits.MaxCycleNameLength
            ? null
            : new Failure(
                $"CycleName must be at most {CyclePeriodLimits.MaxCycleNameLength} characters.",
                "cycle_period_name_invalid");
    }

    public static Failure? ValidateYear(int year)
        => year is >= CyclePeriodLimits.MinYear and <= CyclePeriodLimits.MaxYear
            ? null
            : new Failure(
                $"Year must be between {CyclePeriodLimits.MinYear} and {CyclePeriodLimits.MaxYear}.",
                "cycle_period_year_invalid");

    public static Failure? ValidateSequenceInYear(int sequenceInYear)
        => sequenceInYear is >= CyclePeriodLimits.MinSequenceInYear and <= CyclePeriodLimits.MaxSequenceInYear
            ? null
            : new Failure(
                $"SequenceInYear must be between {CyclePeriodLimits.MinSequenceInYear} and "
                + $"{CyclePeriodLimits.MaxSequenceInYear}.",
                "cycle_period_sequence_invalid");

    /// <summary>EndDate is INCLUSIVE and must be strictly after StartDate. Equal dates are refused rather than read as
    /// a one-day period: a plan nobody can distinguish from a typo is not a plan.</summary>
    public static Failure? ValidateWindow(DateTimeOffset startDate, DateTimeOffset endDate)
        => ToDay(endDate) > ToDay(startDate)
            ? null
            : new Failure("EndDate must be after StartDate.", "cycle_period_window_invalid");

    /// <summary>
    /// The period must START in the planning year it claims. <see cref="Domain.Entities.CyclePeriod.Year"/> is authored
    /// rather than derived, which is what lets a period run past new year's eve — but an authored value nothing checks
    /// is a value that silently goes wrong: "2026 / cycle 1" beginning in March 2027 would sort, group and resolve as a
    /// 2026 period while covering none of it, and nobody would see the mismatch until a plan came out wrong.
    /// <para>Only the START is anchored. The END is deliberately free: a cycle that begins in December and ends in
    /// January is the whole reason the year is a separate field.</para>
    /// <para>The comparison uses the NORMALISED day, so it judges the date that will actually be stored: a start of
    /// 1 Jan 2027 00:00+03:00 lands on 31 Dec 2026 UTC and belongs to 2026 — the same reading the resolver uses.</para>
    /// </summary>
    public static Failure? ValidateStartYearAnchor(int year, DateTimeOffset startDate)
    {
        var startYear = ToDay(startDate).Year;
        return startYear == year
            ? null
            : new Failure(
                $"StartDate falls in {startYear}, but the period is filed under Year {year}. "
                + "A period must start in its planning year; only the end date may cross into the next one.",
                Contract.CyclePeriodErrorCodes.StartYearMismatch);
    }

    public static Failure? ValidateBusinessUnitId(string? businessUnitId)
    {
        var value = Trim(businessUnitId);
        if (value is null)
        {
            return null;
        }

        return value.Length <= CyclePeriodLimits.MaxBusinessUnitIdLength
            ? null
            : new Failure(
                $"BusinessUnitId must be at most {CyclePeriodLimits.MaxBusinessUnitIdLength} characters.",
                "cycle_period_business_unit_invalid");
    }

    public static Failure? ValidateDescription(string? description)
    {
        var value = Trim(description);
        if (value is null)
        {
            return null;
        }

        return value.Length <= CyclePeriodLimits.MaxDescriptionLength
            ? null
            : new Failure(
                $"Description must be at most {CyclePeriodLimits.MaxDescriptionLength} characters.",
                "cycle_period_description_invalid");
    }

    /// <summary>Fail-closed vocabulary check for a status arriving as a FILTER value. An unknown status is refused
    /// rather than ignored, so a UI never silently gets "everything" when it asked for something specific.</summary>
    public static Failure? ValidateStatusFilter(string? cycleStatus)
    {
        var value = Trim(cycleStatus);
        if (value is null)
        {
            return null;
        }

        return CyclePeriodStatuses.IsKnown(value)
            ? null
            : new Failure(
                $"Unknown CycleStatus '{value}'. Known values: {string.Join(", ", CyclePeriodStatuses.All)}.",
                "cycle_period_status_unknown");
    }

    /// <summary>Fail-closed vocabulary check for a scope type arriving as a FILTER value. Same reasoning as the status
    /// filter: an unknown level is refused rather than widened to "everything".</summary>
    public static Failure? ValidateScopeTypeFilter(string? scopeType)
    {
        var value = Trim(scopeType);
        if (value is null)
        {
            return null;
        }

        return CyclePeriodScopeTypes.IsKnown(value)
            ? null
            : new Failure(
                $"Unknown ScopeType '{value}'. Known values: {string.Join(", ", CyclePeriodScopeTypes.All)}.",
                Contract.CyclePeriodErrorCodes.ScopeTypeUnknown);
    }

    /// <summary>
    /// FU07 — the shape of a write, excluding the scope. Scope normalisation and its single-reference invariant live in
    /// <see cref="Rules.CyclePeriodScopeRules"/>, because they decide identity and belong next to the key rather than
    /// among the field-length checks.
    /// </summary>
    public static Failure? ValidateShape(
        string? cycleName,
        int year,
        int sequenceInYear,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string? description)
        => ValidateCycleName(cycleName)
           ?? ValidateYear(year)
           ?? ValidateSequenceInYear(sequenceInYear)
           ?? ValidateWindow(startDate, endDate)
           // After the window check, so an inverted window is reported as an inverted window rather than as a year
           // mismatch. Create and update both come through here, so the anchor cannot hold on one path only.
           ?? ValidateStartYearAnchor(year, startDate)
           ?? ValidateDescription(description);

    public static IReadOnlyList<string> ToErrors(Failure failure)
        => failure.Code is null ? new[] { failure.Message } : new[] { failure.Message, failure.Code };
}
