using System.Text.RegularExpressions;

namespace Diten.MdmService.Application.Features.BankMaster;

/// <summary>
/// MOD-0289 DL-1 — the pure normalization and format rules of a bank and a branch (pack "Validation Rules"). No I/O: a
/// validator, a handler and a test all use the same function. A value that does not fit is reported by the caller; nothing
/// here echoes the value (the validator messages carry no <c>{PropertyValue}</c>).
/// </summary>
public static class BankRules
{
    public const int NameMaxLength = 200;
    public const int NationalCodeMaxLength = 35;
    public const int CityMaxLength = 100;
    public const int SearchMaxLength = 100;
    public const int ReasonCodeMaxLength = 64;
    public const int BulkDeleteMaxIds = 100;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    // ISO 9362: 4 letters (institution) + 2 letters (country) + 2 letters / digits (location) + optional 3 (branch).
    private static readonly Regex BicPattern = new(
        "^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$", RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex CountryPattern = new("^[A-Z]{2}$", RegexOptions.CultureInvariant, MatchTimeout);
    private static readonly Regex CodePattern = new("^[A-Z0-9]+$", RegexOptions.CultureInvariant, MatchTimeout);

    /// <summary>Trim; blank is null.</summary>
    public static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>A status name (case-insensitive); a number or an unknown name is not a status.</summary>
    public static bool TryParseStatus(string? value, out Diten.MdmService.Domain.Enums.BankLifecycleStatus status)
    {
        status = default;
        var text = value?.Trim();
        return !string.IsNullOrEmpty(text) && !char.IsDigit(text[0])
               && Enum.TryParse(text, true, out status) && Enum.IsDefined(status);
    }

    public static bool HasControlCharacters(string value) => value.Any(char.IsControl);

    /// <summary>Trim, upper case (invariant); blank is null.</summary>
    public static string? NormalizeCountry(string? value) => NormalizeText(value)?.ToUpperInvariant();

    public static bool IsValidCountry(string? value) => value is not null && CountryPattern.IsMatch(value);

    /// <summary>Every blank removed, upper case (invariant); blank is null.</summary>
    public static string? NormalizeBic(string? value)
    {
        if (value is null) return null;
        var compact = new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        return compact.Length == 0 ? null : compact;
    }

    /// <summary>ISO 9362 (8 or 11 characters) and characters 5–6 equal the country.</summary>
    public static bool IsValidBic(string? bic, string? countryCode) =>
        bic is not null && countryCode is not null && BicPattern.IsMatch(bic)
        && string.Equals(bic.Substring(4, 2), countryCode, StringComparison.Ordinal);

    /// <summary>A branch BIC is the 11-character form.</summary>
    public static bool IsBranchBic(string? bic) => bic is not null && bic.Length == 11;

    /// <summary>The branch BIC's first 8 characters equal the bank's BIC (when the bank has one).</summary>
    public static bool BranchBicMatchesBank(string branchBic, string? bankBic) =>
        bankBic is null || string.Equals(branchBic[..8], bankBic[..8], StringComparison.Ordinal);

    /// <summary>
    /// National bank code and branch code: trim, every blank and dash removed, upper case; blank is null. The same function
    /// for both (the pack: the branch code normalizes like the national code).
    /// </summary>
    public static string? NormalizeCode(string? value)
    {
        if (value is null) return null;
        var compact = new string(value.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();
        return compact.Length == 0 ? null : compact;
    }

    public static bool IsValidCode(string? code) =>
        code is not null && code.Length is >= 1 and <= NationalCodeMaxLength && CodePattern.IsMatch(code);
}
