using System.Text;
using Diten.CrmService.Domain.Entities;
using PeriodEntity = Diten.CrmService.Domain.Entities.CyclePeriod;

namespace Diten.CrmService.Application.Features.CyclePeriod.Rules;

/// <summary>
/// WP-CAP-MODEL (K-2) — the cycle-code SUGGESTION, as pure functions: no repository, no clock, no I/O.
/// <para><b>A suggestion, never a rule.</b> The create command still takes its code from the request; uniqueness,
/// lower-case storage and immutability are enforced exactly where they always were. This only proposes
/// <c>{PREFIX}-{YYYY}-{NN}</c>:</para>
/// <list type="bullet">
/// <item><description>country → the ISO alpha-2 code (<c>TR-2026-04</c>);</description></item>
/// <item><description>tenant (whole company) → <see cref="TenantPrefix"/> (<c>GM-2027-01</c>);</description></item>
/// <item><description>legal entity / business unit → its short code from the catalog, else <see cref="LegalEntityFallback"/>
/// / <see cref="BusinessUnitFallback"/>.</description></item>
/// </list>
/// <para><c>NN</c> is the first sequence in the scope and year that no period holds — closed ones included, by the very
/// rule <see cref="CyclePeriodOverlapRules.IsSequenceTaken"/> the write path enforces.</para>
/// </summary>
public static class CyclePeriodCodeSuggestionRules
{
    /// <summary>Whole-company (tenant-scope) prefix, as decided in the cycle-planning mockup analysis (K-2).</summary>
    public const string TenantPrefix = "GM";

    public const string LegalEntityFallback = "LE";
    public const string BusinessUnitFallback = "BU";

    /// <summary>"-YYYY-NN" — the part of the code after the prefix.</summary>
    private const int SuffixLength = 8;

    /// <summary>The prefix for a normalised scope. <paramref name="catalogCode"/> is the legal entity's / business
    /// unit's short code when the catalog knows it; anything unusable falls back to LE / BU.</summary>
    public static string Prefix(CyclePeriodScopeRules.NormalizedScope scope, string? catalogCode)
        => scope.ScopeType switch
        {
            CyclePeriodScopeTypes.Tenant => TenantPrefix,
            CyclePeriodScopeTypes.Country => scope.CountryScope!,
            CyclePeriodScopeTypes.LegalEntity => Sanitize(catalogCode) ?? LegalEntityFallback,
            _ => Sanitize(catalogCode) ?? BusinessUnitFallback
        };

    /// <summary>The first sequence (1..99) no row of this scope and year holds, or <c>null</c> when all are taken.
    /// <paramref name="rowsOfYear"/> must be ALL the tenant's rows of the year, closed ones included.</summary>
    public static int? NextFreeSequence(IEnumerable<PeriodEntity> rowsOfYear, string scopeType, string? scopeRef)
    {
        var rows = rowsOfYear.ToList();
        for (var sequence = CyclePeriodLimits.MinSequenceInYear; sequence <= CyclePeriodLimits.MaxSequenceInYear; sequence++)
        {
            if (!CyclePeriodOverlapRules.IsSequenceTaken(rows, scopeType, scopeRef, sequence))
            {
                return sequence;
            }
        }

        return null;
    }

    public static string Format(string prefix, int year, int sequence) => $"{prefix}-{year:0000}-{sequence:00}";

    /// <summary>Upper-cased, reduced to the characters a cycle code may hold (letters, digits, dot, underscore, hyphen),
    /// starting with a letter or digit, and short enough to leave room for <c>-YYYY-NN</c>. <c>null</c> when nothing
    /// usable remains.</summary>
    public static string? Sanitize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var c in code.Trim().ToUpperInvariant())
        {
            var allowed = c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-';
            if (allowed && (builder.Length > 0 || char.IsLetterOrDigit(c)))
            {
                builder.Append(c);
            }
        }

        var maxLength = CyclePeriodLimits.MaxCycleCodeLength - SuffixLength;
        var value = builder.Length > maxLength ? builder.ToString(0, maxLength) : builder.ToString();
        return value.Length == 0 ? null : value;
    }
}
