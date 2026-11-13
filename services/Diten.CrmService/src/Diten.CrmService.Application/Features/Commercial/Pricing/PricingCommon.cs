using System.Globalization;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.Pricing;

/// <summary>WP-VW-W6 W6-BE-b — limits of MOD-0156 §4 / §13.</summary>
public static class PricingLimits
{
    public const int NameMax = 120;
    public const int ReasonMax = 500;
    public const int FxSourceMax = 60;
    public const int ImportMaxBytes = 5 * 1024 * 1024;
    public const int ImportMaxRows = 10_000;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;
    public const int PriceDecimals = 4;
    public const int FxDecimals = 6;
}

/// <summary>The small shared rules of MOD-0156 (one copy): the day, the phase of a published list, the list in force, the
/// response shape of a refusal, the number parser of the file and the manual form.</summary>
public static class PricingCommon
{
    /// <summary>"Today" (§8). CRM has no tenant time zone yet: the UTC day of the injected clock.</summary>
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(new[] { code, message }, status);

    public static Response<T> NotFound<T>() => Fail<T>(PricingErrors.NotFound, "Not found.", 404);

    public static Response<T> Invalid<T>(string message) => Fail<T>(PricingErrors.Invalid, message, 400);

    public static Response<T> Stale<T>() => Fail<T>(PricingErrors.Stale, "The record was changed by someone else; reload and retry.", 409);

    public static Response<T> MdmUnavailable<T>() => Fail<T>(PricingErrors.MdmUnavailable,
        "The product / legal entity master cannot be reached. Nothing was changed.", 503);

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsCurrency(string? value) => value is { Length: 3 } && value.All(c => c is >= 'A' and <= 'Z');

    public static string NormalizeCurrency(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool In(string? value, IReadOnlyList<string> set) => value is not null && set.Contains(value, StringComparer.Ordinal);

    /// <summary>The significant decimals of a value (12.500000 has two).</summary>
    public static int Decimals(decimal value) => (decimal.GetBits(value.Normalize())[3] >> 16) & 0xFF;

    /// <summary>A number from a file cell or a form ("12.50", "12,50", "1.234,50", "1,234.50"); null when it is not one.</summary>
    public static decimal? ParseNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var s = text.Trim().Replace(" ", string.Empty).Replace("%", string.Empty);
        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            s = lastComma > lastDot ? s.Replace(".", string.Empty).Replace(',', '.') : s.Replace(",", string.Empty);
        }
        else if (lastComma >= 0)
        {
            s = s.Replace(',', '.');
        }

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value.Normalize() : null;
    }

    public static decimal Normalize(this decimal value) => value / 1.000000000000000000000000000000000m;

    /// <summary>
    /// The phase of a published list on <paramref name="today"/> (§4.1.1–§4.1.2): planned before its first day; past after its
    /// own end or once a LATER published list of the same (legal entity, country) has come into force; else in force.
    /// <paramref name="sameKey"/> = the published lists of the same (legal entity, country), this one included or not.
    /// </summary>
    public static string? PhaseOf(PriceList list, IEnumerable<PriceList> sameKey, DateOnly today)
    {
        if (!list.IsPublished())
        {
            return null;
        }

        if (list.ValidFrom > today)
        {
            return PriceListPhases.Planned;
        }

        if (list.ValidTo is { } end && end < today)
        {
            return PriceListPhases.Past;
        }

        return sameKey.Any(other => other.Id != list.Id && other.IsPublished() && other.ValidFrom > list.ValidFrom && other.ValidFrom <= today)
            ? PriceListPhases.Past
            : PriceListPhases.InForce;
    }

    /// <summary>The ONE list in force on <paramref name="day"/> among the published lists of a (legal entity, country): the
    /// latest first day not after the day whose own end has not passed. Null = none (quantity mode).</summary>
    public static PriceList? InForceOn(IEnumerable<PriceList> published, DateOnly day)
        => published.Where(l => l.IsPublished() && l.ValidFrom <= day)
            .OrderByDescending(l => l.ValidFrom)
            .FirstOrDefault() is { } latest && (latest.ValidTo is null || latest.ValidTo >= day)
            ? latest
            : null;

    /// <summary>The day the list ends: its own end, else the day before the next published list of the same key begins.</summary>
    public static DateOnly? EffectiveValidTo(PriceList list, IEnumerable<PriceList> sameKey)
    {
        var next = sameKey.Where(o => o.Id != list.Id && o.IsPublished() && o.ValidFrom > list.ValidFrom)
            .Select(o => (DateOnly?)o.ValidFrom.AddDays(-1)).Min();
        return (list.ValidTo, next) switch
        {
            (null, null) => null,
            ({ } own, null) => own,
            (null, { } n) => n,
            ({ } own, { } n) => own < n ? own : n
        };
    }
}
