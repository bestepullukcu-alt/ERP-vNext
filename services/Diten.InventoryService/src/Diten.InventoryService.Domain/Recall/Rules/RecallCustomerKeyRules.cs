namespace Diten.InventoryService.Domain.Recall.Rules;

/// <summary>
/// MOD-0177-FU01 (pack §8.3) — the one GUID-form rule of a recall customer key. A CRM key is an account id stored in the canonical "D" form
/// (lower case, 36 characters); a key that is not "D"-shaped is compared ordinally, as before FU01.
/// </summary>
public static class RecallCustomerKeyRules
{
    /// <summary>The trimmed value is exactly 36 characters and a GUID in the "D" form (any case).</summary>
    public static bool IsDShaped(string? value)
    {
        if (value is null) return false;
        var trimmed = value.Trim();
        return trimmed.Length == 36 && Guid.TryParseExact(trimmed, "D", out _);
    }

    /// <summary>The canonical "D" text (lower case) of a "D"-shaped, non-empty GUID; false otherwise.</summary>
    public static bool TryCanonical(string? value, out string canonical)
    {
        canonical = string.Empty;
        if (!IsDShaped(value)) return false;
        var id = Guid.ParseExact(value!.Trim(), "D");
        if (id == Guid.Empty) return false;
        canonical = id.ToString("D");
        return true;
    }

    /// <summary>The fingerprint form of a command's customerRef: canonical when it is a "D" GUID, else the trimmed value itself (pack §8.3).</summary>
    public static string? FingerprintForm(string? trimmed) => TryCanonical(trimmed, out var canonical) ? canonical : trimmed;
}
