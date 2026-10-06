namespace Diten.CrmService.Domain.Entities;

/// <summary>WP-KP-1 — the chain context failures of a chain-bound knowledge path (country / language / component
/// language). Rendered as <c>[code, message]</c>. WP-CLN-1: moved out of the removed <c>ContentSet.cs</c> (the content
/// set and its <c>ContentSetContextErrors</c> aliases are gone; the codes are unchanged).</summary>
public static class ChainContextErrors
{
    /// <summary>400 — the country is missing or not an active <c>COUNTRY_CODES</c> value.</summary>
    public const string CountryInvalid = "country_invalid";

    /// <summary>400 — the language is missing or not one of the country's <c>country-content-languages</c>.</summary>
    public const string LanguageNotInCountry = "language_not_in_country";

    /// <summary>503 — COUNTRY_CODES / country-content-languages cannot be read (never validated against a local list).</summary>
    public const string ReferenceSetUnavailable = "reference_set_unavailable";

    /// <summary>409 — a component is not in the single language of the chain-bound path.</summary>
    public const string ComponentLanguageMismatch = "component_language_mismatch";
}
