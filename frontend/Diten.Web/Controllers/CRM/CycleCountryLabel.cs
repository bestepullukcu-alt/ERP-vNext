namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-CYC-UI-FIX-2 — how the cycle screens (period finder, period panel, capacity calendar country) name a country:
/// in the READER's language, from the project's existing localized source (<see cref="ClaimDisplayNames.CountryName"/>,
/// ICU — never a compiled list), followed by the code the record stores ("Türkiye (TR)" under tr, "Turquie (TR)" under
/// fr). A code ICU does not know keeps the reference-set label (or the code) — never a guess.
/// </summary>
public static class CycleCountryLabel
{
    public static string For(string? code, string? fallbackLabel = null)
    {
        var clean = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (clean.Length == 0)
        {
            return fallbackLabel ?? string.Empty;
        }

        var name = clean.Length == 2 ? ClaimDisplayNames.CountryName(clean) : null;
        return name is null ? (string.IsNullOrWhiteSpace(fallbackLabel) ? clean : fallbackLabel!) : $"{name} ({clean})";
    }
}
