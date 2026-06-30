namespace Diten.Platform.Domain.Catalog;

/// <summary>
/// FIX-DOMAIN-SERVICE-CANONICAL — resolves a raw module Domain/Service string (which historically drifted into
/// three formats: manifest enum-name <c>PlatformSharedServices</c>, form DisplayName <c>Platform Shared Servicec</c>,
/// or lookup Code <c>PLATFORM-SHARED-SERVICES</c>) to the canonical lookup <b>Code</b>.
///
/// <para>
/// Matching is format-tolerant: both sides are normalized by dropping every non-alphanumeric character and
/// upper-casing (the same key shape as the navigation menu's <c>NormalizeDomainKey</c>), so a raw value matches a
/// lookup option by EITHER its Code or its DisplayName. On a match the option's <c>Code</c> is returned. On NO
/// match the original (trimmed) value is preserved — never lose data — and the caller logs a warning.
/// </para>
///
/// <para>Pure/stateless: callers pass the active lookup options; the migration, the reconcile handler and the
/// create/update handlers all reuse it.</para>
/// </summary>
public static class ModuleTaxonomyCanonicalizer
{
    /// <summary>A single lookup option: its canonical <c>Code</c> and its presentation <c>DisplayName</c>.</summary>
    public readonly record struct TaxonomyOption(string Code, string DisplayName);

    /// <summary>
    /// Format-tolerant key: drop every non-alphanumeric char (dash/space/dot/underscore) and uppercase, so
    /// "PlatformSharedServices", "PLATFORM-SHARED-SERVICES" and "platform shared services" collapse to one key.
    /// </summary>
    public static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[length++] = char.ToUpperInvariant(ch);
            }
        }

        return new string(buffer[..length]);
    }

    /// <summary>
    /// Resolves <paramref name="rawValue"/> to the canonical Code of the matching option (by normalized Code OR
    /// DisplayName). Returns the trimmed original when nothing matches (data-preserving). Blank input → empty string.
    /// </summary>
    public static string ResolveCode(string? rawValue, IEnumerable<TaxonomyOption> options)
    {
        var trimmed = rawValue?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var key = NormalizeKey(trimmed);
        if (key.Length == 0)
        {
            return trimmed;
        }

        foreach (var option in options)
        {
            if (string.Equals(NormalizeKey(option.Code), key, StringComparison.Ordinal)
                || string.Equals(NormalizeKey(option.DisplayName), key, StringComparison.Ordinal))
            {
                return option.Code;
            }
        }

        return trimmed; // no match → preserve original (caller warns)
    }

    /// <summary>True when <paramref name="rawValue"/> resolves to a known option Code; false when it falls through.</summary>
    public static bool TryResolveCode(string? rawValue, IReadOnlyCollection<TaxonomyOption> options, out string code)
    {
        code = ResolveCode(rawValue, options);
        var key = NormalizeKey(code);
        return key.Length > 0 && options.Any(o => string.Equals(NormalizeKey(o.Code), key, StringComparison.Ordinal));
    }
}
